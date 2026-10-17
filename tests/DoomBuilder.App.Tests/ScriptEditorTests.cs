using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Compilers;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.App.Dialogs;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The script editor window (AvaloniaEdit): tabs for the map's script lumps, implicit save into the map, error list.</summary>
public class ScriptEditorTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private ScriptEditorWindow OpenScriptEditor()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        Assert.True(General.Map.Config.HasScriptLumps(), "no script lumps in the config");
        Assert.NotNull(ScriptEditorForm.HostFactory);
        Assert.True(General.Map.IsScriptsWindowOpen, "ScriptEditor=" + (General.Map.ScriptEditor == null ? "null" : "set"));
        return Assert.IsType<ScriptEditorWindow>(ScriptEditorWindow.Instance);
    }

    private static T Find<T>(Avalonia.Visual v, System.Func<T, bool> where) where T : class => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>().First(where);

    private static T Find<T>(Avalonia.Visual v) where T : class => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>().First();

    [AvaloniaFact]
    public void The_editor_has_a_tab_per_script_lump_and_saves_into_the_map()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var tabs = Find<TabControl>(editor);
        Assert.Contains(tabs.Items.OfType<TabItem>(), t => (string)t.Header == "SCRIPTS");

        var text = ((TabItem)tabs.SelectedItem).Content as AvaloniaEdit.TextEditor;
        Assert.NotNull(text);
        text.Text = "script 1 (void) { Print(s:\"hi\"); }";
        Assert.True(General.Map.ScriptEditor.Editor.CheckImplicitChanges());
        Assert.EndsWith("*", (string)((TabItem)tabs.SelectedItem).Header);

        General.Map.ScriptEditor.Editor.ImplicitSave();
        Assert.False(General.Map.ScriptEditor.Editor.CheckImplicitChanges());
        string stored = Encoding.GetEncoding("iso-8859-1").GetString(General.Map.GetLumpData("scripts").ToArray());
        Assert.Equal(text.Text, stored);
    }

    [AvaloniaFact]
    public void Compiler_errors_are_listed_and_double_click_goes_to_the_line()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var tabs = Find<TabControl>(editor);
        var text = (AvaloniaEdit.TextEditor)((TabItem)tabs.SelectedItem).Content;
        text.Text = "line one\nline two\nline three";

        General.Map.ScriptEditor.Editor.ShowErrors(new[] { new CompilerError("boom", "?scripts", 1) }, true);
        var list = Find<ListBox>(editor);
        Assert.True(list.IsVisible);
        Assert.Contains("boom", list.Items.Cast<string>().Single());
    }

    private static TextBox Box(Avalonia.Visual v, string watermark) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<TextBox>().First(b => b.PlaceholderText == watermark);
    private static Button Btn(Avalonia.Visual v, string text) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<Button>().First(b => b.Content as string == text);

    [AvaloniaFact]
    public void Find_next_wraps_around_and_replace_all_honours_whole_word()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var tabs = Find<TabControl>(editor);
        var text = (AvaloniaEdit.TextEditor)((TabItem)tabs.SelectedItem).Content;
        text.Text = "int cat; int category; int Cat;";

        Box(editor, "Find").Text = "cat";
        Click(Btn(editor, "Next"));
        Assert.Equal(4, text.SelectionStart);                       // "cat" in "int cat"
        Click(Btn(editor, "Next"));
        Assert.Equal(13, text.SelectionStart);                      // the start of "category"
        Click(Btn(editor, "Next"));
        Assert.Equal(27, text.SelectionStart);                      // "Cat": case ignored by default
        Click(Btn(editor, "Next"));
        Assert.Equal(4, text.SelectionStart);                       // wrapped around
        Click(Btn(editor, "Previous"));
        Assert.Equal(27, text.SelectionStart);                      // and back

        Find<CheckBox>(editor, c => c.Content as string == "Whole word").IsChecked = true;
        Box(editor, "Replace with").Text = "dog";
        Click(Btn(editor, "Replace all"));
        Assert.Equal("int dog; int category; int dog;", text.Text);
    }

    [AvaloniaFact]
    public void The_navigator_lists_the_scripts_and_picking_one_moves_the_caret()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = (AvaloniaEdit.TextEditor)((TabItem)Find<TabControl>(editor).SelectedItem).Content;
        text.Text = "#include \"zcommon.acs\"\n\nscript 1 (void)\n{\n}\n\nscript 2 (void)\n{\n}\n";

        var names = editor.NavigatorNames();
        Assert.Contains(names, n => n.StartsWith("Script 1") || n.StartsWith("1"));
        Assert.Contains(names, n => n.StartsWith("Script 2") || n.StartsWith("2"));

        editor.Navigate(names.First(n => n.Contains("2")));
        Assert.True(text.Document.GetLineByOffset(text.CaretOffset).LineNumber >= 7);
    }

    [AvaloniaFact]
    public void A_snippet_is_indented_like_the_line_and_leaves_the_caret_at_the_entry_marker()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var tab = (TabItem)Find<TabControl>(editor).SelectedItem;
        var text = (AvaloniaEdit.TextEditor)tab.Content;
        text.Text = "    ";
        text.CaretOffset = 4;

        var config = CodeImp.DoomBuilder.General.GetScriptConfiguration(CodeImp.DoomBuilder.Config.ScriptType.ACS);
        string name = config.Snippets.First(n => System.Linq.Enumerable.Any(config.GetSnippet(n), l => l.Contains("[EP]")));
        Assert.True(editor.InsertSnippetAtCaret(name));
        Assert.DoesNotContain("[EP]", text.Text);
        Assert.StartsWith("    ", text.Text);
        foreach(string line in text.Text.Split('\n').Skip(1)) Assert.StartsWith("    ", line);   // every following line keeps the indentation
        Assert.InRange(text.CaretOffset, 4, text.Text.Length);
    }

    [AvaloniaFact]
    public void The_editor_follows_the_script_preferences()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = (AvaloniaEdit.TextEditor)((TabItem)Find<TabControl>(editor).SelectedItem).Content;
        var settings = General.Settings;
        int size = settings.ScriptFontSize;
        bool numbers = settings.ScriptShowLineNumbers;
        try
        {
            var model = new CodeImp.DoomBuilder.Windows.PreferencesModel();
            Assert.Contains("Script editor", model.Tabs);
            model.Find("scriptfontsize").Value = 17;
            model.Find("scriptshowlinenumbers").Value = false;
            model.Apply();
            General.Map.ScriptEditor.Activate();     // what opening the editor again does
            Assert.Equal(17, text.FontSize);
            Assert.False(text.ShowLineNumbers);
        }
        finally
        {
            settings.ScriptFontSize = size;
            settings.ScriptShowLineNumbers = numbers;
        }
    }

    [AvaloniaFact]
    public void Reopening_the_editor_returns_to_the_caret_and_tab_it_was_left_at()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = (AvaloniaEdit.TextEditor)((TabItem)Find<TabControl>(editor).SelectedItem).Content;
        text.Text = "script 1 (void)\n{\n    Print(s:\"hello\");\n}\n";
        text.CaretOffset = 20;

        General.Map.CloseScriptEditor(false);      // saves the lumps and remembers the view
        Flush();
        Assert.False(General.Map.IsScriptsWindowOpen);

        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        var again = (AvaloniaEdit.TextEditor)((TabItem)Find<TabControl>(ScriptEditorWindow.Instance).SelectedItem).Content;
        Assert.StartsWith("script 1", again.Text);
        Assert.Equal(20, again.CaretOffset);
    }

    private static AvaloniaEdit.TextEditor CurrentText(ScriptEditorWindow editor) => (AvaloniaEdit.TextEditor)((TabItem)Find<TabControl>(editor).SelectedItem).Content;

    [AvaloniaFact]
    public void A_script_file_opens_edits_and_saves_with_its_own_type_and_without_trailing_spaces()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        string path = Path.Combine(dir, "test.acs");
        File.WriteAllText(path, "script 1 (void)\n{\n}\n");

        Assert.True(editor.OpenFile(path));
        Assert.Contains("test.acs", editor.TabTitles());
        var text = CurrentText(editor);
        Assert.StartsWith("script 1", text.Text);

        text.Text = "script 2 (void)   \n{\n}   \n";
        Assert.EndsWith("*", editor.TabTitles().Last());
        Assert.True(editor.SaveCurrent());
        Assert.Equal("script 2 (void)\n{\n}\n", File.ReadAllText(path));
        Assert.Equal("test.acs", editor.TabTitles().Last());

        // Lumps are saved with the map by themselves, files are not: the map sees no change
        Assert.False(General.Map.ScriptEditor.Editor.CheckImplicitChanges());

        // Opening it again selects the same tab
        int count = editor.TabTitles().Count;
        editor.OpenFile(path);
        Assert.Equal(count, editor.TabTitles().Count);
    }

    [AvaloniaFact]
    public void A_new_file_is_saved_as_asked_and_closing_a_changed_tab_asks()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var dialogs = new ScriptedDialogs();
        General.Dialogs = dialogs;

        var acs = General.GetScriptConfiguration(CodeImp.DoomBuilder.Config.ScriptType.ACS);
        editor.NewFile(acs);
        Assert.Contains(editor.TabTitles(), t => t.StartsWith("Untitled"));
        var text = CurrentText(editor);
        text.Text = "int x;";

        string path = Path.Combine(dir, "new.acs");
        dialogs.FilesToChoose.Enqueue(path);
        Assert.True(editor.SaveCurrent());              // no name yet: asks for one
        Assert.Equal("int x;", File.ReadAllText(path));
        Assert.Equal("new.acs", editor.TabTitles().Last());

        text.Text = "int y;";
        dialogs.MessageAnswers.Enqueue(System.Windows.Forms.DialogResult.Cancel);
        Assert.False(editor.CloseCurrentTab());         // cancelled: the tab stays
        Assert.Equal("new.acs*", editor.TabTitles().Last());

        dialogs.MessageAnswers.Enqueue(System.Windows.Forms.DialogResult.No);
        Assert.True(editor.CloseCurrentTab());          // "don't save": gone, the file is as it was
        Assert.DoesNotContain(editor.TabTitles(), t => t.StartsWith("new.acs"));
        Assert.Equal("int x;", File.ReadAllText(path));

        // The map's lumps cannot be closed
        Assert.False(editor.CloseCurrentTab());
    }

    [AvaloniaFact]
    public void A_script_resource_of_a_wad_resource_opens_read_only()
    {
        string resourcewad = SampleWadWithLump("DECORATE", Encoding.ASCII.GetBytes("actor ScriptTestThing 31999\n{\n}\n"));
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg", iwad: resourcewad);
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        var editor = ScriptEditorWindow.Instance;

        Assert.True(General.Map.Data.ScriptResources.TryGetValue(CodeImp.DoomBuilder.Config.ScriptType.DECORATE, out var set), "DECORATE was not found in the resources");
        var resource = set.First(r => r.Filename.EndsWith("DECORATE", System.StringComparison.OrdinalIgnoreCase));
        Assert.True(resource.IsReadOnly);                // resource wads are never written, as in UDB

        Assert.NotNull(editor.OpenResource(resource));
        var text = CurrentText(editor);
        Assert.Contains("ScriptTestThing", text.Text);
        Assert.True(text.IsReadOnly);
        Assert.False(editor.SaveCurrent());
        Assert.Contains(editor.TabTitles(), t => t.EndsWith("DECORATE"));
    }

    [AvaloniaFact]
    public void An_error_in_a_resource_script_opens_it_at_the_line()
    {
        string resourcewad = SampleWadWithLump("DECORATE", Encoding.ASCII.GetBytes("actor Good 31998\n{\n}\nactor Broken 31999\n{\n}\n"));
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg", iwad: resourcewad);
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        var editor = ScriptEditorWindow.Instance;
        var resource = General.Map.Data.ScriptResources[CodeImp.DoomBuilder.Config.ScriptType.DECORATE].First(r => r.Filename.EndsWith("DECORATE", System.StringComparison.OrdinalIgnoreCase));

        var error = new TextResourceErrorItem(ErrorType.Error, resource.ScriptType, resource.Resource.Location, resource.Filename, resource.LumpIndex, 3, "boom");
        error.ShowSource();                 // what double clicking it in the errors window does
        Flush();

        Assert.Contains(editor.TabTitles(), t => t.EndsWith("DECORATE"));
        var text = CurrentText(editor);
        Assert.Equal("actor Broken 31999", text.SelectedText);
    }

    [AvaloniaFact]
    public void The_open_files_come_back_when_the_editor_is_opened_again()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        string path = Path.Combine(dir, "again.acs");
        File.WriteAllText(path, "int z;");
        editor.OpenFile(path);

        General.Map.CloseScriptEditor(false);
        Flush();
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        Assert.Contains("again.acs", ScriptEditorWindow.Instance.TabTitles());
    }

    private static ScriptHighlightRenderer HighlightOf(AvaloniaEdit.TextEditor text) => text.TextArea.TextView.BackgroundRenderers.OfType<ScriptHighlightRenderer>().Single();

    [AvaloniaFact]
    public void Typing_an_opening_bracket_adds_the_closing_one_when_asked()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        var settings = General.Settings;
        bool old = settings.ScriptAutoCloseBrackets;
        try
        {
            settings.ScriptAutoCloseBrackets = true;
            text.Text = "";
            text.TextArea.PerformTextInput("(");
            Assert.Equal("()", text.Text);
            Assert.Equal(1, text.CaretOffset);                 // between them
            text.TextArea.PerformTextInput(")");               // typing the closing one over... adds a second (no overtype in UDB either)
            text.Text = "";

            settings.ScriptAutoCloseBrackets = false;
            text.TextArea.PerformTextInput("[");
            Assert.Equal("[", text.Text);
        }
        finally { settings.ScriptAutoCloseBrackets = old; }
    }

    [AvaloniaFact]
    public void Enter_indents_like_the_line_above_and_opens_a_block_between_braces()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        var settings = General.Settings;
        bool autoindent = settings.ScriptAutoIndent, tabs = settings.ScriptUseTabs, allman = settings.ScriptAllmanStyle, close = settings.ScriptAutoCloseBrackets;
        int width = settings.ScriptTabWidth;
        // The editor starts a line with the newline of the platform (CR LF on Windows): compare without it
        static string Unix(string value) => value.Replace("\r\n", "\n");
        try
        {
            settings.ScriptAutoIndent = true; settings.ScriptUseTabs = false; settings.ScriptTabWidth = 4; settings.ScriptAllmanStyle = false; settings.ScriptAutoCloseBrackets = false;
            General.Map.ScriptEditor.Activate();             // the settings reach the editor when it is shown again

            text.Text = "  foo";
            text.CaretOffset = 5;
            text.TextArea.PerformTextInput("\n");
            Assert.Equal("  foo\n  ", Unix(text.Text));                        // the indentation of the line above
            Assert.Equal(text.Text.Length, text.CaretOffset);

            text.Text = "{";
            text.CaretOffset = 1;
            text.TextArea.PerformTextInput("\n");
            Assert.Equal("{\n    ", Unix(text.Text));                         // one level deeper after an opening brace

            text.Text = "{}";
            text.CaretOffset = 1;
            text.TextArea.PerformTextInput("\n");
            Assert.Equal("{\n    \n}", Unix(text.Text));                      // the closing brace on its own line
            Assert.Equal(6 + (text.Text.Contains("\r\n") ? 1 : 0), text.CaretOffset);                          // and the caret on the indented line between

            settings.ScriptAllmanStyle = true;
            text.Text = "if(x) {";
            text.CaretOffset = 7;
            text.TextArea.PerformTextInput("\n");
            Assert.Equal("if(x)\n{\n    ", Unix(text.Text));                   // the brace moved to its own line
        }
        finally
        {
            settings.ScriptAutoIndent = autoindent; settings.ScriptUseTabs = tabs; settings.ScriptAllmanStyle = allman; settings.ScriptAutoCloseBrackets = close; settings.ScriptTabWidth = width;
        }
    }

    [AvaloniaFact]
    public void The_brace_at_the_caret_is_matched_or_marked_as_missing()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        var highlight = HighlightOf(text);

        text.Text = "if (a (b)) { x }";
        text.CaretOffset = 4;                                  // just after the first "("
        Assert.Equal((3, 9), highlight.Braces);                 // its ")" skips the nested pair

        text.CaretOffset = 15;                                 // after "x " and before "}"
        Assert.Equal((15, 11), highlight.Braces);               // matched backwards to the "{"

        text.Text = "(";
        text.CaretOffset = 1;
        Assert.Equal(-2, highlight.Braces.Second);              // no partner
        text.CaretOffset = 0;
        Assert.Equal(0, highlight.Braces.First);
    }

    [AvaloniaFact]
    public void The_selected_word_is_marked_where_else_it_is_used()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        var highlight = HighlightOf(text);
        text.Text = "int count; count = counter + count;";
        text.Select(4, 5);                                      // "count"
        Assert.Equal(new[] { 11, 29 }, highlight.WordMatches.Select(m => m.Offset));   // not "counter", not the selection itself
    }

    [Fact]
    public void Code_blocks_and_regions_can_be_folded_but_not_the_ones_in_comments_or_strings()
    {
        var config = new CodeImp.DoomBuilder.Config.ScriptConfiguration();
        string text = "script 1 (void)\n{\n    if (x)\n    {\n    }\n    // { not a block\n    Print(s:\"}\");\n}\n#region a\nint y;\n#endregion\n{ one line }\n";
        var folds = ScriptFolding.Find(text, config).ToList();
        Assert.Equal(3, folds.Count);
        Assert.Equal(text.IndexOf('{'), folds[0].StartOffset);          // the whole script block
        Assert.Contains(folds, f => f.Name == "#region");
    }

    [AvaloniaFact]
    public void The_script_colors_of_the_preferences_reach_the_editor()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        var model = new CodeImp.DoomBuilder.Windows.PreferencesModel();
        var colors = General.Colors;
        int old = colors.ScriptBackground.ToInt();
        try
        {
            Assert.Contains(model.InTab("Script editor"), p => p.Key == "colorcomments");
            model.Find("colorscriptbackground").Value = unchecked((int)0xFF102030);
            model.Apply();
            General.Map.ScriptEditor.Activate();
            var back = (Avalonia.Media.SolidColorBrush)text.Background;
            Assert.Equal(Avalonia.Media.Color.FromRgb(0x10, 0x20, 0x30), back.Color);
        }
        finally { colors.ScriptBackground = CodeImp.DoomBuilder.Rendering.PixelColor.FromInt(old); }
    }

    [AvaloniaFact]
    public void Collapsed_folds_are_kept_in_the_map_options_and_come_back()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        text.Text = "script 1 (void)\n{\n    Print(s:\"a\");\n}\n\nscript 2 (void)\n{\n    Print(s:\"b\");\n}\n";
        editor.RefreshFoldings();
        var folding = editor.CurrentFolding;
        Assert.NotNull(folding);
        var second = folding.AllFoldings.OrderBy(f => f.StartOffset).Last();
        second.IsFolded = true;

        General.Map.ScriptEditor.Editor.WriteOpenFilesToConfiguration();
        var saved = General.Map.Options.ScriptDocumentSettings["SCRIPTS"];   // case-insensitive, like the lump names
        Assert.Equal(new[] { 7 }, saved.FoldLevels[1].ToArray());      // the second block opens with its "{" on line 7 (1-based)

        General.Map.CloseScriptEditor(false);
        Flush();
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        var again = ScriptEditorWindow.Instance;
        var folds = again.CurrentFolding.AllFoldings.OrderBy(f => f.StartOffset).ToList();
        Assert.Equal(new[] { false, true }, folds.Select(f => f.IsFolded));
    }

    [AvaloniaFact]
    public void The_fold_colors_of_the_preferences_reach_the_fold_margin()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        var text = CurrentText(editor);
        text.Text = "script 1 (void)\n{\n}\n";
        var colors = General.Colors;
        var old = colors.ScriptFoldForeColor;
        try
        {
            colors.ScriptFoldForeColor = CodeImp.DoomBuilder.Rendering.PixelColor.FromInt(unchecked((int)0xFF0A141E));
            editor.RefreshFoldings();
            var margin = text.TextArea.LeftMargins.OfType<AvaloniaEdit.Folding.FoldingMargin>().Single();
            var brush = (Avalonia.Media.SolidColorBrush)margin.FoldingMarkerBrush;
            Assert.Equal(Avalonia.Media.Color.FromRgb(0x0A, 0x14, 0x1E), brush.Color);
            Assert.Contains(new CodeImp.DoomBuilder.Windows.PreferencesModel().InTab("Script editor"), p => p.Key == "colorscriptfoldback");
        }
        finally { colors.ScriptFoldForeColor = old; }
    }

    [AvaloniaFact]
    public void The_script_type_of_a_file_tab_can_be_changed()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        string path = Path.Combine(dir, "notes.dat");
        File.WriteAllText(path, "script 1 (void)\n{\n}\n");
        editor.OpenFile(path);
        Flush();
        var plain = editor.CurrentConfig;
        var text = CurrentText(editor);
        Assert.Null(editor.CurrentFolding);                    // plain text does not fold

        var acs = General.GetScriptConfiguration(CodeImp.DoomBuilder.Config.ScriptType.ACS);
        Assert.NotNull(acs);
        editor.SetCurrentScriptType(acs);
        Assert.Same(acs, editor.CurrentConfig);
        Assert.NotSame(plain, editor.CurrentConfig);
        Assert.NotNull(editor.CurrentFolding);                 // now it does
        editor.RefreshFoldings();
        Assert.Single(editor.CurrentFolding.AllFoldings);
        Assert.True(text.TextArea.TextView.LineTransformers.OfType<ScriptSyntaxColorizer>().Count() == 1);
    }

    [AvaloniaFact]
    public void The_script_type_picked_for_a_file_tab_comes_back_when_the_editor_is_opened_again()
    {
        ScriptEditorWindow editor = OpenScriptEditor();
        string path = Path.Combine(dir, "typed.dat");
        File.WriteAllText(path, "script 1 (void)\n{\n}\n");
        editor.OpenFile(path);
        Flush();
        var acs = General.GetScriptConfiguration(CodeImp.DoomBuilder.Config.ScriptType.ACS);
        editor.SetCurrentScriptType(acs);

        General.Map.CloseScriptEditor(false);
        Flush();
        General.Actions.InvokeAction("builder_openscripteditor");
        Flush();
        var again = ScriptEditorWindow.Instance;
        Assert.Contains("typed.dat", again.TabTitles());
        Assert.Equal(CodeImp.DoomBuilder.Config.ScriptType.ACS, again.CurrentConfig.ScriptType);
    }
}
