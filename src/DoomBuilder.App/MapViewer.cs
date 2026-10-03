using System;
using System.Drawing;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.App;

/// <summary>
/// View control for the open map: fitting, pan and zoom on the real Renderer2D. Stands in for the classic modes' mouse
/// handling until input is ported (Phase 3).
/// </summary>
internal sealed class MapViewer
{
    private const float MinScale = 0.02f, MaxScale = 50f;

    private float scale = 1f;
    private float centerx, centery;
    private bool viewinitialized;
    private Size lastsize;

    public bool HasMap { get { return General.Map != null && General.Map.Renderer2D != null; } }
    public float Scale { get { return scale; } }

    /// <summary>Call at the start of a frame: positions the view (fits the map the first time, and after resizes).</summary>
    public void PrepareFrame()
    {
        if (!HasMap) return;

        Size size = General.Map.Graphics.ClientSize;
        if (!viewinitialized || size != lastsize)
        {
            if (!viewinitialized) FitToMap(size);
            lastsize = size;
            viewinitialized = true;
            ApplyView();
        }
    }

    /// <summary>Forgets the view so the next map is fitted to the window.</summary>
    public void Reset() { viewinitialized = false; }

    /// <summary>Centers the view on the map so that all of it is visible.</summary>
    public void FitToMap(Size viewsize)
    {
        MapSet map = General.Map.Map;
        if (map.Vertices.Count == 0) { centerx = centery = 0; scale = 1; return; }

        double minx = double.MaxValue, miny = double.MaxValue, maxx = double.MinValue, maxy = double.MinValue;
        foreach (Vertex v in map.Vertices)
        {
            minx = Math.Min(minx, v.Position.x); maxx = Math.Max(maxx, v.Position.x);
            miny = Math.Min(miny, v.Position.y); maxy = Math.Max(maxy, v.Position.y);
        }

        centerx = (float)((minx + maxx) / 2);
        centery = (float)((miny + maxy) / 2);
        double w = Math.Max(maxx - minx, 64), h = Math.Max(maxy - miny, 64);
        scale = (float)Math.Clamp(Math.Min(viewsize.Width / (w * 1.15), viewsize.Height / (h * 1.15)), MinScale, MaxScale);
    }

    /// <summary>Moves the view by a mouse drag, in screen pixels.</summary>
    public void Pan(double dxpixels, double dypixels)
    {
        centerx -= (float)(dxpixels / scale);
        centery += (float)(dypixels / scale);
        ApplyView();
    }

    /// <summary>Zooms around a screen position, keeping the map point under the cursor in place.</summary>
    public void ZoomAt(double factor, double screenx, double screeny)
    {
        if (!HasMap) return;
        IRenderer2D renderer = General.Map.Renderer2D;

        Vector2D before = renderer.DisplayToMap(new Vector2D(screenx, screeny));
        scale = Math.Clamp(scale * (float)factor, MinScale, MaxScale);
        ApplyView();
        Vector2D after = renderer.DisplayToMap(new Vector2D(screenx, screeny));

        centerx += (float)(before.x - after.x);
        centery += (float)(before.y - after.y);
        ApplyView();
    }

    private void ApplyView()
    {
        if (!HasMap) return;
        ((Renderer2D)General.Map.Renderer2D).PositionView(centerx, centery);
        ((Renderer2D)General.Map.Renderer2D).ScaleView(scale);
    }
}
