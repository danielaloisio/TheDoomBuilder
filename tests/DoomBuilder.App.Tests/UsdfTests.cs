using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.USDF;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The USDF plugin: the dialog editor window, offered for the map configurations that have a DIALOGUE lump.</summary>
public class UsdfTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void A_map_without_a_DIALOGUE_lump_has_no_dialog_editor()
    {
        OpenEditor();                                                                    // the Doom format has no DIALOGUE
        Assert.NotNull(General.Actions.GetActionByName("usdf_opendialogeditor"));
        Assert.Null(BuilderPlug.Me.Tools);
    }

    [AvaloniaFact]
    public void The_dialog_editor_opens_once_and_remembers_its_size()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        Assert.NotNull(BuilderPlug.Me.Tools);
        Assert.EndsWith("opendialogeditor", (string)BuilderPlug.Me.Tools.Button.Tag);
        Assert.EndsWith("opendialogeditor", (string)BuilderPlug.Me.Tools.Item.Tag);
        Assert.False(BuilderPlug.Me.EditorOpen);

        General.Actions.InvokeAction("usdf_opendialogeditor");
        Flush();
        Assert.True(BuilderPlug.Me.EditorOpen);
        var editor = BuilderPlug.Me.Editor;
        Assert.Equal("Dialog Editor", editor.Window.Title);
        Assert.True(editor.Window.IsVisible);

        // A second request brings the same window forward
        General.Actions.InvokeAction("usdf_opendialogeditor");
        Flush();
        Assert.Same(editor, BuilderPlug.Me.Editor);

        // Closing it: the next one starts with the size the first had
        editor.Window.Width = 700;
        editor.Window.Close();
        Flush();
        Assert.False(BuilderPlug.Me.EditorOpen);
        General.Actions.InvokeAction("usdf_opendialogeditor");
        Flush();
        Assert.NotSame(editor, BuilderPlug.Me.Editor);
        Assert.Equal(700, BuilderPlug.Me.Editor.Window.Width);
        BuilderPlug.Me.Editor.Window.Close();
    }
}
