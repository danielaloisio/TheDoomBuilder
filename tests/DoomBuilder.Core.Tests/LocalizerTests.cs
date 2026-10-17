using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder.Localization;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>The texts of the editor in the user's language: English is the key, a missing translation leaves the English text.</summary>
[Collection("General static state")]
public class LocalizerTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-lang-" + Guid.NewGuid().ToString("N"));

    public LocalizerTests() { Directory.CreateDirectory(Path.Combine(dir, Localizer.FolderName)); }
    public void Dispose() { Localizer.Reset(); Directory.Delete(dir, true); }

    private void Write(string code, string json) => File.WriteAllText(Path.Combine(dir, Localizer.FolderName, code + ".json"), json);

    [Fact]
    public void A_translation_replaces_the_english_text_and_the_rest_stays_english()
    {
        Write("xx", "{ \"language\": \"Xish\", \"strings\": { \"&File\": \"&Fyle\", \"Hello {0}\": \"Hola {0}\" } }");
        Assert.Equal("xx", Localizer.Load(dir, "xx"));
        Assert.Equal("&Fyle", Localizer.T("&File"));
        Assert.Equal("&Edit", Localizer.T("&Edit"));                 // no translation: the same text
        Assert.Equal("Hola you", Localizer.T("Hello {0}", "you"));
        Assert.Equal("Bye you", Localizer.T("Bye {0}", "you"));
        Assert.Null(Localizer.T(null));
    }

    [Fact]
    public void A_message_built_by_concatenation_matches_a_text_with_values_and_the_values_are_translated_too()
    {
        Write("xx", "{ \"language\": \"Xish\", \"strings\": { \"Deleted {0} linedefs.\": \"Borrado {0} lineas.\", \"Edit {0}\": \"Editar {0}\", " +
                    "\"{0} undone.\": \"{0} deshecho.\", \"Snap to grid is {0}\": \"Rejilla: {0}\", \"ENABLED\": \"ACTIVA\", \"linedef\": \"linea\", " +
                    "\"Changed index of {oldindex} to {newindex}.\": \"Indice {oldindex} a {newindex}.\" } }");
        Localizer.Load(dir, "xx");

        Assert.Equal("Borrado 12 lineas.", Localizer.T("Deleted 12 linedefs."));
        Assert.Equal("Editar linea", Localizer.T("Edit linedef"));             // the value is a text of its own
        Assert.Equal("Editar linea deshecho.", Localizer.T("Edit linedef undone."));   // value inside a value: "{0} undone." then "Edit {0}"
        Assert.Equal("Rejilla: ACTIVA", Localizer.T("Snap to grid is ENABLED"));
        Assert.Equal("Indice 3 a 7.", Localizer.T("Changed index of 3 to 7."));  // named holes (interpolated strings)
        Assert.Equal("Something else", Localizer.T("Something else"));
        Assert.Equal("Deleted 12 linedefs.", Localizer.Exact("Deleted 12 linedefs.") ?? "Deleted 12 linedefs.");
    }

    [Fact]
    public void Exact_only_finds_texts_with_an_entry_and_ignores_the_access_key_style()
    {
        Write("xx", "{ \"language\": \"Xish\", \"strings\": { \"&Select\": \"&Selecta\", \"Edit {0}\": \"Editar {0}\", \"Name:\": \"Nombre:\" } }");
        Localizer.Load(dir, "xx");

        Assert.Equal("Nombre:", Localizer.Exact("Name:"));
        Assert.Equal("_Selecta", Localizer.Exact("_Select"));        // Avalonia style
        Assert.Equal("&Selecta", Localizer.Exact("&Select"));
        Assert.Null(Localizer.Exact("Edit foo"));                    // templates are not for user data
        Assert.Null(Localizer.Exact("Other"));
        Assert.Equal("Editar foo", Localizer.T("Edit foo"));
    }

    [Fact]
    public void The_shipped_portuguese_file_keeps_the_values_of_every_text_with_holes()
    {
        string file = Path.Combine(AppContext.BaseDirectory, Localizer.FolderName, "pt-BR.json");
        if (!File.Exists(file)) file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "Common", Localizer.FolderName, "pt-BR.json"));
        Assert.True(File.Exists(file), file);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
        var holes = new System.Text.RegularExpressions.Regex(@"\{\w+\}");
        foreach (var p in doc.RootElement.GetProperty("strings").EnumerateObject())
        {
            var want = holes.Matches(p.Name).Select(m => m.Value).OrderBy(x => x).ToList();
            var got = holes.Matches(p.Value.GetString()).Select(m => m.Value).OrderBy(x => x).ToList();
            Assert.True(want.SequenceEqual(got), "values differ in: " + p.Name);
        }
    }

    [Fact]
    public void The_language_is_chosen_by_code_then_by_the_language_without_the_country()
    {
        Write("pt-BR", "{ \"language\": \"Português (Brasil)\", \"strings\": { \"Save\": \"Salvar\" } }");
        Write("es", "{ \"language\": \"Español\", \"strings\": { \"Save\": \"Guardar\" } }");

        Assert.Equal("pt-BR", Localizer.Load(dir, "pt-BR"));
        Assert.Equal("pt-BR", Localizer.Load(dir, "pt-PT"));         // any pt-* file when the exact one is not there
        Assert.Equal("es", Localizer.Load(dir, "es-MX"));            // the neutral file
        Assert.Equal("Guardar", Localizer.T("Save"));
        Assert.Equal("en", Localizer.Load(dir, "de-DE"));            // nothing fits: English
        Assert.Equal("Save", Localizer.T("Save"));
        Assert.Equal("en", Localizer.Load(dir, "en-US"));
    }

    [Fact]
    public void The_available_languages_are_listed_with_their_own_names_and_a_broken_file_is_left_out()
    {
        Write("pt-BR", "{ \"language\": \"Português (Brasil)\", \"strings\": {} }");
        Write("broken", "this is not json");
        var found = Localizer.Available(dir);
        Assert.Single(found);
        Assert.Equal("pt-BR", found[0].Code);
        Assert.Equal("Português (Brasil)", found[0].Name);
    }

    [Fact]
    public void A_language_file_that_cannot_be_read_leaves_english()
    {
        Write("zz", "{ \"language\": \"Zed\", \"strings\": [ 1, 2 ] }");
        Assert.Equal("en", Localizer.Load(dir, "zz"));               // "strings" is not a table: the file is not used
        Assert.Equal("Open", Localizer.T("Open"));
    }

    [Fact]
    public void The_portuguese_file_that_ships_with_the_editor_translates_the_menus()
    {
        string app = Path.GetDirectoryName(TestAssets.DefaultSettings);
        Assert.Contains(Localizer.Available(app), l => l.Code == "pt-BR");
        Localizer.Load(app, "pt-BR");
        Assert.Equal("&Arquivo", Localizer.T("&File"));
        Assert.Equal("Preferências...", Localizer.T("Preferences..."));
        Assert.Equal("Deseja salvar as alterações em test.acs?", Localizer.T("Do you want to save changes to {0}?", "test.acs"));
    }

    [Fact]
    public void Every_accelerator_of_a_translated_menu_text_is_kept_and_placeholders_match()
    {
        string app = Path.GetDirectoryName(TestAssets.DefaultSettings);
        string file = Localizer.Available(app).First(l => l.Code == "pt-BR").File;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
        foreach (var p in doc.RootElement.GetProperty("strings").EnumerateObject())
        {
            string translated = p.Value.GetString();
            Assert.True(p.Name.Contains('&') == translated.Contains('&'), "the access key of \"" + p.Name + "\"");
            Assert.True(p.Name.Contains("{0}") == translated.Contains("{0}"), "the placeholder of \"" + p.Name + "\"");
        }
    }
}
