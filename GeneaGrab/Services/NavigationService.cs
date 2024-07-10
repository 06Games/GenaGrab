using System;
using System.Collections;
using Avalonia.Controls;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;

namespace GeneaGrab.Services;

public static class NavigationService
{
    public static event SelectionChangedEventHandler? SelectionChanged;
    public static event Action<TabViewItem>? TabAdded;
    public static event Action<TabViewItem>? TabRemoved;

    public static event NavigatedEventHandler? Navigated;
    public static event NavigationFailedEventHandler? NavigationFailed;

    private static TabView? _tabView;
    private static Frame? _frame;
    private static object? lastParamUsed;

    public static TabView? TabView
    {
        get => _tabView;
        set
        {
            UnregisterTabViewEvents();
            _tabView = value;
            RegisterTabViewEvents();
        }
    }
    private static void RegisterTabViewEvents()
    {
        if (_tabView is null) return;
        _tabView.SelectionChanged += TabView_SelectionChanged;
    }
    private static void UnregisterTabViewEvents()
    {
        if (_tabView is null) return;
        _tabView.SelectionChanged -= TabView_SelectionChanged;
    }
    private static void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TabOpenned();
        SelectionChanged?.Invoke(sender, e);
    }
    private static void TabOpenned()
    {
        if (TabView?.SelectedItem is TabViewItem tab) Frame = tab.Content as Frame;
    }
    public static int TabCount => TabView?.TabItems.Count() ?? 0;

    public static TabViewItem NewTab<T>(object? parameter = null) where T : UserControl => NewTab(typeof(T), parameter);
    public static TabViewItem NewTab(Type page, object? parameter = null)
    {
        var frame = new Frame();
        var tab = new TabViewItem { Content = frame, Header = page.Name };
        Frame = frame;

        if (TabView != null)
        {
            (TabView.TabItems as IList)?.Add(tab);
            TabView.SelectedItem = tab;
        }
        Frame.Navigate(page, parameter);
        TabAdded?.Invoke(tab);
        return tab;
    }
    public static bool CloseTab()
    {
        if (TabView?.SelectedItem is TabViewItem tab) return CloseTab(tab);
        return false;
    }
    public static bool CloseTab(TabViewItem tab)
    {
        if (!tab.IsClosable || TabView == null) return false;
        (TabView.TabItems as IList)?.Remove(tab);
        TabRemoved?.Invoke(tab);
        return true;
    }
    public static TabViewItem? CurrentTab => TabView?.SelectedItem as TabViewItem;
    public static TabViewItem OpenTab(TabViewItem tab)
    {
        if (TabView != null) TabView.SelectedItem = tab;
        TabOpenned();
        return tab;
    }
    public static bool TryGetTabWithId(string? id, out TabViewItem? tab)
    {
        tab = null;
        if (id is null) return false;

        foreach (var item in TabView?.TabItems ?? Array.Empty<object>())
            if (item is TabViewItem t && t.Tag as string == id)
            {
                tab = t;
                return true;
            }
        return false;
    }



    public static Frame? Frame
    {
        get => _frame;
        private set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }
    private static void RegisterFrameEvents()
    {
        if (_frame == null) return;
        _frame.Navigated += Frame_Navigated;
        _frame.NavigationFailed += Frame_NavigationFailed;
    }
    private static void UnregisterFrameEvents()
    {
        if (_frame == null) return;
        _frame.Navigated -= Frame_Navigated;
        _frame.NavigationFailed -= Frame_NavigationFailed;
    }
    private static void Frame_NavigationFailed(object sender, NavigationFailedEventArgs e) => NavigationFailed?.Invoke(sender, e);
    private static void Frame_Navigated(object sender, NavigationEventArgs e) => Navigated?.Invoke(sender, e);


    public static bool Navigate<T>(object? parameter = null, NavigationTransitionInfo? infoOverride = null) where T : UserControl => Navigate(typeof(T), parameter, infoOverride);
    public static bool Navigate(Type pageType, object? parameter = null, NavigationTransitionInfo? infoOverride = null)
    {
        if (pageType == null || !pageType.IsSubclassOf(typeof(UserControl))) throw new ArgumentException($@"Invalid pageType '{pageType}', please provide a valid pageType.", nameof(pageType));
        if (Frame?.Content?.GetType() == pageType && (parameter == null || parameter.Equals(lastParamUsed))) return false; // Don't open the same page multiple times

        var navigationResult = Frame?.Navigate(pageType, parameter, infoOverride) ?? false;
        if (navigationResult) lastParamUsed = parameter;
        return navigationResult;
    }

    public static bool CanGoBack => Frame?.CanGoBack ?? false;
    public static bool GoBack()
    {
        if (!CanGoBack) return false;
        Frame?.GoBack();
        return true;
    }
    public static bool CanGoForward => Frame?.CanGoForward ?? false;
    public static bool GoForward()
    {
        if (!CanGoForward) return false;
        Frame?.GoForward();
        return true;
    }
}
