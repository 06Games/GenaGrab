﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
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
using NaturalSort.Extension;
using Button = DiscordRPC.Button;
using Frame = GeneaGrab.Core.Models.Frame;

namespace GeneaGrab.Views;

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
    public async Task GetRichPresenceAsync(RichPresence richPresence)
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



    private Control? draggedRectangle;

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

        SideNav.SelectionChanged += (_, _) =>
        {
            var tag = SideNav.SelectedItem is NavigationViewItem item ? item.Tag : null;
            foreach (var child in SideContent.Children)
                child.IsVisible = child.Tag == tag;
        };
        SideNav.SelectedItem = SideNav.MenuItems.FirstOrDefault();
        BottomNav.SelectionChanged += (_, _) =>
        {
            var tag = BottomNav.SelectedItem is NavigationViewItem item ? item.Tag : null;
            foreach (var child in BottomContent.Children)
                child.IsVisible = child.Tag == tag;
        };
        BottomNav.SelectedItem = BottomNav.MenuItems.FirstOrDefault();

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

        ImagePanel.Dragging += dragProperties =>
        {
            if (dragProperties is not { PressedButton: MouseButton.Left, KeyModifiers: KeyModifiers.Shift }) return;
            if (draggedRectangle == null) draggedRectangle = DrawRectangle(dragProperties.Area);
            else UpdateRectangle(draggedRectangle, dragProperties.Area);
        };
        ImagePanel.DraggingStopped += dragProperties =>
        {
            RemoveRectangle(draggedRectangle);
            draggedRectangle = null;
            Rect area;
            if (dragProperties is { PressedButton: MouseButton.Left, KeyModifiers: KeyModifiers.Shift } && (area = dragProperties.Area) is { Width: > 20, Height: > 20 })
                AddIndex(area);
        };

        RecordList.SelectionChanged += (s, e) =>
        {
            if (RecordList.SelectedItem is Record { Position: not null } record)
                ImagePanel.MoveToImageCoordinates(record.Position.Value.Center);
        };
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
            var viewer = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                NavigationService.OpenTab(tab);
                return NavigationService.Frame?.Content as RegistryViewer;
            });

            if (viewer == null) return;
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
        if (parameter is not RegistryInfo and not Uri) return (Registry != null && Frame != null, true);

        var info = parameter as RegistryInfo;
        var uri = parameter as Uri;
        if (info == null && uri != null)
            foreach (var p in Data.Providers.Values)
                if ((info = await p.GetRegistryFromUrlAsync(uri)) != null)
                    break;
        if (info == null) return (false, false);

        await using var db = new DatabaseContext();
        var registry = await db.Registries.Where(r => r.ProviderId == info.ProviderId && r.Id == info.RegistryId).Include(r => r.Frames).FirstOrDefaultAsync();
        if (registry is null && uri != null)
        {
            var provider = Data.Providers[info.ProviderId];
            AuthenticateIfNeeded(provider, nameof(Provider.Infos));
            var data = await provider.Infos(uri);
            registry = data.registry;
            db.Registries.Add(registry);
            await db.SaveChangesAsync();
        }
        Registry = registry;
        if (Registry == null) return (false, false);

        if (info.FrameArkUrl != null) Frame = Registry.Frames.FirstOrDefault(f => f.ArkUrl == info.FrameArkUrl);
        if (info.PageNumber.HasValue) Frame ??= Registry.Frames.FirstOrDefault(f => f.FrameNumber == info.PageNumber);
        Frame ??= Registry.Frames.FirstOrDefault();
        return (Frame != null, false);
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
        await Dispatcher.UIThread.InvokeAsync(() => RefreshView(image));
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
        Provider.GetFrame(Frame, Scale.Full, TrackProgress).ContinueWith(t =>
        {
            var frame = t.Result;
            return Dispatcher.UIThread.InvokeAsync(() => RefreshView(frame));
        }, TaskScheduler.Current).Forget();
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

    private bool HasRecords { get; set; }

    private void AddIndex(Rect position)
    {
        if (Registry == null || Frame == null) return;
        using var db = new DatabaseContext();
        db.Records.Add(new Record(Registry, Frame)
        {
            Position = position
        });
        db.SaveChanges();
        DisplayIndex();
    }

    private void RemoveIndex(object _, RoutedEventArgs e)
    {
        if (e.Source is Control { DataContext: Record record })
            RemoveIndex(record);
    }
    private void RemoveIndex(Record record)
    {
        if (Registry == null || Frame == null) return;
        using var db = new DatabaseContext();
        db.Records.Remove(record);
        db.SaveChanges();
        DisplayIndex();
    }

    private void DisplayIndex()
    {
        if (Registry == null || Frame == null) return;
        ImageCanvas.Children.Clear();

        using var db = new DatabaseContext();
        var indexes = db.Records
            .Where(r => r.ProviderId == Registry.ProviderId && r.RegistryId == Registry.Id && r.FrameNumber == Frame.FrameNumber)
            .Include(r => r.Persons)
            .AsEnumerable()
            .OrderBy(r => r.PageNumber, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ThenBy(r => r.SequenceNumber, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ThenBy(r => r.Position?.Y)
            .ThenBy(r => r.Position?.X)
            .ToList();
        RecordList.ItemsSource = indexes;
        HasRecords = indexes.Count > 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRecords)));
        foreach (var index in indexes)
            DisplayIndexRectangle(index);
    }
    private void DisplayIndexRectangle(Record? index)
    {
        if (index?.Position is null) return;
        var btn = DrawRectangle(index.Position.Value);
        var tt = new ToolTip { Content = index.ToString() };
        ToolTip.SetTip(btn, tt);

        btn.PointerPressed += (_, e) =>
        {
            var properties = new DragProperties.Keys(e);
            switch (properties)
            {
                case { PressedButton: MouseButton.Right, KeyModifiers: KeyModifiers.Shift, ClickCount: 1 }:
                    RemoveIndex(index);
                    break;
                case { PressedButton: MouseButton.Left, KeyModifiers: KeyModifiers.None, ClickCount: 2 }:
                    RecordList.SelectedItem = index;
                    break;
            }
        };
        btn.BindClass("selected", RecordList.GetBindingObservable(SelectingItemsControl.SelectedItemProperty).Select(item => item == index).ToBinding(), null!);
    }

    private Border DrawRectangle(Rect rect, Color? color = null)
    {
        color ??= Colors.RoyalBlue;
        var rectangle = new Border
        {
            Background = new SolidColorBrush(color.Value, .1),
            BorderBrush = new SolidColorBrush(color.Value),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };
        rectangle.Styles.Add(new Style { Setters = { new Setter(OpacityProperty, .4d) } });
        rectangle.Styles.Add(new Style(x => x.Class(":pointerover")) { Setters = { new Setter(OpacityProperty, .8d) } });
        rectangle.Styles.Add(new Style(x => x.Class("selected")) { Setters = { new Setter(OpacityProperty, 1d) } });

        ImageCanvas.Children.Add(rectangle);
        UpdateRectangle(rectangle, rect);
        return rectangle;
    }

    private static void UpdateRectangle(Control rectangle, Rect rect)
    {
        rectangle.Width = rect.Width;
        rectangle.Height = rect.Height;
        Canvas.SetLeft(rectangle, rect.X);
        Canvas.SetTop(rectangle, rect.Y);
    }

    private void RemoveRectangle(Control? rectangle)
    {
        if (rectangle != null) ImageCanvas.Children.Remove(rectangle);
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
