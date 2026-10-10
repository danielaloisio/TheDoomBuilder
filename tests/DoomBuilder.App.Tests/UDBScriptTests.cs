using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.UDBScript;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The UDBScript plugin: JavaScript (Jint) scripts that edit the map, with a docker, options, messages and a window that runs them.</summary>
public class UDBScriptTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private string ScriptsDir => Path.Combine(Program.ApplicationDirectory, "UDBScript", "Scripts");

    // Puts a script into the scripts folder and loads the folder again; returns what the plugin knows about it
    private ScriptInfo AddScript(string file, string text)
    {
        BuilderPlug.Me.SetWatching(false);                       // (the scripts are read again here: not also when the watcher notices the new file)
        string path = Path.Combine(ScriptsDir, "tests", file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text);
        BuilderPlug.Me.LoadScripts();
        Flush();
        return Find(path);
    }

    private static ScriptInfo Find(string path)
    {
        ScriptInfo found = null;
        void Walk(ScriptDirectoryStructure d) { foreach (var s in d.Scripts.Where(s => s.ScriptFile == path)) found = s; foreach (var sub in d.Directories) Walk(sub); }
        Walk(BuilderPlug.Me.ScriptDirectoryStructure);
        return found;
    }

    private void Run(ScriptInfo script)
    {
        BuilderPlug.Me.CurrentScript = script;
        General.Actions.InvokeAction("udbscript_udbscriptexecute");
        Flush();
    }

    // ---------------------------------------------------------------- the plugin

    [AvaloniaFact]
    public void The_plugin_adds_its_docker_and_shows_the_scripts_of_the_folder_as_a_tree()
    {
        OpenEditor();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.Contains(shell.Dockers.Dockers, d => d.Title == "Scripts");
        Assert.NotNull(General.Actions.GetActionByName("udbscript_udbscriptexecute"));
        Assert.NotNull(General.Actions.GetActionByName("udbscript_udbscriptexecuteslot30"));

        // The scripts are read on a task of their own when the map opens
        for (int i = 0; i < 500 && BuilderPlug.Me.ScriptDirectoryStructure == null; i++) { Flush(); System.Threading.Thread.Sleep(10); }
        Assert.NotNull(BuilderPlug.Me.ScriptDirectoryStructure);
        Assert.Contains(BuilderPlug.Me.ScriptDirectoryStructure.Directories, d => d.Name == "Examples");

        var panel = (ScriptDockerControl)shell.Dockers.Dockers.First(d => d.Title == "Scripts").Control;
        panel.FillTree();
        Assert.Contains(panel.Roots, n => n.Text == "Examples" && n.IsFolder);
        Assert.Contains(panel.Roots.First(n => n.Text == "Examples").Nodes, n => n.Text == "Imps to Arch-Viles");
    }

    [AvaloniaFact]
    public void The_header_of_a_script_gives_its_name_description_version_and_options()
    {
        OpenEditor();
        var info = AddScript("header.js", "// comment\n/* more */\n`#version 4`;\n`#name My script`;\n`#description Does\nthings`;\n`#scriptoptions\ncount\n{\n\tdescription = \"How many\";\n\ttype = 0;\n\tdefault = 3;\n}\n`;\nUDB.log(1);\n`#name Not a header`;\n");
        Assert.NotNull(info);
        Assert.Equal("My script", info.Name);
        Assert.Equal("Does things", info.Description);
        Assert.Equal(4u, info.Version);
        Assert.Single(info.Options);
        Assert.Equal("count", info.Options[0].name);
        Assert.Equal("How many", info.Options[0].description);
        Assert.Equal("3", info.Options[0].defaultvalue.ToString());

        // A script without a header has its file name and the default description
        var plain = AddScript("plain.js", "UDB.log(1);");
        Assert.Equal("plain", plain.Name);
        Assert.Equal("No description.", plain.Description);
        Assert.Equal(1u, plain.Version);
    }

    // ---------------------------------------------------------------- running

    [AvaloniaFact]
    public void A_script_changes_the_map_with_one_undo_level_and_the_window_goes_away_by_itself()
    {
        OpenEditor();
        var script = AddScript("things.js", "`#version 4`;\n`#name Make things`;\nfor(let i = 0; i < 3; i++) UDB.Map.createThing(new UDB.Vector2D(10 * i, 20), 3004);\n");
        int things = General.Map.Map.Things.Count, undos = General.Map.UndoRedo.GetUndoList().Count;

        Run(script);
        Assert.Equal(things + 3, General.Map.Map.Things.Count);
        Assert.Equal(undos + 1, General.Map.UndoRedo.GetUndoList().Count);
        Assert.Equal("Run script Make things", General.Map.UndoRedo.NextUndo.Description);
        Assert.True(General.Map.Map.IsSafeToAccess);
        Assert.False(BuilderPlug.Me.ScriptRunnerForm.IsRunning);
        Assert.Empty(window.OwnedWindows.Where(w => w.IsVisible));                       // it was never shown: the script was quick
    }

    [AvaloniaFact]
    public void A_script_that_logs_keeps_the_window_open_with_its_text()
    {
        OpenEditor();
        var script = AddScript("log.js", "`#version 4`;\nUDB.log('first');\nUDB.setProgress(40);\nUDB.log('second');\n");

        string log = null, status = null;
        // The window stays open until it is closed: wait (from a timer, so that the script can finish) for the script to be done, then close it
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (s, e) =>
        {
            var form = BuilderPlug.Me.ScriptRunnerForm;
            if (form.Window == null || form.IsRunning || !form.IsShown) return;
            timer.Stop();
            log = form.Log; status = form.Status;
            Assert.Equal("Close", form.ActionButton.Content);
            Assert.Equal(0, form.Progress.Value);                                            // (the progress goes back to 0 at the end)
            Click(form.ActionButton);
        };
        timer.Start();
        try { Run(script); } finally { timer.Stop(); }
        Assert.Equal("first" + Environment.NewLine + "second", log);
        Assert.StartsWith("Script finished. Runtime: ", status);
    }

    [AvaloniaFact]
    public void A_script_error_shows_the_error_window_and_withdraws_what_the_script_did()
    {
        OpenEditor();
        var script = AddScript("error.js", "`#version 4`;\nUDB.Map.createThing(new UDB.Vector2D(1, 1), 3004);\nthrow new Error('boom');\n");
        int things = General.Map.Map.Things.Count, undos = General.Map.UndoRedo.GetUndoList().Count;

        string text = null;
        WhenShown<UDBScriptErrorForm>(w => { text = w.StackTrace.Text; Click(w.GetVisualDescendantsOf<Button>().First(b => b.Content as string == "OK")); });
        Run(script);
        Assert.Contains("boom", text);
        Assert.Equal(things, General.Map.Map.Things.Count);
        Assert.Equal(undos, General.Map.UndoRedo.GetUndoList().Count);
        Assert.True(General.Map.Map.IsSafeToAccess);
    }

    [AvaloniaFact]
    public void A_syntax_error_is_reported_before_anything_runs()
    {
        OpenEditor();
        var script = AddScript("syntax.js", "`#version 4`;\nlet x = ;\n");
        var dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = dialogs;
        bool errorwindow = false;
        WhenShown<UDBScriptErrorForm>(w => { errorwindow = true; w.Close(); });
        Run(script);
        Assert.True(errorwindow || dialogs.Messages.Any(m => m.Contains("parsing") || m.Contains("error")), "an error was shown");
        Assert.True(General.Map.Map.IsSafeToAccess);
    }

    [AvaloniaFact]
    public void exit_and_die_end_the_script_and_die_withdraws_the_changes()
    {
        OpenEditor();
        int things = General.Map.Map.Things.Count;
        Run(AddScript("exit.js", "`#version 4`;\nUDB.Map.createThing(new UDB.Vector2D(1, 1), 3004);\nUDB.exit('done');\nUDB.Map.createThing(new UDB.Vector2D(2, 2), 3004);\n"));
        Assert.Equal(things + 1, General.Map.Map.Things.Count);                              // stops, keeps what was done

        Run(AddScript("die.js", "`#version 4`;\nUDB.Map.createThing(new UDB.Vector2D(1, 1), 3004);\nUDB.die('no');\n"));
        Assert.Equal(things + 1, General.Map.Map.Things.Count);                              // stops, takes back what it did
    }

    [AvaloniaFact]
    public void A_script_of_the_examples_that_comes_with_the_program_runs()
    {
        // Two Imps on all skills (one UV only): the example makes Arch-Viles of the ones that are UV only, and adds health potions
        string map = UdmfSample
            + "thing { x = 32.0; y = 32.0; type = 3001; skill1 = true; skill2 = true; skill3 = true; skill4 = true; skill5 = true; single = true; }\n"
            + "thing { x = 96.0; y = 96.0; type = 3001; skill5 = true; single = true; }\n";
        OpenEditor(wadPath: WriteUdmfWad(map), config: "GZDoom_DoomUDMF.cfg");
        for (int i = 0; i < 500 && BuilderPlug.Me.ScriptDirectoryStructure == null; i++) { Flush(); System.Threading.Thread.Sleep(10); }
        var info = Find(Path.Combine(ScriptsDir, "Examples", "imps2archviles.js"));
        Assert.NotNull(info);
        Assert.Equal("Imps to Arch-Viles", info.Name);

        Run(info);
        var things = General.Map.Map.Things.ToList();
        Assert.Equal(1, things.Count(t => t.Type == 64 && t.Position.x == 96));                 // the UV-only imp became an Arch-Vile
        Assert.Equal(2, things.Count(t => t.Type == 2014));                                      // a health potion for each imp that is UV
        Assert.Equal(1, things.Count(t => t.Type == 64 && t.Position.x == 32));                  // an Arch-Vile next to the imp on all skills
    }

    // ---------------------------------------------------------------- dialogs of a script

    [AvaloniaFact]
    public void showMessage_and_showMessageYesNo_ask_in_a_window_and_the_script_goes_on_with_the_answer()
    {
        OpenEditor();
        var script = AddScript("message.js", "`#version 4`;\nUDB.showMessage('hello\\nthere');\nlet yes = UDB.showMessageYesNo('sure?');\nUDB.Map.createThing(new UDB.Vector2D(1, 1), yes ? 3004 : 3001);\n");

        var texts = new System.Collections.Generic.List<string>();
        int answered = 0;
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (s, e) =>
        {
            var form = window.OwnedWindows.OfType<MessageForm>().FirstOrDefault(w => w.IsVisible);
            if (form == null) return;
            texts.Add(form.MessageBox.Text);
            answered++;
            if (form.Button2.IsVisible) Click(form.Button2);                               // "Yes" is the first option: the second button
            else Click(form.Button1);
        };
        timer.Start();
        try { Run(script); }
        finally { timer.Stop(); }

        Assert.Equal(new[] { "hello" + Environment.NewLine + "there", "sure?" }, texts);
        Assert.Equal(3004, General.Map.Map.Things.Last().Type);                              // the answer was yes
    }

    [AvaloniaFact]
    public void Aborting_from_a_message_ends_the_script()
    {
        OpenEditor();
        General.Dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs { };
        ((CodeImp.DoomBuilder.Windows.ScriptedDialogs)General.Dialogs).MessageAnswers.Enqueue(System.Windows.Forms.DialogResult.Yes);   // "are you sure you want to abort?"
        var script = AddScript("abort.js", "`#version 4`;\nUDB.Map.createThing(new UDB.Vector2D(1, 1), 3004);\nUDB.showMessage('stop me');\nUDB.Map.createThing(new UDB.Vector2D(2, 2), 3004);\n");
        int things = General.Map.Map.Things.Count;
        WhenShown<MessageForm>(w => Click(w.AbortButton));
        Run(script);
        Assert.Equal(things, General.Map.Map.Things.Count);                                  // aborted: everything taken back
    }

    [AvaloniaFact]
    public void QueryOptions_asks_for_values_and_gives_them_to_the_script()
    {
        OpenEditor();
        var script = AddScript("query.js", "`#version 4`;\nlet q = new UDB.QueryOptions();\nq.addOption('count', 'How many things', 0, 2);\nq.addOption('label', 'A name', 2, 'zed');\nif(!q.query()) UDB.exit();\nfor(let i = 0; i < q.options.count; i++) UDB.Map.createThing(new UDB.Vector2D(i, i), 3004);\nUDB.Map.getThings()[0].fields.comment = q.options.label;\n");
        int things = General.Map.Map.Things.Count;

        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Query options", d.Title);
            var boxes = d.GetVisualDescendantsOf<TextBox>().ToList();
            Assert.Equal(2, boxes.Count);
            Assert.Equal("2", boxes[0].Text);                                              // the defaults
            Assert.Equal("zed", boxes[1].Text);
            boxes[0].Text = "4";
            boxes[1].Text = "quux";
            Click(d.OkButton);
        });
        Run(script);
        Assert.True(shown);
        Assert.Equal(things + 4, General.Map.Map.Things.Count);
    }

    [AvaloniaFact]
    public void Cancelling_QueryOptions_makes_query_false()
    {
        OpenEditor();
        var script = AddScript("querycancel.js", "`#version 4`;\nlet q = new UDB.QueryOptions();\nq.addOption('count', 'How many', 0, 2);\nif(q.query()) UDB.Map.createThing(new UDB.Vector2D(1, 1), 3004);\n");
        int things = General.Map.Map.Things.Count;
        WhenShown<SimpleDialog>(d => Click(d.CancelButton));
        Run(script);
        Assert.Equal(things, General.Map.Map.Things.Count);
    }

    // ---------------------------------------------------------------- the docker

    private ScriptDockerControl Docker()
    {
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        return (ScriptDockerControl)shell.Dockers.Dockers.First(d => d.Title == "Scripts").Control;
    }

    private static ScriptNode NodeOf(ScriptDockerControl panel, string text)
    {
        ScriptNode Walk(System.Collections.Generic.IEnumerable<ScriptNode> nodes) { foreach (var n in nodes) { if (n.Text.StartsWith(text)) return n; var r = Walk(n.Nodes); if (r != null) return r; } return null; }
        return Walk(panel.Roots);
    }

    [AvaloniaFact]
    public void Selecting_a_script_shows_its_description_and_options_and_the_options_reach_the_script()
    {
        OpenEditor();
        var script = AddScript("options.js", "`#version 4`;\n`#name Optional`;\n`#description Uses options`;\n`#scriptoptions\ncount\n{\n\tdescription = \"How many\";\n\ttype = 0;\n\tdefault = 1;\n}\n`;\nfor(let i = 0; i < UDB.ScriptOptions.count; i++) UDB.Map.createThing(new UDB.Vector2D(i, i), 3004);\n");
        var panel = Docker();
        panel.FillTree();
        var node = NodeOf(panel, "Optional");
        Assert.NotNull(node);

        panel.Tree.SelectedItem = node;
        Assert.Equal("Uses options", panel.Description.Text);
        Assert.Same(script, BuilderPlug.Me.CurrentScript);
        Assert.Single(panel.Options.Rows);
        var row = panel.Options.Rows[0];
        Assert.Equal("How many", row.Description.Text);
        Assert.Equal("1", row.Box.Text);

        // A value typed into the option reaches the script; the default is back after "Reset"
        row.Box.Text = "3";
        panel.EndEdit();
        int things = General.Map.Map.Things.Count;
        Click(panel.RunButton);
        Flush();
        Assert.Equal(things + 3, General.Map.Map.Things.Count);

        Click(panel.ResetButton);
        Assert.Equal("1", panel.Options.Rows[0].Box.Text);
        Assert.Equal(1, Convert.ToInt32(script.Options[0].typehandler.GetValue()));

        // The filter looks at names and descriptions
        panel.Filter.Text = "uses opt";
        Assert.Single(panel.Roots.SelectMany(Flat).Where(n => n.Tag is ScriptInfo));
        panel.Filter.Text = "no such script anywhere";
        Assert.Empty(panel.Roots.SelectMany(Flat).Where(n => n.Tag is ScriptInfo));
        Click(panel.ClearFilterButton);
        Assert.Equal("", panel.Filter.Text);
        Assert.NotEmpty(panel.Roots.SelectMany(Flat).Where(n => n.Tag is ScriptInfo));
    }

    private static System.Collections.Generic.IEnumerable<ScriptNode> Flat(ScriptNode n) => new[] { n }.Concat(n.Nodes.SelectMany(Flat));

    [AvaloniaFact]
    public void A_script_in_a_slot_runs_with_the_slot_action()
    {
        OpenEditor();
        var script = AddScript("slot.js", "`#version 4`;\n`#name Slotted`;\nUDB.Map.createThing(new UDB.Vector2D(5, 5), 3004);\n");
        var panel = Docker();
        panel.FillTree();
        var node = NodeOf(panel, "Slotted");

        panel.SetSlot(node, 3);
        // (the folder is watched: the scripts may have been read again meanwhile, so the same script is not the same object)
        Assert.Equal(script.ScriptFile, BuilderPlug.Me.GetScriptSlot(3).ScriptFile);
        Assert.Equal(3, BuilderPlug.Me.GetScriptSlotByScriptInfo(BuilderPlug.Me.GetScriptSlot(3)));
        Assert.StartsWith("Slotted [", NodeOf(panel, "Slotted").Text);                    // the tree shows the hotkey

        int things = General.Map.Map.Things.Count;
        General.Actions.InvokeAction("udbscript_udbscriptexecuteslot3");
        Flush();
        Assert.Equal(things + 1, General.Map.Map.Things.Count);

        panel.ClearSlot(NodeOf(panel, "Slotted"));
        Assert.Null(BuilderPlug.Me.GetScriptSlot(3));
    }
}

internal static class VisualExt
{
    public static System.Collections.Generic.IEnumerable<T> GetVisualDescendantsOf<T>(this Avalonia.Visual v) where T : class
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
