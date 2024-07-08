using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GeneaGrab.Helpers;
using Vector = Avalonia.Vector;

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

        /// <summary>The user is dragging</summary>
        public event Action<DragProperties>? Dragging;

        /// <summary>The user stopped dragging</summary>
        public event Action<DragProperties>? DraggingStopped;

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
                if (point.Properties.IsMiddleButtonPressed) Reset();
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
                if (Child.Bounds.Size != default) scale = size / Child.Bounds.Size;
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


        private DragProperties dragProperties;

        private void child_MouseLeftButtonDown(object? _, PointerPressedEventArgs e)
        {
            if (Child is null) return;

            var pointer = e.GetCurrentPoint(this);

            var tt = GetTranslateTransform(Child);
            if (tt == null) return;

            dragProperties = new DragProperties(
                GetScaleTransform(Child)?.ScaleX ?? 1,
                Bounds.Size,
                Child.Bounds.Size,
                new Point(tt.X, tt.Y),
                pointer.Position,
                pointer.Position,
                pointer.Properties.PointerUpdateKind.GetMouseButton()
            );
            if (pointer.Properties.IsLeftButtonPressed) Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(Child);
        }

        private void child_MouseLeftButtonUp(object? _, PointerReleasedEventArgs e)
        {
            if (Child is null) return;

            e.Pointer.Capture(null);
            Cursor = new Cursor(StandardCursorType.Arrow);

            if (DraggingStopped == null) return;
            dragProperties.End = e.GetCurrentPoint(this).Position;
            DraggingStopped.Invoke(dragProperties);
        }

        private void child_MouseMove(object? _, PointerEventArgs e)
        {
            if (Child is null || !Equals(e.Pointer.Captured, Child)) return;
            dragProperties.End = e.GetCurrentPoint(this).Position;
            if (dragProperties.PressedButton == MouseButton.Left) MoveTo(dragProperties.Position);
            Dragging?.Invoke(dragProperties);
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

    public class DragProperties(double zoom, Size parentSize, Size childSize, Point origin, Point start, Point end, MouseButton pressedButton)
    {
        public Point End { get; internal set; } = end;
        public MouseButton PressedButton => pressedButton;

        public Rect Area => new Rect(start, End)
            .Translate(-origin) // Subtracts image position in the canvas
            .Translate((childSize * zoom - parentSize) / 2) // From top-left of the image
            .Divide(zoom) // Full-scale
            .Normalize() // Ensure size is positive (if not, it will change the origin)
            .Intersect(new Rect(childSize)) // Remove everything outside the image
            .Round(); // Round values to integers

        public Point Position => origin + End - start;
    }
}
