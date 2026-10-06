using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using Xunit;
using Tags = CodeImp.DoomBuilder.TagExplorer;
using Comments = CodeImp.DoomBuilder.CommentsPanel;
using Range = CodeImp.DoomBuilder.TagRange;

namespace DoomBuilder.App.Tests;

/// <summary>The three small plugins that deal with tags and comments: TagExplorer (docker), TagRange (dialog) and CommentsPanel (docker).</summary>
public class TagPluginsTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // Three rooms side by side (clockwise: the front of a wall is on its right, so this is the inside).
    // Sector 0: tag 7, comment "first room"; sector 1: tag 9, effect 1; sector 2: no tag, the same comment as sector 0.
    // Linedef 0: tag 7, action 11, comment "door line". A zombieman with tag 3 and the comment "boss thing".
    private static string Rooms()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("namespace = \"zdoom\";");
        text.AppendLine("thing { x = 64.0; y = 64.0; type = 1; angle = 0; skill1 = true; skill2 = true; single = true; }");
        text.AppendLine("thing { x = 320.0; y = 64.0; type = 3004; angle = 0; id = 3; comment = \"boss thing\"; skill1 = true; skill2 = true; single = true; }");
        for (int room = 0; room < 3; room++)
        {
            int x = room * 256;
            text.AppendLine($"vertex {{ x = {x}.0; y = 0.0; }}");
            text.AppendLine($"vertex {{ x = {x}.0; y = 128.0; }}");
            text.AppendLine($"vertex {{ x = {x + 128}.0; y = 128.0; }}");
            text.AppendLine($"vertex {{ x = {x + 128}.0; y = 0.0; }}");
        }
        for (int room = 0; room < 3; room++)
            for (int side = 0; side < 4; side++)
            {
                string extra = room == 0 && side == 0 ? " id = 7; special = 11; comment = \"door line\";" : "";
                text.AppendLine($"linedef {{ v1 = {room * 4 + side}; v2 = {room * 4 + (side + 1) % 4}; sidefront = {room * 4 + side}; blocking = true;{extra} }}");
            }
        for (int i = 0; i < 12; i++) text.AppendLine($"sidedef {{ sector = {i / 4}; texturemiddle = \"STARTAN1\"; }}");
        text.AppendLine("sector { heightfloor = 0; heightceiling = 128; texturefloor = \"FLOOR4_8\"; textureceiling = \"CEIL3_5\"; lightlevel = 192; id = 7; comment = \"first room\"; }");
        text.AppendLine("sector { heightfloor = 0; heightceiling = 128; texturefloor = \"FLOOR4_8\"; textureceiling = \"CEIL3_5\"; lightlevel = 192; id = 9; special = 1; }");
        text.AppendLine("sector { heightfloor = 0; heightceiling = 128; texturefloor = \"FLOOR4_8\"; textureceiling = \"CEIL3_5\"; lightlevel = 192; comment = \"first room\"; }");
        return text.ToString();
    }

    private void OpenRooms() => OpenEditor(wadPath: WriteUdmfWad(Rooms()), config: "GZDoom_DoomUDMF.cfg");

    private static Tags.TagExplorer Explorer => Tags.BuilderPlug.Me.Panel;
    private static Comments.CommentsDocker CommentsList => Comments.BuilderPlug.Me.Panel;

    private static Tags.TagTreeNode Category(string text) => Explorer.Roots.Single(r => r.Text == text);

    private static IEnumerable<string> Texts(Tags.TagTreeNode node) => node.Nodes.Select(n => n.Text);

    private static IEnumerable<Tags.TagTreeNode> AllNodes(IEnumerable<Tags.TagTreeNode> nodes)
        => nodes.SelectMany(n => new[] { n }.Concat(AllNodes(n.Nodes)));

    private static Button Named(Window w, string text)
        => w.GetVisualDescendants().OfType<Button>().First(b => (string)b.Content == text && b.IsEffectivelyVisible);

    // ---------------------------------------------------------------- Tag Explorer

    [AvaloniaFact]
    public void The_explorer_docker_is_added_to_the_shell_and_lists_tagged_elements_as_a_tree()
    {
        OpenRooms();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.Contains(shell.Dockers.Dockers, d => d.Title == "Tag Explorer");

        Explorer.UpdateTreeNow();
        Assert.Equal(new[] { "Things:", "Sectors:", "Linedefs:" }, Explorer.Roots.Select(r => r.Text));
        Assert.All(Explorer.Roots, r => Assert.True(r.IsExpanded));

        // Sectors by index: the comment replaces the name; the untagged sector is not listed
        Assert.Equal(2, Category("Sectors:").Nodes.Count);
        Assert.Equal("0: first room, Tag 7", Category("Sectors:").Nodes[0].Text);
        Assert.StartsWith("1: ", Category("Sectors:").Nodes[1].Text);
        Assert.EndsWith(", Tag 9, Action 1", Category("Sectors:").Nodes[1].Text);
        Assert.Single(Category("Linedefs:").Nodes);
        Assert.StartsWith("0: door line, Tag 7", Category("Linedefs:").Nodes[0].Text);

        // Things are grouped by their category
        var things = AllNodes(Category("Things:").Nodes).Where(n => n.Tag != null).ToList();
        Assert.Single(things);
        Assert.Contains("boss thing", things[0].Text);
        Assert.True(Explorer.ExportButton.IsEnabled);
    }

    [AvaloniaFact]
    public void Sorting_by_tag_and_by_action_groups_the_elements_under_their_tag_or_action()
    {
        OpenRooms();
        Explorer.SortModeBox.SelectedIndex = 1;        // by tag
        Assert.Equal(new[] { "Tag 7", "Tag 9" }, Texts(Category("Sectors:")));
        Assert.Equal(new[] { "Tag 7" }, Texts(Category("Linedefs:")));
        Assert.Contains("Tag 3", Texts(Category("Things:")));

        Explorer.SortModeBox.SelectedIndex = 2;        // by action
        Assert.Contains(Category("Linedefs:").Nodes, n => n.Text.StartsWith("11 - "));
        Assert.Contains(Category("Sectors:").Nodes, n => n.Text.StartsWith("1 - "));
    }

    [AvaloniaFact]
    public void The_display_mode_and_the_filter_narrow_the_tree()
    {
        OpenRooms();
        Explorer.DisplayMode.SelectedIndex = 2;        // action specials only: the sector with tag 7 and no effect disappears
        Assert.Single(Category("Sectors:").Nodes);
        Assert.DoesNotContain(Explorer.Roots, r => r.Text == "Things:");
        Explorer.DisplayMode.SelectedIndex = 0;

        Explorer.Search.Text = "#9";
        Assert.Single(Category("Sectors:").Nodes);
        Assert.Contains("Tag 9", Category("Sectors:").Nodes[0].Text);
        Assert.DoesNotContain(Explorer.Roots, r => r.Text == "Linedefs:");

        Explorer.Search.Text = "$11";
        Assert.DoesNotContain(Explorer.Roots, r => r.Text == "Sectors:");
        Assert.Single(Category("Linedefs:").Nodes);

        Explorer.Search.Text = "boss";                 // text: finds comments
        Assert.Single(AllNodes(Explorer.Roots), n => n.Tag != null);

        Explorer.ClearSearchButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("", Explorer.Search.Text);
        Assert.Equal(3, Explorer.Roots.Count);

        Explorer.CommentsOnly.IsChecked = true;        // only elements that have a comment
        Assert.Single(Category("Sectors:").Nodes);
        Assert.Contains("first room", Category("Sectors:").Nodes[0].Text);
    }

    [AvaloniaFact]
    public void Clicking_an_element_selects_it_and_centers_the_view_when_asked()
    {
        OpenRooms();
        Explorer.SelectOnClick.IsChecked = true;
        Explorer.CenterOnSelected.IsChecked = true;
        Explorer.UpdateTreeNow();
        float x = Renderer.OffsetX;

        Assert.Equal(320, x, 1);                                // the view starts at the center of the map
        var sectornode = Category("Sectors:").Nodes[0];       // the room at x = 0..128
        Explorer.NodeClick(sectornode, false);
        Assert.Equal("SectorsMode", General.Editing.Mode.GetType().Name);
        Assert.Equal(new[] { 0 }, General.Map.Map.GetSelectedSectors(true).Select(s => s.Index));
        Assert.Equal(64, Renderer.OffsetX, 1);                 // centered on the room

        Explorer.NodeClick(Category("Linedefs:").Nodes[0], false);
        Assert.Equal("LinedefsMode", General.Editing.Mode.GetType().Name);
        Assert.Equal(new[] { 0 }, General.Map.Map.GetSelectedLinedefs(true).Select(l => l.Index));

        var thing = AllNodes(Category("Things:").Nodes).First(n => n.Tag != null);
        Explorer.NodeClick(thing, false);
        Assert.Equal("ThingsMode", General.Editing.Mode.GetType().Name);
        Assert.Single(General.Map.Map.GetSelectedThings(true));
    }

    [AvaloniaFact]
    public void Clicking_does_not_change_the_selection_unless_select_on_click_is_on()
    {
        OpenRooms();
        Explorer.UpdateTreeNow();
        Explorer.NodeClick(Category("Sectors:").Nodes[0], false);
        Assert.Empty(General.Map.Map.GetSelectedSectors(true));
    }

    [AvaloniaFact]
    public void A_right_click_opens_the_properties_of_the_element()
    {
        OpenRooms();
        Explorer.UpdateTreeNow();
        bool shown = false;
        WhenShown<SectorEditWindow>(d =>
        {
            shown = true;
            d.Tag.ValidateTag();
            Assert.Equal(9, d.Tag.GetTag(0));                  // the second tagged sector
            Click(Named(d, "Cancel"));
        });
        Explorer.NodeClick(Category("Sectors:").Nodes[1], true);
        Assert.True(shown);
    }

    [AvaloniaFact]
    public void A_comment_edited_in_the_tree_is_stored_in_the_element_with_an_undo_level()
    {
        OpenRooms();
        Explorer.UpdateTreeNow();
        var node = Category("Sectors:").Nodes[1];
        var sector = General.Map.Map.GetSectorByIndex(1);
        Assert.False(sector.Fields.ContainsKey("comment"));

        Explorer.EditComment(node, "the second one");
        Assert.Equal("the second one", sector.Fields["comment"].Value.ToString());
        Assert.Equal("Set comment", General.Map.UndoRedo.NextUndo.Description);
        Assert.Contains("1: the second one, Tag 9", Texts(Category("Sectors:")).Last());   // the tree was rebuilt

        Explorer.EditComment(Category("Sectors:").Nodes[1], "");                          // an empty comment removes it
        Assert.False(sector.Fields.ContainsKey("comment"));
        Assert.Equal("Remove comment", General.Map.UndoRedo.NextUndo.Description);
    }

    [AvaloniaFact]
    public void The_tree_can_be_exported_to_a_text_file()
    {
        OpenRooms();
        Explorer.UpdateTreeNow();
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;

        Click(Explorer.ExportButton);                          // cancelled: nothing is written
        Assert.Single(scripted.FileDialogsShown);
        Assert.EndsWith("_info.txt", scripted.FileDialogsShown[0].FileName);

        string path = Path.Combine(dir, "tags.txt");
        scripted.FilesToChoose.Enqueue(path);
        Click(Explorer.ExportButton);
        string text = File.ReadAllText(path);
        Assert.Contains("Sectors (by index):", text);
        Assert.Contains("  0: first room, Tag 7", text);
        Assert.Contains("Linedefs (by index):", text);
    }

    [AvaloniaFact]
    public void The_explorer_follows_undo_and_the_options_are_kept_between_runs()
    {
        OpenRooms();
        Explorer.SortModeBox.SelectedIndex = 1;
        Explorer.CenterOnSelected.IsChecked = true;
        Explorer.UpdateTreeNow();
        General.Map.Map.GetSectorByIndex(2).Tag = 12;
        Explorer.UpdateTreeNow();
        Assert.Equal(new[] { "Tag 7", "Tag 9", "Tag 12" }, Texts(Category("Sectors:")));

        Tags.BuilderPlug.Me.OnMapCloseBegin();                   // the panel writes its settings
        Assert.Null(Tags.BuilderPlug.Me.Panel);
        Tags.BuilderPlug.Me.OnMapOpenEnd();
        Assert.Equal(1, Explorer.SortModeBox.SelectedIndex);
        Assert.True(Explorer.CenterOnSelected.IsChecked);
    }

    // ---------------------------------------------------------------- Tag Range

    private void SelectSectors(params int[] indices)
    {
        General.Editing.ChangeMode("SectorsMode");
        General.Map.Map.ClearAllSelected();
        foreach (int i in indices) General.Map.Map.GetSectorByIndex(i).Selected = true;
    }

    private static NumberBox[] Numbers(Window d) => d.GetVisualDescendants().OfType<NumberBox>().ToArray();

    private static CheckBox Check(Window d, string text) => d.GetVisualDescendants().OfType<CheckBox>().First(c => (string)c.Content == text);

    [AvaloniaFact]
    public void The_tag_range_action_is_registered_and_its_button_is_on_the_toolbar_only_in_the_modes_with_tags()
    {
        OpenRooms();
        Assert.NotNull(General.Actions.GetActionByName("tagrange_rangetagselection"));
        var tools = Range.BuilderPlug.Me.Tools;

        General.Editing.ChangeMode("VerticesMode");
        Assert.False(tools.ButtonOnToolbar);
        General.Editing.ChangeMode("SectorsMode");
        Assert.True(tools.ButtonOnToolbar);
        Assert.EndsWith("rangetagselection", (string)tools.Button.Tag);
        General.Editing.ChangeMode("LinedefsMode");
        Assert.True(tools.ButtonOnToolbar);
        General.Editing.ChangeMode("VerticesMode");
        Assert.False(tools.ButtonOnToolbar);
    }

    [AvaloniaFact]
    public void A_range_of_tags_is_given_to_the_selected_sectors()
    {
        OpenRooms();
        SelectSectors(0, 1, 2);
        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Create tag range for 3 sectors", d.Title);
            var boxes = Numbers(d);
            Assert.Equal(2, boxes.Length);
            Assert.Equal(General.Map.Map.GetNewTag().ToString(), boxes[0].Text);        // the first tag that is not used
            Check(d, "Relative to existing tags").IsChecked = false;       // (the form remembers the choice of the last run)
            boxes[0].Text = "100";
            boxes[1].Text = "5";
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Flush();
        Assert.True(shown);
        Assert.Equal(new[] { 100, 105, 110 }, General.Map.Map.Sectors.Select(s => s.Tag));
        Assert.Equal("Set 3 sector tags", General.Map.UndoRedo.NextUndo.Description);

        // The step is remembered for the next time
        bool again = false;
        WhenShown<SimpleDialog>(d => { again = true; Assert.Equal("5", Numbers(d)[1].Text); Click(d.CancelButton); });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Assert.True(again);
        Assert.Equal(new[] { 100, 105, 110 }, General.Map.Map.Sectors.Select(s => s.Tag));    // cancel changes nothing
    }

    [AvaloniaFact]
    public void A_relative_range_adds_to_the_tags_the_elements_have()
    {
        OpenRooms();
        SelectSectors(0, 1, 2);
        WhenShown<SimpleDialog>(d =>
        {
            var boxes = Numbers(d);
            Check(d, "Relative to existing tags").IsChecked = true;
            boxes[0].Text = "10";
            boxes[1].Text = "1";
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Flush();
        Assert.Equal(new[] { 17, 20, 12 }, General.Map.Map.Sectors.Select(s => s.Tag));       // 7+10, 9+10+1, 0+10+2
    }

    [AvaloniaFact]
    public void Used_tags_can_be_skipped()
    {
        OpenRooms();
        SelectSectors(1, 2);
        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            var boxes = Numbers(d);
            var skip = Check(d, "Skip over already used tags");
            Check(d, "Relative to existing tags").IsChecked = false;
            Assert.False(skip.IsVisible);

            boxes[0].Text = "7";                                // 7 is the tag of the first room
            boxes[1].Text = "1";
            Assert.True(skip.IsVisible, "the range uses a tag that exists");
            skip.IsChecked = true;
            Assert.Contains(d.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "9");    // 8 and 9: 7 is used, 9 is only the old tag of a selected sector
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Flush();
        Assert.True(shown);
        Assert.Equal(new[] { 7, 8, 9 }, General.Map.Map.Sectors.Select(s => s.Tag));
    }

    [AvaloniaFact]
    public void A_range_that_does_not_fit_cannot_be_made()
    {
        OpenEditor();                                           // a Doom map: its tags stop at 65535
        General.Editing.ChangeMode("SectorsMode");
        var sectors = General.Map.Map.Sectors.ToList();
        Assert.True(sectors.Count >= 2);
        General.Map.Map.ClearAllSelected();
        foreach (var s in sectors) s.Selected = true;
        var before = sectors.Select(s => s.Tag).ToList();

        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            var boxes = Numbers(d);
            Check(d, "Relative to existing tags").IsChecked = false;
            boxes[0].Text = "65535";
            boxes[1].Text = "1";
            Assert.False(d.OkButton.IsEnabled);
            Assert.Contains(d.GetVisualDescendants().OfType<TextBlock>(), t => t.IsVisible && t.Text.StartsWith("The range exceeds"));
            Click(d.CancelButton);
        });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Assert.True(shown);
        Assert.Equal(before, General.Map.Map.Sectors.Select(s => s.Tag));
    }

    [AvaloniaFact]
    public void Without_a_selection_there_is_no_dialog()
    {
        OpenRooms();
        SelectSectors();
        bool shown = false;
        WhenShown<SimpleDialog>(d => { shown = true; Click(d.CancelButton); });
        General.Actions.InvokeAction("tagrange_rangetagselection");
        Flush();
        Assert.False(shown);
    }

    // ---------------------------------------------------------------- Comments

    [AvaloniaFact]
    public void The_comments_docker_only_exists_for_UDMF_maps()
    {
        OpenEditor();                                           // the sample map is a Doom map
        Assert.Null(Comments.BuilderPlug.Me.Panel);
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.DoesNotContain(shell.Dockers.Dockers, d => d.Title == "Comments");
    }

    [AvaloniaFact]
    public void The_list_holds_each_comment_once_with_all_the_elements_that_have_it()
    {
        OpenRooms();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.Contains(shell.Dockers.Dockers, d => d.Title == "Comments");

        CommentsList.UpdateList();
        Assert.Equal(3, CommentsList.CommentCount);
        var first = CommentsList.Comments.Single(c => c.Comment == "first room");
        Assert.Equal(2, first.Elements.Count);                  // two sectors share it
        Assert.All(first.Elements, e => Assert.IsType<Sector>(e));

        // Sorted by comment
        var rows = CommentsList.List.ItemsSource.Cast<Control>().Select(r => ((Comments.CommentInfo)r.Tag).Comment).ToList();
        Assert.Equal(new[] { "boss thing", "door line", "first room" }, rows);

        // Only the comments of the mode that is on
        General.Editing.ChangeMode("SectorsMode");
        CommentsList.FilterMode.IsChecked = true;
        Assert.Equal(new[] { "first room" }, CommentsList.Comments.Select(c => c.Comment));
        General.Editing.ChangeMode("ThingsMode");
        CommentsList.UpdateList();
        Assert.Equal(new[] { "boss thing" }, CommentsList.Comments.Select(c => c.Comment));
    }

    [AvaloniaFact]
    public void A_comment_is_set_on_the_selection_with_one_undo_level()
    {
        OpenRooms();
        SelectSectors(1);
        CommentsList.enabledtimer_Tick(null, EventArgs.Empty);
        Assert.True(CommentsList.AddCommentGroup.IsEnabled);

        CommentsList.AddCommentText.Text = "new one";
        Click(CommentsList.AddCommentButton);
        Assert.Equal("new one", General.Map.Map.GetSectorByIndex(1).Fields["comment"].Value.ToString());
        Assert.Equal("Add comment", General.Map.UndoRedo.NextUndo.Description);
        Assert.Equal("", CommentsList.AddCommentText.Text);
        Assert.Equal(4, CommentsList.CommentCount);

        // Nothing selected: nothing to comment on
        SelectSectors();
        CommentsList.enabledtimer_Tick(null, EventArgs.Empty);
        Assert.False(CommentsList.AddCommentGroup.IsEnabled);
    }

    [AvaloniaFact]
    public void Clicking_a_comment_views_it_and_selects_its_elements_when_asked()
    {
        OpenRooms();
        CommentsList.UpdateList();
        var boss = CommentsList.Comments.Single(c => c.Comment == "boss thing");
        var door = CommentsList.Comments.Single(c => c.Comment == "door line");
        Assert.Equal(320, Renderer.OffsetX, 1);                // the view starts at the center of the map
        CommentsList.ClickComment(door);
        Assert.Equal(0, Renderer.OffsetX, 1);                  // the view moved to the line at x = 0
        Assert.Empty(General.Map.Map.GetSelectedLinedefs(true));
        CommentsList.ClickComment(boss);
        Assert.Equal(320, Renderer.OffsetX, 1);                // and to the thing
        Assert.Empty(General.Map.Map.GetSelectedThings(true));

        CommentsList.ClickSelects.IsChecked = true;
        CommentsList.ClickComment(boss);
        Assert.Equal("ThingsMode", General.Editing.Mode.GetType().Name);
        Assert.Single(General.Map.Map.GetSelectedThings(true));
    }

    [AvaloniaFact]
    public void The_menu_of_a_comment_selects_edits_and_removes_its_elements()
    {
        OpenRooms();
        CommentsList.UpdateList();
        var first = CommentsList.Comments.Single(c => c.Comment == "first room");
        var row = (Control)first.Row;

        Control[] Items() => CommentsList.ContextMenu.ItemsSource.Cast<Control>().ToArray();
        MenuItem Item(string header) => Items().OfType<MenuItem>().First(m => (string)m.Header == header);
        void Press(MenuItem item) => item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        CommentsList.ShowMenu(first, row);
        Assert.Equal(new[] { "Edit Sectors...", "Select", "Select Additive", "Remove Comment" }, Items().OfType<MenuItem>().Select(m => (string)m.Header));

        Press(Item("Select"));
        Assert.Equal(new[] { 0, 2 }, General.Map.Map.GetSelectedSectors(true).Select(s => s.Index).OrderBy(i => i));

        bool edited = false;
        WhenShown<SectorEditWindow>(d => { edited = true; Click(Named(d, "Cancel")); });
        Press(Item("Edit Sectors..."));
        Assert.True(edited);

        CommentsList.ShowMenu(first, row);
        Press(Item("Remove Comment"));
        Assert.False(General.Map.Map.GetSectorByIndex(0).Fields.ContainsKey("comment"));
        Assert.False(General.Map.Map.GetSectorByIndex(2).Fields.ContainsKey("comment"));
        Assert.Equal("Remove 2 comments", General.Map.UndoRedo.NextUndo.Description);
        Assert.Equal(2, CommentsList.CommentCount);
    }
}
