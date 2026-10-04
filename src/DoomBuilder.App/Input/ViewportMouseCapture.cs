using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Geometry;

namespace DoomBuilder.App.Input;

/// <summary>
/// Exclusive mouse mode on a viewport: hides the pointer and reports its movement as relative deltas by warping it back to the
/// center of the view after every movement (see <see cref="RelativePointerTracker"/>).
/// </summary>
public sealed class ViewportMouseCapture : IMouseCapture
{
    private readonly MapViewport viewport;
    private readonly IPointerWarp warp;
    private readonly RelativePointerTracker tracker = new RelativePointerTracker();
    private readonly Cursor previouscursor;
    private readonly Control surface;
    private PixelPoint screencenter;

    public ViewportMouseCapture(MapViewport viewport, IPointerWarp warp)
    {
        this.viewport = viewport;
        this.warp = warp;

        previouscursor = viewport.Cursor;
        viewport.Cursor = new Cursor(StandardCursorType.None);

        UpdateCenter();
        warp.MoveTo(screencenter.X, screencenter.Y);

        surface = viewport.InputSurface ?? viewport;
        surface.PointerMoved += OnPointerMoved;
        viewport.SizeChanged += OnSizeChanged;
    }

    // Center of the view in view pixels (what the tracker works in) and in screen pixels (what the warp needs)
    private void UpdateCenter()
    {
        PixelSize size = viewport.PixelSize;
        tracker.SetCenter(size.Width / 2.0, size.Height / 2.0);

        double scale = TopLevel.GetTopLevel(viewport)?.RenderScaling ?? 1.0;
        screencenter = viewport.PointToScreen(new Point(size.Width / 2.0 / scale, size.Height / 2.0 / scale));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCenter();

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        double scale = TopLevel.GetTopLevel(viewport)?.RenderScaling ?? 1.0;
        Point p = e.GetPosition(surface);
        if (tracker.Feed(p.X * scale, p.Y * scale))
            warp.MoveTo(screencenter.X, screencenter.Y);
    }

    public Vector2D Poll() => tracker.Poll();

    public void Dispose()
    {
        surface.PointerMoved -= OnPointerMoved;
        viewport.SizeChanged -= OnSizeChanged;
        viewport.Cursor = previouscursor;

        // Like UDB: leave the pointer in the middle of the view
        warp.MoveTo(screencenter.X, screencenter.Y);
    }
}
