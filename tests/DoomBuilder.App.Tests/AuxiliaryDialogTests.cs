using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.BuilderModes;
using CodeImp.DoomBuilder.BuilderModes.Interface;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using Xunit;
using DialogResult = System.Windows.Forms.DialogResult;

namespace DoomBuilder.App.Tests;

/// <summary>The small dialogs of the BuilderModes plugin, shown over the real window.</summary>
public class AuxiliaryDialogTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static System.Collections.Generic.List<T> All<T>(Avalonia.Controls.Control root) where T : class
        => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().ToList();

    [AvaloniaFact]
    public void Change_index_rejects_a_too_high_or_unchanged_index_and_answers_with_the_new_one()
    {
        OpenEditor();
        var form = new ChangeMapElementIndexForm("sector", 1, 5);
        WhenShown<SimpleDialog>(dialog =>
        {
            var box = All<NumberBox>((Avalonia.Controls.Control)dialog.Content).First();
            box.Text = "99";
            Assert.False(dialog.OkButton.IsEnabled);                      // beyond the maximum
            box.Text = "1";
            Assert.False(dialog.OkButton.IsEnabled);                      // the same index
            box.Text = "3";
            Assert.True(dialog.OkButton.IsEnabled);
            Click(dialog.OkButton);
        });

        Assert.Equal(DialogResult.OK, form.ShowDialog());
        Assert.Equal(3, form.GetNewIndex());
    }

    [AvaloniaFact]
    public void Cancelling_the_change_index_dialog_answers_cancel()
    {
        OpenEditor();
        var form = new ChangeMapElementIndexForm("thing", 0, 10);
        WhenShown<SimpleDialog>(dialog => Click(dialog.CancelButton));
        Assert.Equal(DialogResult.Cancel, form.ShowDialog());
    }

    [AvaloniaFact]
    public void Filter_things_keeps_only_the_chosen_types_selected()
    {
        OpenEditor();
        General.Editing.ChangeMode("ThingsMode");
        foreach (Thing t in General.Map.Map.Things) t.Selected = true;
        var types = FilterSelectedThingsForm.CountTypes(General.Map.Map.GetSelectedThings(true));
        Assert.True(types.Count > 1, "the sample map should have several thing types");
        Assert.Equal(types.Select(t => t.Item1).OrderBy(t => t), types.Select(t => t.Item1));       // sorted by type

        int keep = types[0].Item1;
        var form = new FilterSelectedThingsForm(General.Map.Map.GetSelectedThings(true), (ThingsMode)General.Editing.Mode);
        WhenShown<SimpleDialog>(dialog =>
        {
            var list = All<Avalonia.Controls.ListBox>((Avalonia.Controls.Control)dialog.Content).First();
            list.SelectedIndex = 0;
            Click(dialog.OkButton);
        });
        form.ShowDialog();

        Assert.All(General.Map.Map.GetSelectedThings(true), t => Assert.Equal(keep, t.Type));
        Assert.Equal(types[0].Item3, General.Map.Map.GetSelectedThings(true).Count);
    }

    [AvaloniaFact]
    public void Select_similar_selects_the_sectors_that_match_the_selected_one()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        Sector first = General.Map.Map.Sectors.First();
        first.Selected = true;
        int matching = General.Map.Map.Sectors.Count(s => s == first || (s.FloorHeight == first.FloorHeight && s.CeilHeight == first.CeilHeight
            && s.FloorTexture == first.FloorTexture && s.CeilTexture == first.CeilTexture && s.Brightness == first.Brightness
            && s.Effect == first.Effect && s.Tag == first.Tag));

        var form = new SelectSimilarElementOptionsPanel();
        Assert.True(form.Setup((BaseClassicMode)General.Editing.Mode));
        WhenShown<SimpleDialog>(dialog =>
        {
            // every property is a condition; turn all off with one toggle (they start on), so only identical-by-nothing remains: everything
            var toggle = All<Avalonia.Controls.Button>((Avalonia.Controls.Control)dialog.Content).Concat(dialog.ExtraButtons.Children.OfType<Avalonia.Controls.Button>()).First(b => b.Content as string == "Toggle All");
            Click(toggle);
            Click(dialog.OkButton);
        });
        form.ShowDialog();

        Assert.Equal(General.Map.Map.Sectors.Count, General.Map.Map.GetSelectedSectors(true).Count);   // no condition left: all match
        Assert.True(General.Map.Map.GetSelectedSectors(true).Count >= matching);
    }

    [AvaloniaFact]
    public void Select_similar_needs_a_selection()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        var form = new SelectSimilarElementOptionsPanel();
        Assert.False(form.Setup((BaseClassicMode)General.Editing.Mode));
    }

    [AvaloniaFact]
    public void Paste_properties_options_list_the_copied_kinds_and_store_the_choices()
    {
        OpenEditor();
        var form = new PastePropertiesOptionsForm();
        Assert.False(form.Setup(MapElementType.SECTOR));                  // nothing copied yet

        BuilderPlug.Me.CopiedSectorProps = new SectorProperties(General.Map.Map.Sectors.First());
        form = new PastePropertiesOptionsForm();
        Assert.True(form.Setup(MapElementType.SECTOR));
        bool before = SectorProperties.CopySettings.FloorHeight;
        WhenShown<SimpleDialog>(dialog =>
        {
            var boxes = All<Avalonia.Controls.CheckBox>((Avalonia.Controls.Control)dialog.Content);
            Assert.NotEmpty(boxes);
            boxes.First(b => b.Content as string == "Floor height").IsChecked = !before;
            Click(dialog.OkButton);
        });

        Assert.Equal(DialogResult.OK, form.ShowDialog());
        Assert.Equal(!before, SectorProperties.CopySettings.FloorHeight);
        SectorProperties.CopySettings.FloorHeight = before;
    }

    [AvaloniaFact]
    public void Make_door_returns_the_chosen_textures_and_options()
    {
        OpenEditor();
        var form = new MakeDoorForm();
        WhenShown<SimpleDialog>(dialog => Click(dialog.OkButton));

        Assert.Equal(DialogResult.OK, form.Show(null, "BIGDOOR2", "DOORTRAK", "CEIL1_1", "FLOOR4_8", true, false, true));
        Assert.Equal("BIGDOOR2", form.DoorTexture);
        Assert.Equal("DOORTRAK", form.TrackTexture);
        Assert.Equal("CEIL1_1", form.CeilingTexture);
        Assert.Equal("FLOOR4_8", form.FloorTexture);
        Assert.True(form.ResetOffsets);
        Assert.False(form.ApplyActionSpecials);
        Assert.True(form.ApplyTag);
    }

    [AvaloniaFact]
    public void Make_door_stays_open_without_a_door_texture()
    {
        OpenEditor();
        General.Dialogs = new CodeImp.DoomBuilder.Windows.NoDialogs();        // the warning must not open a message box of its own
        var form = new MakeDoorForm();
        WhenShown<SimpleDialog>(dialog =>
        {
            Click(dialog.OkButton);
            Assert.True(dialog.IsVisible);                                    // refused
            Click(dialog.CancelButton);
        });

        Assert.Equal(DialogResult.Cancel, form.Show(null, "", "DOORTRAK", "CEIL1_1", "FLOOR4_8", true, true, true));
    }
}
