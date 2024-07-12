using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DiscordRPC;
using DynamicData;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Services;
using GeneaGrab.Views.Components;

namespace GeneaGrab.Views;

public partial class RegistriesPage : Page, ITabPage
{
    public Symbol IconSource => Symbol.Library;
    public string? DynaTabHeader => ResourceExtensions.GetLocalized($"Provider.{Provider?.Id}") ?? Provider?.Id;
    public string? Identifier => Provider?.Id;
    public Task GetRichPresenceAsync(RichPresence richPresence) => Task.CompletedTask;
    private Provider? Provider { get; set; }

    public RegistriesPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected ObservableCollection<RegistriesTreeStructure> Items { get; } = [];

    public override void OnNavigatedTo(NavigationEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.Parameter is not Provider provider) return;
        Provider = provider;
        MainWindow.UpdateSelectedTitle();

        Items.Clear();

        using var db = new DatabaseContext();
        foreach (var registry in db.Registries.Where(r => r.ProviderId == Provider.Id).ToList())
        {
            var parent = Items;
            foreach (var location in registry.Location)
            {
                if (string.IsNullOrWhiteSpace(location)) continue;
                var container = parent.FirstOrDefault(c => c.Title == location);
                if (container is null)
                {
                    container = new RegistriesTreeStructure(location);
                    InsertInPlace(parent, container);
                }
                parent = container.Children;
            }
            InsertInPlace(parent, registry);
        }

        void InsertInPlace<T>(IList<T> list, T item) where T : IComparable<T>
        {
            var index = list.BinarySearch(item);
            if (index < 0) index = ~index;
            list.Insert(index, item);
        }
    }

    private void RegisterList_ItemInvoked(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count < 1 || sender is not TreeView treeView || e.AddedItems[0] is not RegistriesTreeStructure data) return;
        treeView.UnselectAll();
        if (treeView.TreeContainerFromItem(data) is not TreeViewItem node) return;
        if (data.Children.Any()) node.IsExpanded = !node.IsExpanded;
        else if (data.Registry != null) NavigationService.Navigate(typeof(RegistryViewer), new RegistryInfo(data.Registry));
    }
}
public class RegistriesTreeStructure(string title, string? subtitle = null) : IComparable<RegistriesTreeStructure>
{
    public string Title { get; } = title;
    public string? Subtitle { get; } = subtitle;
    public ObservableCollection<RegistriesTreeStructure> Children { get; } = [];

    public Registry? Registry { get; }

    private RegistriesTreeStructure(Registry registry) : this(registry.GetDescription(), registry.CallNumber)
    {
        Registry = registry;
    }

    public static implicit operator RegistriesTreeStructure(Registry registry) => new(registry);

    public int CompareTo(RegistriesTreeStructure? other)
    {
        if (other is null) return 1;

        var compare = 0;
        if (Registry?.From != null && other.Registry?.From != null) compare += Registry.From.CompareTo(other.Registry.From) * 10;
        else if (Registry == null && other.Registry != null) compare += -10;
        else if (Registry != null && other.Registry == null) compare += 10;
        return compare + string.Compare(Title, other.Title, StringComparison.CurrentCulture);
    }
    public override bool Equals(object? obj)
    {
        if (obj is not RegistriesTreeStructure other) return false;
        return GetHashCode() == other.GetHashCode();
    }
    public override int GetHashCode() => HashCode.Combine(Title, Subtitle, Children, Registry);
    public static bool operator ==(RegistriesTreeStructure? left, RegistriesTreeStructure? right) => left?.Equals(right) ?? false;
    public static bool operator !=(RegistriesTreeStructure? left, RegistriesTreeStructure? right) => !(left == right);
    public static bool operator >(RegistriesTreeStructure left, RegistriesTreeStructure right) => left.CompareTo(right) > 0;
    public static bool operator <(RegistriesTreeStructure left, RegistriesTreeStructure right) => left.CompareTo(right) < 0;
    public static bool operator >=(RegistriesTreeStructure left, RegistriesTreeStructure right) => left.CompareTo(right) >= 0;
    public static bool operator <=(RegistriesTreeStructure left, RegistriesTreeStructure right) => left.CompareTo(right) <= 0;
}
