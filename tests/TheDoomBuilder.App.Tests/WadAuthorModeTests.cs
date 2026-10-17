using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The WadAuthor mode: highlights what is under the mouse (vertex, thing, line or sector) and offers a menu for the lines.</summary>
public class WadAuthorModeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private Point DisplayPointOf(Vector2D mappos)
    {
        Vector2D d = Renderer.MapToDisplay(mappos);
        return InView(d.x, d.y);
    }

    [AvaloniaFact]
    public void The_mode_highlights_what_is_under_the_mouse()
    {
        OpenEditor();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "WadAuthorMode");
        General.Actions.InvokeAction("wadauthormode_wadauthormode");
        Flush();
        Assert.Equal("WadAuthorMode", General.Editing.Mode.GetType().Name);

        // Over a vertex: the vertex
        var vertex = General.Map.Map.Vertices.First();
        window.MouseMove(DisplayPointOf(vertex.Position));
        Flush();
        Assert.Same(vertex, General.Editing.Mode.HighlightedObject ?? Highlighted());

        // Over the middle of a line: the line
        var line = General.Map.Map.Linedefs.First();
        window.MouseMove(DisplayPointOf((line.Start.Position + line.End.Position) * 0.5));
        Flush();
        Assert.IsType<Linedef>(Highlighted());

        // Away from the mouse: nothing
        General.Editing.Mode.OnMouseLeave(System.EventArgs.Empty);
        Assert.Null(Highlighted());
    }

    private static object Highlighted() => General.Editing.Mode.GetType().GetField("highlighted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(General.Editing.Mode);

    [AvaloniaFact]
    public void The_linedef_menu_has_the_entries_of_UDB()
    {
        var tools = new CodeImp.DoomBuilder.BuilderModes.Editing.WAuthorTools();
        var headers = tools.Menu.Items.OfType<Avalonia.Controls.MenuItem>().Select(m => (string)m.Header).ToList();
        Assert.Equal(new[] { "Properties...", "Delete", "Split", "Flip", "Curve..." }, headers);
    }
}
