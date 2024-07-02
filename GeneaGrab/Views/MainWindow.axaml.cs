using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DiscordRPC;
using FluentAvalonia.UI.Controls;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Services;
using Microsoft.VisualStudio.Threading;
using Serilog;
using Frame = FluentAvalonia.UI.Controls.Frame;

namespace GeneaGrab.Views;

public interface ITabPage
{
    Symbol IconSource { get; }
    string? DynaTabHeader { get; }
    string? Identifier { get; }
    Task RichPresence(RichPresence richPresence);
}
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public GridLength WindowsTitleBarWidth => new(IsWindows ? 150 : 15);

    public MainWindow()
    {
        if (IsWindows)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = -1;
        }
        else if (IsMacOS) ExtendClientAreaToDecorationsHint = true;

        InitializeComponent();
        DataContext = this;
        Initialize();
    }

    public bool IsBackEnabled => NavigationService.CanGoBack;
    public bool IsForwardEnabled => NavigationService.CanGoForward;

    private void Initialize()
    {
        NavigationService.TabView = TabView;

        Dispatcher.UIThread.Post(() =>
        {
            if (NavigationService.TabView.GetVisualDescendants().FirstOrDefault(d => d.Name == "TabContainerGrid") is not InputElement titlebar) return;
            titlebar.PointerMoved += InputElement_OnPointerMoved;
            titlebar.PointerPressed += InputElement_OnPointerPressed;
            titlebar.PointerReleased += InputElement_OnPointerReleased;
        });

        NavigationService.Navigated += (_, e) =>
        {
            FrameChanged();
            if (e.Content is Page page) page.OnNavigatedTo(e);
        };
        NavigationService.NavigationFailed += (_, e) => throw e.Exception;
        NavigationService.TabAdded += UpdateTitle;
        NavigationService.TabRemoved += _ =>
        {
            if (NavigationService.TabCount <= 1) NewTab();
        };
        NavigationService.SelectionChanged += (_, _) => FrameChanged();

        NavigationService.NewTab(typeof(SettingsPage)).IsClosable = false;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Startup += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (NavigationService.TabCount <= 1) NavigationService.OpenTab(NewTab());
            });
        else NavigationService.OpenTab(NewTab());
    }

    private TabViewItem NewTab() => NavigationService.NewTab(typeof(ProviderList));

    /// <summary>Add a new Tab to the TabView</summary>
    protected void AddTab(TabView _, EventArgs _1) => NewTab();
    /// <summary>Remove the requested tab from the TabView</summary>
    protected void CloseTab(TabView _, TabViewTabCloseRequestedEventArgs args) => NavigationService.CloseTab(args.Tab);
    protected void GoBack(object _, RoutedEventArgs _1) => NavigationService.GoBack();
    protected void GoForward(object _, RoutedEventArgs _1) => NavigationService.GoForward();

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void FrameChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBackEnabled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsForwardEnabled)));
        UpdateSelectedTitle();
    }
    public static void UpdateSelectedTitle() => UpdateTitle(NavigationService.TabView?.SelectedItem as TabViewItem);
    private static void UpdateTitle(TabViewItem? tab)
    {
        if (tab is null) return;
        var frame = tab.Content as Frame;
        var frameData = frame?.Content as ITabPage;
        var name = frameData?.DynaTabHeader?.Split('\n');
        var defaultName = ResourceExtensions.GetLocalized($"Tab.{frame?.SourcePageType?.Name}", ResourceExtensions.Resource.UI) ?? frame?.SourcePageType?.Name;

        tab.IconSource = frameData is null ? null : new SymbolIconSource { Symbol = frameData.IconSource };
        tab.Header = name == null ? defaultName : string.Join(" - ", name);
        tab.Tag = frameData?.Identifier;

        Task.Run(async () =>
        {
            var rp = new RichPresence
            {
                Details = name is not { Length: > 0 } ? null : name[0][..Math.Min(name[0].Length, 96)],
                State = name is not { Length: > 1 } ? null : name[1][..Math.Min(name[1].Length, 96)],
                Assets = new Assets
                {
                    LargeImageKey = "logo",
                    SmallImageKey = frameData is null ? null : Enum.GetName(frameData.IconSource)?.ToLower(),
                    SmallImageText = defaultName?[..Math.Min(defaultName.Length, 96)]
                }
            };
            if (frameData != null) await frameData.RichPresence(rp);
            (Application.Current as App)?.Discord.SetPresence(rp);
        }).ContinueWith(t =>
        {
            Log.Warning(t.Exception, "Couldn't update Discord RPC");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default).Forget();
    }


    #region Search

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static async Task<IEnumerable<object>> PopulateAsync(string? query, CancellationToken _)
    {
        var searchResults = new List<Result>();
        if (string.IsNullOrWhiteSpace(query)) return searchResults;

        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri)) return searchResults;
        foreach (var (providerName, provider) in Data.Providers)
        {
            var info = await provider.GetRegistryFromUrlAsync(uri);
            if (info == null) continue;
            searchResults.Add(new Result { Text = $"{providerName} ({(info.RegistryId.Length > 18 ? $"{info.RegistryId[..15]}..." : info.RegistryId)})", Value = uri });
        }
        return searchResults;
    }

    private sealed class Result
    {
        public object? Value { get; init; }
        public string? Text { get; init; }
        public override string? ToString() => Text;
    }

    private void RegistrySearch_SuggestionChosen(object? sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count == 0) return;
        if (args.AddedItems[0] is Result result) NavigationService.Navigate(typeof(RegistryViewer), result.Value);
        if (sender is AutoCompleteBox searchBar) Dispatcher.UIThread.Post(() => searchBar.Text = string.Empty);
    }

    #endregion


    #region MoveWindow

    private bool _mouseDownForWindowMoving;
    private PointerPoint _originalPoint;

    private void InputElement_OnPointerMoved(object? _, PointerEventArgs e)
    {
        if (!_mouseDownForWindowMoving) return;

        var currentPoint = e.GetCurrentPoint(this);
        Position = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X), Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
    }

    private void InputElement_OnPointerPressed(object? _, PointerEventArgs e)
    {
        if (WindowState is WindowState.Maximized or WindowState.FullScreen) return;

        _mouseDownForWindowMoving = true;
        _originalPoint = e.GetCurrentPoint(this);
    }

    private void InputElement_OnPointerReleased(object? _, PointerReleasedEventArgs e) => _mouseDownForWindowMoving = false;

    #endregion
}
