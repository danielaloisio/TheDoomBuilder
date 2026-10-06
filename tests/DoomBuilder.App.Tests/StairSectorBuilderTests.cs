using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.StairSectorBuilderMode;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The stair sector builder: a mode and a window that make a run of stair sectors along the selected lines (or around the selected sectors).</summary>
public class StairSectorBuilderTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static StairSectorBuilderForm Form() =>
        (StairSectorBuilderForm)General.Editing.Mode.GetType().GetField("stairsectorbuilderform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(General.Editing.Mode);

    private void Engage(bool selectline = true)
    {
        General.Editing.ChangeMode("LinedefsMode");
        General.Map.Map.ClearAllSelected();
        if (selectline) General.Map.Map.Linedefs.First().Selected = true;
        General.Actions.InvokeAction("stairsectorbuilder_stairsectorbuildermode");
        Flush();
    }

    [AvaloniaFact]
    public void The_mode_needs_something_selected_to_start()
    {
        OpenEditor();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "StairSectorBuilderMode");
        Engage(selectline: false);
        Assert.NotEqual("StairSectorBuilderMode", General.Editing.Mode.GetType().Name);          // nothing selected: back to the previous mode
    }

    [AvaloniaFact]
    public void The_window_opens_with_the_defaults_and_shows_the_heights_the_stairs_will_have()
    {
        OpenEditor();
        Engage();
        Assert.Equal("StairSectorBuilderMode", General.Editing.Mode.GetType().Name);
        var form = Form();
        Assert.True(form.Window.IsVisible);
        Assert.True(form.FullyLoaded);
        Assert.Equal(1u, form.NumberOfSectors);
        Assert.Equal(32u, form.SectorDepth);
        Assert.Equal(0, form.Spacing);
        Assert.True(form.SideFront);
        Assert.Equal(0, form.StairType);                                                          // straight

        // One line selected: only the straight stairs
        Assert.Equal(1, form.Tabs.Count);

        // The first and last heights of the floor and the ceiling follow the numbers
        form.FloorBaseBox.Text = "10";
        form.FloorModBox.Text = "8";
        form.NumberOfSectorsBox.Text = "4";
        Assert.Equal("18", form.FloorFirst.Text);
        Assert.Equal("42", form.FloorLast.Text);                                                  // 10 + 8 * 4

        // Without the floor height the heights are not computed (the base is for the "distinct base heights" off)
        form.FloorHeightCheck.Checked = false;
        Assert.False(form.FloorModBox.Enabled);
    }

    [AvaloniaFact]
    public void OK_builds_the_stair_sectors_and_Cancel_leaves_the_map_alone()
    {
        OpenEditor();
        Engage();
        var form = Form();
        int sectors = General.Map.Map.Sectors.Count;
        form.NumberOfSectorsBox.Text = "3";
        form.CancelButton.PerformClick();
        Flush();
        Assert.Equal(sectors, General.Map.Map.Sectors.Count);
        Assert.NotEqual("StairSectorBuilderMode", General.Editing.Mode.GetType().Name);

        Engage();
        form = Form();
        form.NumberOfSectorsBox.Text = "3";
        form.FloorModBox.Text = "8";
        General.Editing.Mode.OnRedrawDisplay();                                                  // (the stairs are worked out when the display is drawn)
        form.OkButton.PerformClick();
        Flush();
        Assert.Equal(sectors + 3, General.Map.Map.Sectors.Count);
        Assert.Equal("Build stair sectors", General.Map.UndoRedo.NextUndo.Description);
        var heights = General.Map.Map.Sectors.Skip(sectors).Select(s => s.FloorHeight).OrderBy(h => h).ToList();
        Assert.Equal(3, heights.Distinct().Count());                                              // each step has its own height
    }

    [AvaloniaFact]
    public void Prefabs_are_saved_loaded_and_deleted_and_the_last_choice_is_the_default_of_the_next_time()
    {
        OpenEditor();
        Engage();
        var form = Form();
        int count = BuilderPlug.Me.Prefabs.Count;
        Assert.Equal("Prefab #1", form.PrefabNameBox.Text);                                       // a name that is free

        form.NumberOfSectorsBox.Text = "5";
        form.SectorDepthBox.Text = "48";
        form.PrefabNameBox.Text = "wide";
        form.PrefabSaveButton.PerformClick();
        Assert.Equal(count + 1, BuilderPlug.Me.Prefabs.Count);
        Assert.Equal(count + 1, form.PrefabList.Count);
        Assert.Contains("wide", form.PrefabList.TextOf(count));

        form.NumberOfSectorsBox.Text = "2";
        form.SectorDepthBox.Text = "16";
        form.PrefabList.View.SelectedIndex = count;
        form.PrefabLoadButton.PerformClick();
        Assert.Equal(5u, form.NumberOfSectors);
        Assert.Equal(48u, form.SectorDepth);

        // The reserved names are not for the user
        var dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = dialogs;
        form.PrefabNameBox.Text = "[Previous]";
        form.PrefabSaveButton.PerformClick();
        Assert.Contains(dialogs.Messages, m => m.Contains("reserved"));
        Assert.Equal(count + 1, BuilderPlug.Me.Prefabs.Count);

        form.PrefabList.View.SelectedIndex = count;
        form.PrefabDeleteButton.PerformClick();
        Assert.Equal(count, BuilderPlug.Me.Prefabs.Count);
        Assert.Equal(count, form.PrefabList.Count);

        // OK keeps the choices as "[Previous]"
        form.NumberOfSectorsBox.Text = "2";
        form.OkButton.PerformClick();
        Flush();
        Assert.Contains(BuilderPlug.Me.Prefabs, p => p.name == "[Previous]" && p.numberofsectors == 2);
    }

    [AvaloniaFact]
    public void Selecting_sectors_makes_the_stairs_around_them_only()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        General.Map.Map.ClearAllSelected();
        General.Map.Map.Sectors.First().Selected = true;
        General.Actions.InvokeAction("stairsectorbuilder_stairsectorbuildermode");
        Flush();
        Assert.Equal("StairSectorBuilderMode", General.Editing.Mode.GetType().Name);
        var form = Form();
        Assert.Equal(1, form.Tabs.Count);
        Assert.True(form.SingleStepsCheck.Checked);                                               // sectors always make single steps
        Assert.False(form.SingleStepsCheck.Enabled);
        Assert.False(form.SingleDirectionCheck.Checked);
        form.CancelButton.PerformClick();
        Flush();
    }
}
