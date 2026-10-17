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
    public void The_toolbar_has_a_visible_test_map_button()
    {
        OpenEditor();
        window.UpdateLayout();
        var buttons = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<Avalonia.Controls.Primitives.ToggleButton>()
            .Where(b => b.IsVisible && Equals(Avalonia.Controls.ToolTip.GetTip(b), "Test Map"));
        Assert.NotEmpty(buttons);        // a split button without entries used to be dropped

        // ... and clicking it runs the action (it asks for a test program when none is set up)
        General.Map.ConfigSettings.TestProgram = "";
        var dialogs = new ScriptedDialogs();
        General.Dialogs = dialogs;
        buttons.First().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.Contains(dialogs.Messages, m => m.Contains("test program"));
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

    [AvaloniaFact]
    public void Test_map_builds_the_nodes_with_the_nodebuilder_before_starting_the_engine()
    {
        if (OperatingSystem.IsWindows()) return;                              // the stand-ins below are shell scripts

        // A nodebuilder that "builds" by handing back a prepared WAD whose node lumps carry a marker
        Directory.CreateDirectory(dir);
        string built = Path.Combine(dir, "built.wad");
        using (var wad = new CodeImp.DoomBuilder.IO.WAD(built))
        {
            int index = 0;
            foreach (string name in new[] { "MAP01", "THINGS", "LINEDEFS", "SIDEDEFS", "VERTEXES", "SEGS", "SSECTORS", "NODES", "SECTORS", "REJECT", "BLOCKMAP" })
            {
                byte[] data = name == "MAP01" ? Array.Empty<byte>() : System.Text.Encoding.ASCII.GetBytes(name == "NODES" ? "BUILT-NODES" : "data");
                var lump = wad.Insert(name, index++, data.Length);
                lump.Stream.Write(data, 0, data.Length);
            }
        }

        string nodedir = Path.Combine(dir, "app", "Compilers", "Nodebuilders");
        Directory.CreateDirectory(nodedir);
        File.WriteAllText(Path.Combine(nodedir, "fakebsp.cfg"),
            "compilers { fakebsp { interface = \"NodesCompiler\"; program = \"fakebsp.sh\"; } }\n" +
            "nodebuilders { fake_normal { title = \"Fake\"; compiler = \"fakebsp\"; parameters = \"-o%FO %FI\"; } }\n");
        string bsp = Path.Combine(nodedir, "fakebsp.sh");
        File.WriteAllText(bsp, "#!/bin/sh\nfor a in \"$@\"; do case \"$a\" in -o*) out=\"${a#-o}\";; esac; done\ncp '" + built + "' \"$out\"\n");
        File.SetUnixFileMode(bsp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // An engine that keeps the WAD it was started with
        string kept = Path.Combine(dir, "kept.wad");
        string engine = Path.Combine(dir, "engine.sh");
        File.WriteAllText(engine, "#!/bin/sh\nwhile [ \"$1\" != \"-file\" ] && [ -n \"$1\" ]; do shift; done\ncp \"$2\" '" + kept + "'\n");
        File.SetUnixFileMode(engine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        OpenEditor();
        General.Map.ConfigSettings.NodebuilderTest = "fake_normal";
        General.Map.ConfigSettings.TestProgram = engine;
        General.Map.ConfigSettings.CustomParameters = true;
        General.Map.ConfigSettings.TestParameters = "-file \"%F\"";

        General.Map.IsChanged = true;                                         // (nodes are only rebuilt when the map changed since the last save or test)
        General.Actions.InvokeAction("builder_testmap");
        for (int i = 0; i < 300 && !File.Exists(kept); i++) System.Threading.Thread.Sleep(50);

        Assert.True(File.Exists(kept), "the engine did not get a WAD");
        using (var wad = new CodeImp.DoomBuilder.IO.WAD(kept, true))
        {
            var nodes = wad.FindLump("NODES");
            Assert.NotNull(nodes);                                            // the node lumps came from the nodebuilder
            Assert.Equal("BUILT-NODES", System.Text.Encoding.ASCII.GetString(nodes.Stream.ReadAllBytes()));
            Assert.NotNull(wad.FindLump("THINGS"));                           // and the map itself is the edited one
        }
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

/// <summary>The window that runs the external (pre/post) commands.</summary>
public class ExternalCommandWindowTests : EditorTestBase
{
    private static System.Diagnostics.ProcessStartInfo Sh(string script)
    {
        var info = new System.Diagnostics.ProcessStartInfo { FileName = "/bin/sh" };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return info;
    }

    private static void WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(20); }
        Assert.True(condition(), "timed out");
    }

    [AvaloniaFact]
    public void A_successful_command_closes_the_window_by_itself_and_answers_OK()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenEditor();
        var form = new CodeImp.DoomBuilder.Windows.RunExternalCommandForm(Sh("echo hello"), new ExternalCommandSettings { AutoCloseOnSuccess = true });
        string shown = null;
        WhenShown<DoomBuilder.App.Dialogs.ExternalCommandWindow>(w =>
        {
            for (int i = 0; i < 100 && w.IsVisible; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(20); shown = w.OutputText; }
        });

        Assert.Equal(DialogResult.OK, form.ShowDialog());
    }

    [AvaloniaFact]
    public void A_failed_command_keeps_the_window_with_its_output_until_the_user_decides()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenEditor();
        var form = new CodeImp.DoomBuilder.Windows.RunExternalCommandForm(Sh("echo building; echo broken >&2; exit 2"), new ExternalCommandSettings());
        bool stillopen = false;
        string text = null;
        WhenShown<DoomBuilder.App.Dialogs.ExternalCommandWindow>(w =>
        {
            WaitFor(() => w.ContinueButton.IsEnabled);
            stillopen = w.IsVisible;
            text = w.OutputText;
            Click(w.CancelButton);
        });

        Assert.Equal(DialogResult.Cancel, form.ShowDialog());
        Assert.True(stillopen);
        Assert.Contains("building", text);
        Assert.Contains("broken", text);
        Assert.Contains("Exit code: 2", text);
    }

    [AvaloniaFact]
    public void Continue_accepts_a_failed_command_and_Run_again_starts_it_over()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenEditor();
        string marker = Path.Combine(dir, "runs.txt");
        Directory.CreateDirectory(dir);
        var form = new CodeImp.DoomBuilder.Windows.RunExternalCommandForm(Sh("echo run >> '" + marker + "'; exit 1"), new ExternalCommandSettings());
        WhenShown<DoomBuilder.App.Dialogs.ExternalCommandWindow>(w =>
        {
            WaitFor(() => w.ContinueButton.IsEnabled);
            Click(w.RetryButton);
            WaitFor(() => File.ReadAllLines(marker).Length == 2 && w.ContinueButton.IsEnabled);
            Click(w.ContinueButton);
        });

        Assert.Equal(DialogResult.OK, form.ShowDialog());
        Assert.Equal(2, File.ReadAllLines(marker).Length);
    }

    [AvaloniaFact]
    public void The_pre_test_command_runs_before_the_engine_starts()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenEditor();
        Directory.CreateDirectory(dir);
        string order = Path.Combine(dir, "order.txt");
        string engine = Path.Combine(dir, "engine.sh");
        File.WriteAllText(engine, "#!/bin/sh\necho engine >> '" + order + "'\n");
        File.SetUnixFileMode(engine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        General.Map.ConfigSettings.TestProgram = engine;
        General.Map.ConfigSettings.CustomParameters = true;
        General.Map.ConfigSettings.TestParameters = "-file \"%F\"";
        General.Map.Options.TestPreCommand = new ExternalCommandSettings { Commands = "echo precommand >> '" + order + "'\n", AutoCloseOnSuccess = true };
        WhenShown<DoomBuilder.App.Dialogs.ExternalCommandWindow>(w => WaitFor(() => !w.IsVisible));

        General.Actions.InvokeAction("builder_testmap");
        WaitFor(() => File.Exists(order) && File.ReadAllLines(order).Length == 2);

        Assert.Equal(new[] { "precommand", "engine" }, File.ReadAllLines(order));
    }
}
