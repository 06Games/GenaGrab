using System.Linq;
using Avalonia;
using Avalonia.Styling;
using FluentAvalonia.Styling;

namespace GeneaGrab.Services;

public enum Theme { System, Light, Dark, HighContrast }
public static class ThemeSelectorService
{
    public static Theme Theme { get; private set; } = Theme.System;

    public static void Initialize()
    {
        Theme = SettingsService.SettingsData.Theme;
        SetRequestedTheme();
    }

    public static void SetTheme(Theme theme)
    {
        Theme = theme;

        SetRequestedTheme();
        SaveThemeInSettings(Theme);
    }

    private static void SetRequestedTheme()
    {
        if (Application.Current == null) return;
        var faTheme = Application.Current.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
        if (faTheme == null) return;

        Application.Current.RequestedThemeVariant = Theme switch
        {
            Theme.Light => ThemeVariant.Light,
            Theme.Dark => ThemeVariant.Dark,
            Theme.HighContrast => FluentAvaloniaTheme.HighContrastTheme,
            _ => null
        };
        faTheme.PreferSystemTheme = Theme == Theme.System;
    }

    private static void SaveThemeInSettings(Theme theme) => SettingsService.SettingsData.Theme = theme;
}
