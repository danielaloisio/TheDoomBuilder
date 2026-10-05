using System;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using Xunit;
using DialogResult = System.Windows.Forms.DialogResult;

namespace DoomBuilder.App.Tests;

/// <summary>Copy and paste through the system clipboard, paste special and prefab files, in the real window.</summary>
public class CopyPasteTests : EditorTestBase
{
    private const string Prefix = "GZDOOM_BUILDER_GEOMETRY";

    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private string ClipboardText() => window.Clipboard.TryGetTextAsync().GetAwaiter().GetResult();
    private void SetClipboardText(string text) => window.Clipboard.SetTextAsync(text).GetAwaiter().GetResult();

    private Sector SelectFirstSector()
    {
        General.Editing.ChangeMode("SectorsMode");
        Sector sector = General.Map.Map.Sectors.First();
        ((CodeImp.DoomBuilder.BuilderModes.BaseClassicMode)General.Editing.Mode).SelectMapElement(sector);   // selects its vertices and lines too
        return sector;
    }

    [AvaloniaFact]
    public void Copying_puts_the_selection_on_the_system_clipboard_as_text()
    {
        OpenEditor();
        SelectFirstSector();

        General.Actions.InvokeAction("builder_copyselection");
        Flush();

        string text = ClipboardText();
        Assert.NotNull(text);
        Assert.StartsWith(Prefix, text);
        Assert.True(text.Length > Prefix.Length + 20);
    }

    [AvaloniaFact]
    public void Pasting_adds_what_the_system_clipboard_holds()
    {
        OpenEditor();
        SelectFirstSector();
        General.Actions.InvokeAction("builder_copyselection");
        int sectors = General.Map.Map.Sectors.Count, vertices = General.Map.Map.Vertices.Count;

        General.Actions.InvokeAction("builder_pasteselection");
        Flush();

        Assert.True(General.Map.Map.Sectors.Count > sectors);               // the sector and the virtual one its shared lines need
        Assert.True(General.Map.Map.Vertices.Count > vertices);
    }

    [AvaloniaFact]
    public void A_copy_from_another_instance_pastes_because_only_the_clipboard_counts()
    {
        OpenEditor();
        SelectFirstSector();
        General.Actions.InvokeAction("builder_copyselection");
        string copied = ClipboardText();

        SetClipboardText("something else");                                  // another program copied text
        int sectors = General.Map.Map.Sectors.Count;
        General.Actions.InvokeAction("builder_pasteselection");
        Flush();
        Assert.Equal(sectors, General.Map.Map.Sectors.Count);                // nothing to paste

        SetClipboardText(copied);                                            // the geometry arrives from elsewhere
        General.Actions.InvokeAction("builder_pasteselection");
        Flush();
        Assert.True(General.Map.Map.Sectors.Count > sectors);               // the sector and the virtual one its shared lines need
    }

    [AvaloniaFact]
    public void Paste_special_applies_the_chosen_tag_and_action_options()
    {
        OpenEditor();
        Sector sector = SelectFirstSector();
        sector.Tag = 77;
        General.Actions.InvokeAction("builder_copyselection");

        var dialogs = new ScriptedDialogs();
        dialogs.OnPasteOptions = form =>
        {
            Assert.Equal(PasteOptions.TAGS_KEEP, form.Options.ChangeTags);   // starts from the defaults
            form.Options = new PasteOptions { ChangeTags = PasteOptions.TAGS_REMOVE, RemoveActions = true };
            return DialogResult.OK;
        };
        General.Dialogs = dialogs;
        int sectors = General.Map.Map.Sectors.Count;

        General.Actions.InvokeAction("builder_pasteselectionspecial");
        Flush();

        Assert.True(General.Map.Map.Sectors.Count > sectors);
        Assert.Equal(77, sector.Tag);                                        // the original keeps its tag
        Assert.Single(General.Map.Map.Sectors, s => s.Tag == 77);             // the pasted copy lost it
    }

