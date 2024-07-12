using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using DiscordRPC;
using DynamicData;
using FluentAvalonia.UI.Controls;
using GeneaGrab.Core.Models;
using GeneaGrab.Services;
using GeneaGrab.Views.Components;

namespace GeneaGrab.Views;

public partial class ProviderList : Page, ITabPage
{
    public Symbol IconSource => Symbol.World;
    public string? DynaTabHeader => null;
    public string? Identifier => null;
    public Task GetRichPresenceAsync(RichPresence richPresence) => Task.CompletedTask;

    public ProviderList()
    {
        InitializeComponent();
        DataContext = this;


        Providers.Clear();
        Providers.Add(Data.Providers.Values);
    }

    public ObservableCollection<Provider> Providers { get; } = new(Data.Providers.Values);
    protected void ProvidersList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count < 1) return;
        if (sender is ListBox listBox) listBox.UnselectAll();

        var provider = e.AddedItems[0] as Provider;
        NavigationService.Navigate(typeof(RegistriesPage), provider);
    }
}
