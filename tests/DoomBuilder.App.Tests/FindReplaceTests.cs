using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The Find and Replace mode (BuilderModes' FindReplaceMode and FindReplaceForm) in the real window.</summary>
public class FindReplaceTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private CodeImp.DoomBuilder.BuilderModes.FindReplaceForm OpenForm()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        General.Actions.InvokeAction("buildermodes_findmode");
        Flush();
        Assert.Equal("FindReplaceMode", General.Editing.Mode.GetType().Name);
        var form = CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.FindReplaceForm;
        Assert.True(form.Window.IsVisible);
        return form;
    }

    private static void Choose(CodeImp.DoomBuilder.BuilderModes.FindReplaceForm form, string type)
    {
        var index = form.SearchTypes.Items.Cast<object>().ToList().FindIndex(o => o.ToString() == type);
        Assert.True(index >= 0, "no search type '" + type + "'");
        form.SearchTypes.SelectedIndex = index;
        Flush();
    }

    private static void Click(Avalonia.Controls.Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Find_selects_what_matches_and_closing_the_mode_hides_the_window()
    {
        var form = OpenForm();
        Assert.False(form.FindButton.IsEnabled);                 // nothing to look for yet

        Choose(form, "Sector Tag");
        form.FindInput.Text = "5";
        Assert.True(form.FindButton.IsEnabled);
        Click(form.FindButton);
        Flush();
        Assert.Equal("1 items found.", form.ResultsCount);
        Assert.Equal(1, form.ResultsList.ItemCount);
        Assert.Single(form.GetSelection());

        Choose(form, "Thing Type");
        form.FindInput.Text = "9999";
        Click(form.FindButton);
        Flush();
        Assert.Equal("0 items found.", form.ResultsCount);

        General.Editing.CancelMode();
        Flush();
        Assert.NotEqual("FindReplaceMode", General.Editing.Mode.GetType().Name);
        Assert.False(form.Window.IsVisible);
    }

    [AvaloniaFact]
    public void Replace_changes_the_tag_and_one_undo_puts_it_back()
    {
        var form = OpenForm();
        var sector = General.Map.Map.Sectors.First();
        Assert.Equal(5, sector.Tag);

        Choose(form, "Sector Tag");
        form.DoReplace.IsChecked = true;
        form.FindInput.Text = "5";
        form.ReplaceInput.Text = "7";
        Click(form.FindButton);
        Flush();
        Assert.Equal("1 items found and replaced.", form.ResultsCount);
        Assert.Equal(7, sector.Tag);

        // Nothing to replace: no undo step is left behind
        form.FindInput.Text = "123";
        Click(form.FindButton);
        Flush();
        Assert.Equal("0 items found and replaced.", form.ResultsCount);
        Assert.Equal("FindReplaceMode", General.Editing.Mode.GetType().Name);

        General.Editing.CancelMode();
        Flush();
        General.Map.UndoRedo.PerformUndo();
        Assert.Equal(5, sector.Tag);
    }
}
