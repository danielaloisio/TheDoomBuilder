using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Localization;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.App.Dialogs;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The interface in the language of the settings: the menus, the toolbar and the windows built later.</summary>
public class LanguageTests : EditorTestBase
{
    private static string[] MenuHeaders(MainWindow w) =>
        w.GetVisualDescendants().OfType<AccessText>().Select(a => a.Text).ToArray();

    public override void Dispose()
    {
        Localizer.Reset();
        base.Dispose();
    }

    [AvaloniaFact]
    public void The_menus_follow_the_language_of_the_settings()
    {
        OpenEditor(withMap: false);
        Assert.Contains("_File", MenuHeaders(window));

        General.Settings.Language = "pt-BR";
        window.ApplyLanguage();
        Assert.Equal("pt-BR", Localizer.CurrentLanguage);
        Assert.Contains("_Arquivo", MenuHeaders(window));
        Assert.DoesNotContain("_File", MenuHeaders(window));

        General.Settings.Language = "en";
        window.ApplyLanguage();
        Assert.Contains("_File", MenuHeaders(window));
    }

    [AvaloniaFact]
    public void Windows_built_after_the_change_use_the_language()
    {
        OpenEditor(withMap: false);
        General.Settings.Language = "pt-BR";
        window.ApplyLanguage();

        string title = null;
        WhenShown<PreferencesWindow>(w =>
        {
            title = w.Title;
            Assert.Contains(w.Tabs.Items.OfType<TabItem>(), t => (string)t.Header == "Editor de scripts");
            Click(w.CancelButton);
        });
        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Preferências", title);
    }

    [AvaloniaFact]
    public void The_language_is_a_preference_with_the_languages_found()
    {
        OpenEditor(withMap: false);
        var model = new PreferencesModel();
        var language = model.Find("language");
        Assert.Equal(PreferenceKind.Choice, language.Kind);
        Assert.Contains("Português (Brasil)", language.Choices);
        Assert.Equal("Follow the system", language.Choices[0]);

        string old = General.Settings.Language;
        try
        {
            language.Value = System.Array.IndexOf(language.Choices, "Português (Brasil)");
            model.Apply();
            Assert.Equal("pt-BR", General.Settings.Language);
            language.Value = 0;
            model.Apply();
            Assert.Equal("", General.Settings.Language);
        }
        finally { General.Settings.Language = old; }
    }
}