    [AvaloniaFact]
    public void Cancelling_paste_special_pastes_nothing()
    {
        OpenEditor();
        SelectFirstSector();
        General.Actions.InvokeAction("builder_copyselection");
        General.Dialogs = new ScriptedDialogs();                             // cancels
        int sectors = General.Map.Map.Sectors.Count;

        General.Actions.InvokeAction("builder_pasteselectionspecial");
        Flush();

        Assert.Equal(sectors, General.Map.Map.Sectors.Count);
    }

    [AvaloniaFact]
    public void A_prefab_saved_to_a_file_can_be_inserted_again()
    {
        OpenEditor();
        SelectFirstSector();
        string file = Path.Combine(dir, "room.dbprefab");
        Directory.CreateDirectory(dir);
        var dialogs = new ScriptedDialogs();
        dialogs.FilesToChoose.Enqueue(file);
        General.Dialogs = dialogs;

        General.Actions.InvokeAction("builder_createprefab");
        Flush();
        Assert.True(File.Exists(file));
        Assert.True(new FileInfo(file).Length > 0);

        dialogs.FilesToChoose.Enqueue(file);
        int sectors = General.Map.Map.Sectors.Count;
        General.Actions.InvokeAction("builder_insertprefabfile");
        Flush();

        Assert.True(General.Map.Map.Sectors.Count > sectors);               // the sector and the virtual one its shared lines need
    }
}

/// <summary>Saving the map and starting the engine on it.</summary>
public class TestMapTests : EditorTestBase
{
    [AvaloniaFact]
    public void Test_map_saves_a_temporary_wad_and_starts_the_engine_with_it()
    {
        if (OperatingSystem.IsWindows()) return;                              // the "engine" below is a shell script

        OpenEditor();
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, "args.txt");
        string engine = Path.Combine(dir, "engine.sh");
        File.WriteAllText(engine, "#!/bin/sh\nfor a in \"$@\"; do echo \"$a\"; done > \"" + output + "\"\n");
        File.SetUnixFileMode(engine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        General.Map.ConfigSettings.TestProgram = engine;
        General.Map.ConfigSettings.CustomParameters = true;
        General.Map.ConfigSettings.TestParameters = "-file \"%F\" -skill %S -warp %L1";
        General.Map.ConfigSettings.TestShortPaths = false;
        General.Map.ConfigSettings.TestLinuxPaths = false;
        General.Map.ConfigSettings.TestAdditionalParameters = "";
        General.Map.ConfigSettings.TestSkill = 4;

        General.Actions.InvokeAction("builder_testmap");

        for (int i = 0; i < 200 && !File.Exists(output); i++) System.Threading.Thread.Sleep(50);
        Assert.True(File.Exists(output), "the engine was not started");
        string[] args = File.ReadAllLines(output);
        Assert.Equal("-file", args[0]);
        Assert.EndsWith(".wad", args[1]);                                    // the quoted path is one argument
        Assert.True(args[1].Length > 4);
        Assert.Contains("-skill", args);
        Assert.Equal("4", args[Array.IndexOf(args, "-skill") + 1]);
        Assert.Contains("-warp", args);
    }

    [AvaloniaFact]
    public void Test_map_without_an_engine_asks_to_set_one_up_and_starts_nothing()
    {
        OpenEditor();
        General.Map.ConfigSettings.TestProgram = "";
        var dialogs = new ScriptedDialogs();
        General.Dialogs = dialogs;

        General.Actions.InvokeAction("builder_testmap");

        Assert.Contains(dialogs.Messages, m => m.Contains("test program"));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("-file \"C:\\a b\\x.wad\" -skill 3", 4)]
    [InlineData("one  two   \"three four\"  five", 4)]
    [InlineData("\"\"", 1)]
    public void Command_lines_split_into_arguments_like_the_engine_expects(string line, int count)
    {
        Assert.Equal(count, Launcher.SplitArguments(line).Count);
    }
}
