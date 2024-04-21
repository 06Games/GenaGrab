using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DiscordRPC;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Models.Indexing;
using GeneaGrab.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Threading;
using Button = DiscordRPC.Button;
using Frame = GeneaGrab.Core.Models.Frame;

namespace GeneaGrab.Views
{
    public partial class RegistryViewer : Page, INotifyPropertyChanged, ITabPage
    {
        public Symbol IconSource => Symbol.Pictures;
        public string? DynaTabHeader
        {
            get
            {
                if (Registry is null) return null;
                var location = string.Join(", ", Registry.Location);
                var registry = Registry.GetDescription();
                return location.Length == 0 ? registry : $"{location}: {registry}";
            }
        }
        public string? Identifier => Registry?.Id;
        public async Task RichPresence(RichPresence richPresence)
        {
            if (Provider is null) return;
            var url = await Provider.Ark(Frame);
            richPresence.Buttons =
            [
                new Button
                {
                    Label = ResourceExtensions.GetLocalized("Discord.OpenRegistry", ResourceExtensions.Resource.UI),
                    Url = Uri.IsWellFormedUriString(url, UriKind.Absolute) ? url : Registry?.URL
                }
            ];
        }



        private Provider? Provider => Frame?.Provider;
        public Registry? Registry { get; private set; }
        public Frame? Frame { get; private set; }

        public RegistryViewer()
        {
            InitializeComponent();
            DataContext = this;

            var pageNumber = PageNumber;
            if (pageNumber != null)
                pageNumber.ValueChanged += (s, ne) =>
                {
                    var frame = Registry?.Frames.FirstOrDefault(f => f.FrameNumber == (int)ne.NewValue);
                    if (frame != null) _ = ChangePageAsync(frame);
                };

            FrameNotes.TextChanging += (_, _) => Task.Run(() => SaveAsync(Frame).ContinueWith(_ =>
            {
                if (Frame is null) return;
                var frameItem = PageList.Items.Cast<PageList>().FirstOrDefault(f => f.Number == Frame.FrameNumber);
                frameItem?.OnPropertyChanged(nameof(frameItem.Notes));
            }, TaskScheduler.Current));

            Image.GetObservable(BoundsProperty).Subscribe(b =>
            {
                MainGrid.Width = b.Width;
                MainGrid.Height = b.Height;
            });
        }

        private static async Task SaveAsync<T>(T entity)
        {
            if (entity is null) return;
            await using var db = new DatabaseContext();
            db.Update(entity);
            await db.SaveChangesAsync();
        }

