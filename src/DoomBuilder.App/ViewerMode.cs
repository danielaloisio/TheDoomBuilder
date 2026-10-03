using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Plugins;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.App;

/// <summary>The plugin entry for the host application's own edit modes (loaded as a built-in plugin).</summary>
public class ViewerPlug : Plug
{
}

/// <summary>
/// Minimal 2D edit mode that only draws the map. The Renderer2D expects an active edit mode, so this stands in until the
/// real classic modes (BuilderModes: vertices, linedefs, sectors, things) are ported in Phase 5.
/// </summary>
[EditMode(DisplayName = "Viewer",
          SwitchAction = "viewermode",
          ButtonOrder = int.MinValue,
          ButtonGroup = "000_editing",
          Optional = false,
          UseByDefault = true)]
public class ViewerMode : ClassicMode
{
    public override void OnEngage()
    {
        base.OnEngage();
        renderer.SetPresentation(Presentation.Standard);
    }

    public override void OnRedrawDisplay()
    {
        renderer.RedrawSurface();

        if (renderer.StartPlotter(true))
        {
            renderer.PlotLinedefSet(General.Map.Map.Linedefs);
            renderer.PlotVerticesSet(General.Map.Map.Vertices);
            renderer.Finish();
        }

        if (renderer.StartThings(true))
        {
            renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
            renderer.Finish();
        }

        renderer.Present();
    }
}
