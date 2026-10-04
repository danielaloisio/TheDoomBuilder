using CodeImp.DoomBuilder.Geometry;

namespace DoomBuilder.App.Input;

/// <summary>
/// Turns absolute pointer positions into relative movement for the exclusive (3D) mouse mode, the way UDB's Unix fallback
/// did: the pointer is warped back to the center of the view after every movement, and the distance it travelled from the
/// center is the delta. This needs no relative-input API from the toolkit, only "where is the pointer" and "move the pointer".
/// </summary>
public sealed class RelativePointerTracker
{
    private double centerx, centery;
    private double accumx, accumy;

    /// <summary>Pointer position (in view pixels) the pointer is returned to after each movement.</summary>
    public void SetCenter(double x, double y)
    {
        centerx = x;
        centery = y;
    }

    /// <summary>
    /// Feed each pointer position. Returns true when the pointer has to be warped back to the center
    /// (false for the event the warp itself generates, which sits exactly on the center).
    /// </summary>
    public bool Feed(double x, double y)
    {
        double dx = x - centerx, dy = y - centery;
        if (dx == 0 && dy == 0) return false;

        accumx += dx;
        accumy += dy;
        return true;
    }

    /// <summary>Movement in pixels since the last call.</summary>
    public Vector2D Poll()
    {
        Vector2D result = new Vector2D(accumx, accumy);
        accumx = accumy = 0;
        return result;
    }
}
