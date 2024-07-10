using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordRPC;
using FluentAvalonia.UI.Controls;
using GeneaGrab.Core.Models;
using GeneaGrab.Helpers;
using GeneaGrab.Services;

namespace GeneaGrab.Views;

public partial class SettingsPage : Page, INotifyPropertyChanged, ITabPage
{
    public Symbol IconSource => Symbol.Setting;
    public string? DynaTabHeader => null;
    public string? Identifier => null;
    public Task GetRichPresenceAsync(RichPresence richPresence) => Task.CompletedTask;

    public Theme ElementTheme => ThemeSelectorService.Theme;

    private string? _versionDescription;
    public string? VersionDescription
    {
        get => _versionDescription;
        set => Set(ref _versionDescription, value);
    }
    public Credentials? FamilySearch { get; }

    public SettingsPage()
    {
        ThemeSelectorService.Initialize();
        VersionDescription = GetVersionDescription();
        FamilySearch = SettingsService.SettingsData.Credentials.TryGetValue("FamilySearch", out var credentials) ? credentials : new Credentials();

        InitializeComponent();
        DataContext = this;
    }


    private static string? GetVersionDescription()
    {
        var appName = ResourceExtensions.GetLocalized("AppDisplayName", ResourceExtensions.Resource.UI);
        if (Application.Current is not App package) return null;
        var version = package.Version;

        return $"{appName} - {version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}";
    }

    private void ThemeChanged_Checked(object sender, RoutedEventArgs _)
    {
        if (sender is not RadioButton btn) return;
        var param = btn.CommandParameter;
        if (param != null && btn.IsChecked.GetValueOrDefault()) ThemeSelectorService.SetTheme((Theme)param);
    }

    private void FamilySearch_Changed(object _, TextChangedEventArgs _1)
    {
        if (FamilySearch is null) return;
        SettingsService.SettingsData.Credentials["FamilySearch"] = FamilySearch;
        SettingsService.Save();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return;
        storage = value;
        OnPropertyChanged(propertyName);
    }
    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