        public override async void OnNavigatedTo(NavigationEventArgs args)
        {
            base.OnNavigatedTo(args);
            var (keepTabOpened, noRefresh) = await LoadRegistryAsync(args.Parameter).ConfigureAwait(false);
            if (!keepTabOpened)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (NavigationService.CanGoBack) NavigationService.GoBack();
                    else NavigationService.CloseTab();
                });
                return;
            }

            var tab = await Dispatcher.UIThread.InvokeAsync(() => NavigationService.TryGetTabWithId(Registry?.Id, out var tab) ? tab : null);
            if (tab != null)
            {
                var currentTab = NavigationService.CurrentTab;
                NavigationService.OpenTab(tab);
                if (NavigationService.Frame?.Content is not RegistryViewer viewer) return;
                await viewer.ChangePageAsync(Frame!.FrameNumber);
                if (!ReferenceEquals(viewer, this))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => NavigationService.CloseTab(currentTab!));
                    return;
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshView();
                MainWindow.UpdateSelectedTitle();
            });
            if (noRefresh || Provider is null || Registry is null || Frame is null) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var pageNumber = PageNumber;
                pageNumber.Minimum = Registry.Frames.Any() ? Registry.Frames.Min(p => p.FrameNumber) : 0;
                pageNumber.Maximum = Registry.Frames.Any() ? Registry.Frames.Max(p => p.FrameNumber) : 0;
            });

            AuthenticateIfNeeded(Provider, nameof(Provider.GetFrame));
            _ = Task.Run(async () =>
            {
                var img = await Provider.GetFrame(Frame, Scale.Navigation, TrackProgress);
                await Dispatcher.UIThread.InvokeAsync(() => RefreshView(img));
                await Dispatcher.UIThread.InvokeAsync(() => PageList.ItemsSource = GetFramesList());
            });
        }

        private async Task<(bool success, bool inRam)> LoadRegistryAsync(object parameter)
        {
            var inRam = false;

            await using var db = new DatabaseContext();
            switch (parameter)
            {
                case RegistryInfo infos:
                    Registry = db.Registries.Include(r => r.Frames).FirstOrDefault(r => r.ProviderId == infos.ProviderId && r.Id == infos.RegistryId)!;
                    Frame = Registry.Frames.FirstOrDefault(f => f.FrameNumber == infos.PageNumber) ?? Registry.Frames.FirstOrDefault();
                    break;
                case Uri url:
                    RegistryInfo? info = null;
                    Provider? provider = null;
                    foreach (var p in Data.Providers.Values)
                        if ((info = await p.GetRegistryFromUrlAsync(url)) != null)
                        {
                            provider = p;
                            break;
                        }
                    if (provider != null && info != null)
                    {
                        var registry = db.Registries.Include(r => r.Frames).FirstOrDefault(r => r.ProviderId == info.ProviderId && r.Id == info.RegistryId);
                        if (registry is null)
                        {
                            AuthenticateIfNeeded(provider, nameof(Provider.Infos));
                            var data = await provider.Infos(url);
                            registry = data.registry;
                            db.Registries.Add(registry);
                            await db.SaveChangesAsync();
                        }
                        Registry = registry;
                        Frame = Registry.Frames.FirstOrDefault(f => f.FrameNumber == info.PageNumber) ?? Registry.Frames.FirstOrDefault();
                    }
                    break;
                default:
                    inRam = true;
                    break;
            }
            return (Registry != null && Frame != null, inRam);
        }

        protected internal static void AuthenticateIfNeeded(Provider provider, string method)
        {
            if (!provider.NeedsAuthentication(method)) return;
            if (provider is IAuthentification auth && SettingsService.SettingsData.Credentials.TryGetValue(provider.Id, out var credentials)) auth.Authenticate(credentials);
            else throw new AuthenticationException("Couldn't authenticate");
        }


        private void GoToPreviousPage(object? _, RoutedEventArgs _1) => ChangePageAsync(Frame?.FrameNumber - 1 ?? -1);
        private void GoToNextPage(object? _, RoutedEventArgs _1) => ChangePageAsync(Frame?.FrameNumber + 1 ?? -1);
        private void ChangePage(object _1, SelectionChangedEventArgs e)
        {
            if (e.AddedItems is [PageList page, ..]) _ = ChangePageAsync(page.Page);
        }
        private Task ChangePageAsync(int pageNumber) => ChangePageAsync(Registry?.Frames.FirstOrDefault(f => f.FrameNumber == pageNumber));
        private async Task ChangePageAsync(Frame? page)
        {
            if (page is null || Provider is null || Frame is null || Frame.FrameNumber == page.FrameNumber) return;
            Frame = page;
            AuthenticateIfNeeded(Provider, nameof(Provider.GetFrame));
            var image = await Provider.GetFrame(page, Scale.Navigation, TrackProgress);
            RefreshView(image);
            await SaveAsync(Frame);
        }
        private void RefreshView(Stream? img = null)
        {
            if (Registry is null || Frame is null) return;

            var pageTotal = Registry.Frames.Any() ? Registry.Frames.Max(p => p.FrameNumber) : 0;
            PageNumber.Value = Frame.FrameNumber;
            PageTotal.Text = $"/ {pageTotal}";
            PreviousPage.IsEnabled = Frame.FrameNumber > 1;
            NextPage.IsEnabled = Frame.FrameNumber < pageTotal;

            DisplayIndex();

            var image = Image;
            var pageList = PageList;
            if (img != null) image.Source = img.ToBitmap();
            pageList.Selection.Select(Frame.FrameNumber - 1);
            pageList.ScrollIntoView(Frame.FrameNumber - 1);
            ImagePanel.Reset();
            ImagePanel.ZoomMultiplier = SettingsService.SettingsData.ZoomMultiplier;
            OnPropertyChanged(nameof(image));
            OnPropertyChanged(nameof(Registry));
            OnPropertyChanged(nameof(Frame));
        }

        private IEnumerable<PageList> GetFramesList()
        {
            if (Provider == null || Registry == null) return Array.Empty<PageList>();
            var result = new List<PageList>(Registry.Frames.Select(f => new PageList(f)));
            _ = Task.Run(async () =>
            {
                var tasks = new List<Task<PageList>>();
                var unsavedCount = 0;
                foreach (var frame in result)
                {
                    if (tasks.Count >= 5)
                    {
                        tasks.Remove(await Task.WhenAny(tasks));
                        unsavedCount++;
                        if (unsavedCount > 30)
                        {
                            unsavedCount = 0;
                            await SaveAsync(Registry);
                        }
                    }
                    tasks.Add(Task.Run(async () =>
                    {
                        await frame.GetThumbnailAsync();
                        return frame;
                    }));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                await SaveAsync(Registry);
            });
            return result;
        }

        private void Download(object _1, RoutedEventArgs _2)
        {
            if (Provider is null || Frame == null) return;
            AuthenticateIfNeeded(Provider, nameof(Provider.GetFrame));
            Provider.GetFrame(Frame, Scale.Full, TrackProgress).ContinueWith(t => Dispatcher.UIThread.InvokeAsync(() => RefreshView(t.Result)), TaskScheduler.Current).Forget();
        }
        private void OpenFolder(object _, RoutedEventArgs _1)
        {
            if (Frame == null) return;
            var page = LocalData.GetFile(Frame);
            if (page is null) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try { WinExplorer.OpenFolderAndSelectItem(page.FullName); }
                catch { Process.Start("explorer.exe", "/select,\"" + page.FullName + "\""); }
            }
            else if (page.DirectoryName != null)
            {
                var url = $"file://{page.DirectoryName.Replace(" ", "%20")}";
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
        }
        private void Ark(object _1, RoutedEventArgs _2)
        {
            if (Provider is null || Frame == null) return;
            Provider.Ark(Frame).ContinueWith(t => TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(t.Result).Forget(), TaskScheduler.Current).Forget();
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void TrackProgress(Progress progress) => Dispatcher.UIThread.Post(() =>
        {
            var imageProgress = ImageProgress;
            imageProgress.IsVisible = !progress.Done;
            imageProgress.IsIndeterminate = progress.Undetermined;
            imageProgress.Value = progress.Value;
        });

        #region Index

        private void AddIndex(object _, RoutedEventArgs _1)
        {
            if (Registry == null || Frame == null) return;
            using var db = new DatabaseContext();
            db.Records.Add(new Record(Registry.ProviderId, Registry.Id, Frame.FrameNumber)
            {
                Position = new Rect(100, 75, 100, 50)
            });
            db.SaveChanges();
            DisplayIndex();
        }
        private void DisplayIndex()
        {
            if (Registry == null || Frame == null) return;
            ImageCanvas.Children.Clear();

            using var db = new DatabaseContext();
            var indexes = db.Records.Where(r => r.ProviderId == Registry.ProviderId && r.RegistryId == Registry.Id && r.FrameNumber == Frame.FrameNumber);
            RecordList.ItemsSource = indexes.ToList();
            foreach (var index in indexes)
                DisplayIndexRectangle(index);
        }
        private void DisplayIndexRectangle(Record? index)
        {
            if (index?.Position is null) return;

            var pos = index.Position.Value;
            var btn = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromRgb((byte)(index.Id * 100 % 255), (byte)((index.Id + 2) * 50 % 255), (byte)((index.Id + 1) * 75 % 255))),
                Opacity = .25,
                Width = pos.Width,
                Height = pos.Height
            };

            var tt = new ToolTip { Content = index.ToString() };
            ToolTip.SetTip(btn, tt);

            ImageCanvas.Children.Add(btn);
            Canvas.SetLeft(btn, pos.X);
            Canvas.SetTop(btn, pos.Y);
        }

        #endregion
    }

    public class PageList(Frame page) : INotifyPropertyChanged
    {
        public Frame Page { get; } = page;
        public async Task GetThumbnailAsync()
        {
            RegistryViewer.AuthenticateIfNeeded(Page.Provider, nameof(Provider.GetFrame));
            Thumbnail = (await Page.Provider.GetFrame(Page, Scale.Thumbnail, null)).ToBitmap();
            OnPropertyChanged(nameof(Thumbnail));
        }
        public Bitmap? Thumbnail { get; private set; }

        public int Number => Page.FrameNumber;
        public string Notes => Page.Notes?.Split('\n').FirstOrDefault() ?? "";

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
