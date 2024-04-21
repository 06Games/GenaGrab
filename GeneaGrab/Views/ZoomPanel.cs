using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace GeneaGrab.Views
{
    /// <summary>An element for zooming and moving around an image</summary>
    public class ZoomPanel : Panel
    {
        public ZoomPanel() => Background = new SolidColorBrush(Colors.Transparent); //Allows interaction with the element

        /// <summary>The image component</summary>
        private Control? Child
        {
            get => _child;
            set
            {
                _child = value;
                initialized = false;
            }
        }
        private bool initialized;
        private Control? _child;

        public double ZoomMultiplier { get; set; } = 1.5;
        
        /// <summary>The user has moved the child</summary>
        public event Action<double, double>? PositionChanged;
        /// <summary>The user has changed the zoom</summary>
        public event Action<double>? ZoomChanged;

        private Size size;
        protected override Size ArrangeOverride(Size finalSize)
        {
            size = finalSize;
            Initialize()?.Arrange(new Rect(new Point(), finalSize)); //Initialise the element and place the child in it
            return finalSize;
        }
        private Control? Initialize()
        {
            Child ??= Children.FirstOrDefault();
            if (Child is null) return null;
            if (initialized) return Child;

            PointerPressed += (_, e) =>
            {
                var point = e.GetCurrentPoint(this);
                if (point.Properties.IsRightButtonPressed) Reset();
            };

            LayoutUpdated += (_, _) => SetClip();
            SetClip();

            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform());
            group.Children.Add(new TranslateTransform());
            Child.GetObservable(BoundsProperty).Subscribe(_ => Reset());
            Child.RenderTransform = group;
            Child.RenderTransformOrigin = new RelativePoint(new Point(0.5, 0.5), RelativeUnit.Relative);
            Child.PointerWheelChanged += child_MouseWheel;
            Child.PointerPressed += child_MouseLeftButtonDown;
            Child.PointerReleased += child_MouseLeftButtonUp;
            Child.PointerMoved += child_MouseMove;

            initialized = true;
            return Child;

            void SetClip() => Clip = new RectangleGeometry { Rect = new Rect(0, 0, Bounds.Width, Bounds.Height) }; //Prevents the child from being rendered out of the element
        }

        private static TranslateTransform? GetTranslateTransform(Visual element)
        {
            if (element.RenderTransform is TransformGroup group) return group.Children.First(tr => tr is TranslateTransform) as TranslateTransform;
            return null;
        }
        private static ScaleTransform? GetScaleTransform(Visual element)
        {
            if (element.RenderTransform is TransformGroup group) return group.Children.FirstOrDefault(tr => tr is ScaleTransform) as ScaleTransform;
            return null;
        }

        /// <summary>Resets the child's position and zoom</summary>
        public void Reset()
        {
            if (Child is null) return;

            // Reset zoom
            var st = GetScaleTransform(Child);
            if (st != null)
            {
                var scale = Vector.One;
                if (Child.Bounds.Width != 0 && Child.Bounds.Height != 0) scale = size / Child.Bounds.Size;
                st.ScaleX = st.ScaleY = Math.Min(scale.X, scale.Y);
            }

            // Reset position
            var tt = GetTranslateTransform(Child);
            if (tt != null) tt.X = tt.Y = 0.0;
        }

        #region Child Events

        private void child_MouseWheel(object? _, PointerWheelEventArgs e)
        {
            if (Child is null) return;

            var st = GetScaleTransform(Child);
            var tt = GetTranslateTransform(Child);
            if (st == null || tt == null) return;
            var position = new Point(tt.X, tt.Y);

            var delta = e.Delta.Y;
            var zoom = ZoomMultiplier;
            if (delta < 0) zoom = 1 / zoom;
            if (st.ScaleX * zoom < .1 || st.ScaleX * zoom > 10) return;

            var pointer = e.GetCurrentPoint(Child).Position;
            var childTopRight = new Point(Child.Bounds.Width, Child.Bounds.Height);
            var pointerFromCenter = pointer - childTopRight / 2;
            var oldZoom = st.ScaleX;
            st.ScaleX = st.ScaleY *= zoom;
            MoveTo(position + pointerFromCenter * oldZoom * (1 - zoom)); // position + oldMousePosFromCenter - currentMousePosFromCenter
            ZoomChanged?.Invoke(st.ScaleX);
        }


        private Point origin;
        private Point start;

        private void child_MouseLeftButtonDown(object? _, PointerPressedEventArgs e)
        {
            if (Child is null) return;

            var tt = GetTranslateTransform(Child);
            if (tt == null) return;

            start = e.GetCurrentPoint(this).Position;
            origin = new Point(tt.X, tt.Y);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(Child);
        }

        private void child_MouseLeftButtonUp(object? _, PointerReleasedEventArgs e)
        {
            if (Child is null) return;

            e.Pointer.Capture(null);
            Cursor = new Cursor(StandardCursorType.Arrow);
        }

        private void child_MouseMove(object? _, PointerEventArgs e)
        {
            if (Child is null || !Equals(e.Pointer.Captured, Child)) return;
            var (mouseX, mouseY) = e.GetCurrentPoint(this).Position;
            MoveTo(origin.X + mouseX - start.X, origin.Y + mouseY - start.Y);
        }

        private void MoveTo(Point p) => MoveTo(p.X, p.Y);
        private void MoveTo(double x, double y)
        {
            if (Child is null) return;
            var st = GetScaleTransform(Child);
            var tt = GetTranslateTransform(Child);
            if (st == null || tt == null) return;

            var max = Bounds.Size / 4 + Child.Bounds.Size * st.ScaleX / 2;
            tt.X = Math.Max(Math.Min(x, max.Width), -max.Width);
            tt.Y = Math.Max(Math.Min(y, max.Height), -max.Height);
            PositionChanged?.Invoke(tt.X, tt.Y);
        }

        #endregion
    }
}
