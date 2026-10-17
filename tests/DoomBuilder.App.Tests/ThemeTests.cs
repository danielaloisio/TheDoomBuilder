using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The theme preference (Interface tab): follow the system, light or dark.</summary>
public class ThemeTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_theme_preference_is_saved_in_the_settings_and_sets_the_application_theme()
    {
        OpenEditor(withMap: false);
        int original = General.Settings.Theme;
        try
        {
            var model = new PreferencesModel();
            Preference theme = model.Find("theme");
            Assert.Equal("Interface", theme.Tab);
            Assert.Equal(PreferenceKind.Choice, theme.Kind);

            theme.Value = 2;
            model.Apply();
            Assert.Equal(2, General.Settings.Theme);
            AvaloniaShell.ApplyTheme();
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current.RequestedThemeVariant);

            theme.Value = 1;
            model.Apply();
            AvaloniaShell.ApplyTheme();
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current.RequestedThemeVariant);

            theme.Value = 0;
            model.Apply();
            AvaloniaShell.ApplyTheme();
            Assert.Equal(ThemeVariant.Default, Avalonia.Application.Current.RequestedThemeVariant);
        }
        finally
        {
            var model = new PreferencesModel();
            model.Find("theme").Value = original;
            model.Apply();
            AvaloniaShell.ApplyTheme();
        }
    }
}
