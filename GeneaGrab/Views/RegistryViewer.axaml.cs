using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DiscordRPC;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Models.Indexing;
using GeneaGrab.Services;
using GeneaGrab.Views.Components;
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

        RecordList.SelectionChanged += (_, _) =>
        {
            if (RecordList.SelectedItem is Record { Position: not null } record)
                ImagePanel.MoveToImageCoordinates(record.Position.Value.Center);
        };
        Records.CollectionChanged += (_, _) => DisplayIndex();
    }

    private static async Task SaveAsync<T>(T entity, string? property = null)
    {
        if (entity is null) return;
        await using var db = new DatabaseContext();
        if (property == null) db.Update(entity);
        else db.Entry(entity).Property(property).IsModified = true;
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

        Frame? frame = null;
        if (info.FrameArkUrl != null) frame = Registry.Frames.FirstOrDefault(f => f.ArkUrl == info.FrameArkUrl);
        if (info.PageNumber.HasValue) frame ??= Registry.Frames.FirstOrDefault(f => f.FrameNumber == info.PageNumber);
        Frame = frame ?? Registry.Frames.FirstOrDefault();
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
        ReloadRecords();
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
        if (page != null) FileExplorer.OpenFolderAndSelectItem(page);
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

    private AvaloniaList<Record> Records { get; } = [];

    private bool HasRecords { get; set; }

    private void AddIndex(Rect position)
    {
        if (Registry == null || Frame == null) return;
        using var db = new DatabaseContext();
        var record = new Record(Registry, Frame)
        {
            Position = position
        };
        db.Records.Add(record);
        db.SaveChanges();
        Records.Add(record);
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
        Records.Remove(record);
    }

    private void ReloadRecords()
    {
        if (Registry == null || Frame == null) return;
        using var db = new DatabaseContext();
        Records.Clear();
        Records.AddRange(db.Records
            .Where(r => r.ProviderId == Registry.ProviderId && r.RegistryId == Registry.Id && r.FrameNumber == Frame.FrameNumber)
            .Include(r => r.Persons)
            .AsEnumerable()
            .OrderBy(r => r.PageNumber, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ThenBy(r => r.SequenceNumber, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ThenBy(r => r.Position?.Y)
            .ThenBy(r => r.Position?.X)
            .ToList());
    }

    private void DisplayIndex()
    {
        HasRecords = Records.Count > 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRecords)));

        ImageCanvas.Children.Clear();
        foreach (var index in Records) DisplayIndexRectangle(index);
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
            Background = new SolidColorBrush(color.Value, .1d),
            BorderBrush = new SolidColorBrush(color.Value),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5)
        };
        rectangle.Styles.Add(new Style { Setters = { new Setter(OpacityProperty, .4d) } });
        rectangle.Styles.Add(new Style(x => x.Class(":pointerover")) { Setters = { new Setter(OpacityProperty, .7d) } });
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

    private void AddPersonRecord(object? _, RoutedEventArgs routedEventArgs) => AddPersonRecord((routedEventArgs.Source as StyledElement)?.DataContext as Record);
    private static void AddPersonRecord(Record? record)
    {
        if (record == null) return;
        using var db = new DatabaseContext();
        var person = new Person
        {
            RecordId = record.Id
        };
        db.Persons.Add(person);
        db.SaveChanges();
        record.Persons.Add(person);
    }

    private void SelectParentListBoxItem(object? sender, GotFocusEventArgs _)
    {
        if (sender is Visual visual && visual.FindAncestorOfType<ListBox>() is { } lb) // Try to find an ancestor of type ListBox
            lb.SelectedItem = visual.DataContext; // Set the selected item of our ListBox to the DataContext of our Visual
    }

    public Func<string, string?, Task<IEnumerable<object>>> RecordFieldSuggestions => (field, search) => GetRecordFieldSuggestionsAsync(SelectFieldExpression<Record, string?>(field), search);
    public Func<string, string?, Task<IEnumerable<object>>> PersonFieldSuggestions => (field, search) => GetPersonRecordFieldSuggestionsAsync(SelectFieldExpression<Person, string?>(field), search);

    private async Task<IEnumerable<object>> GetRecordFieldSuggestionsAsync(Expression<Func<Record, string?>> selector, string? search)
    {
        if (Registry is null) return [];
        await using var db = new DatabaseContext();
        var suggestions = await SearchInFieldAsync(db.Records.Where(r => r.ProviderId == Registry.ProviderId && r.RegistryId == Registry.Id), selector, search ?? string.Empty);
        return suggestions;
    }

    private async Task<IEnumerable<object>> GetPersonRecordFieldSuggestionsAsync(Expression<Func<Person, string?>> selector, string? search)
    {
        if (Registry is null) return [];
        await using var db = new DatabaseContext();
        var suggestions = await SearchInFieldAsync(db.Records
            .Where(r => r.ProviderId == Registry.ProviderId && r.RegistryId == Registry.Id)
            .Include(r => r.Persons)
            .SelectMany(r => r.Persons), selector, search ?? string.Empty);
        return suggestions;
    }

    private static Task<string[]> SearchInFieldAsync<T>(IQueryable<T> query, Expression<Func<T, string?>> selector, string search)
        => query.Select(selector)
            .Where(str => str != null && str.StartsWith(search))
            .Cast<string>()
            .Distinct()
            .Take(10)
            .ToArrayAsync();

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    private static Expression<Func<T, TR>> SelectFieldExpression<T, TR>(string field)
    {
        var parameter = Expression.Parameter(typeof(T), "e"); // Name 'e' the entry of type T
        var property = Expression.PropertyOrField(parameter, field); // The column 'field' of 'e'
        return Expression.Lambda<Func<T, TR>>(property, parameter); // e => e.field
    }

    private void SavePersonRecordField(object? _, DataGridCellEditEndedEventArgs e)
    {
        if (e.Row.DataContext is Person person) SaveAsync(person).Forget();
    }
    private void SaveRecordField(object? _, TextChangedEventArgs e)
    {
        if (e.Source is Control { DataContext: Record record })
            SaveAsync(record).Forget();
    }
    private void SaveRecordField(object? _, SelectionChangedEventArgs e)
    {
        if (e.Source is Control { DataContext: Record record })
            SaveAsync(record).Forget();
    }

    private void PersonRecordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e is not { Key: Key.Delete, KeyModifiers: KeyModifiers.None }) return;
        if (sender is not DataGrid { SelectedItem: Person person, DataContext: Record record }) return;
        using var db = new DatabaseContext();
        db.Persons.Remove(person);
        db.SaveChanges();
        record.Persons.Remove(person);
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
