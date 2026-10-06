using System;
using System.Diagnostics;
using CodeImp.DoomBuilder.Geometry;

namespace DoomBuilder.App.Input;

/// <summary>
/// Turns absolute pointer positions into relative movement for the exclusive (3D) mouse mode.
///
/// The movement of an event is always the difference to the previous position, so it can never be larger than what the mouse really
/// travelled. The pointer is warped back to the center of the view only when it has wandered far from it (to keep it from reaching
/// the edge of the screen), and the jump that the warp itself causes is recognized and skipped. If warping does nothing in this
/// session (XWayland ignores it unless the pointer is grabbed) the warps are given up, and the movement is still the difference
/// between positions: the pointer may reach the edge of the screen, but the view never turns by itself.
///
/// (An earlier version measured the distance to the center for every event and warped after each one. When the warp did not bring the
/// pointer exactly to the center, every event added that distance again and the camera spun as soon as the mouse moved.)
/// </summary>
public sealed class RelativePointerTracker
{
    /// <summary>How long after asking for a warp its jump can still arrive (ms).</summary>
    public const long EchoWindowMilliseconds = 60;

    /// <summary>How far from the center, in fractions of the distance from the center to the nearest edge, before the pointer is brought back.</summary>
    public const double FarFraction = 0.5;

    // A jump to the center is at least this long, and covers at least this part of the distance the pointer was from the center
    private const double MinimumJump = 30.0;
    private const double MinimumJumpFraction = 0.6;

    // After this many warps in a row that nothing answered, warping is given up
    private const int UnansweredLimit = 3;

    private readonly Func<long> clock;
    private double centerx, centery;
    private double accumx, accumy;
    private double lastx, lasty;
    private bool haslast;
    private bool warppending;
    private long warpedat;
    private int unanswered;

    public RelativePointerTracker() : this(() => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency) { }

    /// <param name="clock">Milliseconds; a test can supply its own.</param>
    public RelativePointerTracker(Func<long> clock) { this.clock = clock; }

    /// <summary>
    /// False when the platform cannot move the pointer (a pure Wayland session), or once it was seen that moving it has no effect:
    /// there is nothing to recenter, and the movement is read between successive positions.
    /// </summary>
    public bool UsesWarp { get; set; } = true;

    /// <summary>Raised once, when the tracker found that the warps do not move the pointer and gave them up.</summary>
    public event Action WarpGivenUp;

    /// <summary>Pointer position (in view pixels) the pointer is returned to when it has wandered off.</summary>
    public void SetCenter(double x, double y)
    {
        centerx = x;
        centery = y;
    }

    private double DistanceToCenter(double x, double y) => Math.Max(Math.Abs(x - centerx), Math.Abs(y - centery));

    /// <summary>
    /// Feed each pointer position. Returns true when the pointer has to be warped back to the center.
    /// </summary>
    public bool Feed(double x, double y)
    {
        bool previous = haslast;
        double px = lastx, py = lasty;
        lastx = x; lasty = y; haslast = true;

        // The first position is only where the pointer is
        if (!previous) return false;

        // The first one after the capture began: a pointer that is not near the center did not get there, so this is not a movement
        if (fresh)
        {
            fresh = false;
            if (DistanceToCenter(x, y) > 100) { warppending = false; return false; }
        }

        if (UsesWarp && warppending)
        {
            bool inwindow = clock() - warpedat <= EchoWindowMilliseconds;
            double before = DistanceToCenter(px, py), after = DistanceToCenter(x, y);
            double came = before - after;       // how much closer to the center the pointer got in this one event

            if (inwindow && came >= MinimumJump && came >= before * MinimumJumpFraction)
            {
                warppending = false;            // the warp: the pointer jumped to the center, which is not a movement
                unanswered = 0;
                return false;
            }
            if (!inwindow)
            {
                // Nothing answered that warp
                warppending = false;
                if (++unanswered >= UnansweredLimit)
                {
                    UsesWarp = false;
                    WarpGivenUp?.Invoke();
                }
            }
        }

        accumx += x - px;
        accumy += y - py;

        if (UsesWarp && !warppending && DistanceToCenter(x, y) > FarFraction * Math.Min(centerx, centery))
        {
            warppending = true;
            warpedat = clock();
            return true;
        }
        return false;
    }

    /// <summary>
    /// The capture starts: the pointer is being put on the center. Movement counts from there; the first position that arrives far
    /// from the center is only where the pointer happens to be (the warp that starts the capture did not take effect, or has not yet),
    /// and the jump that the warp makes when it does is skipped like any other.
    /// </summary>
    public void Begin()
    {
        lastx = centerx; lasty = centery; haslast = true;
        warppending = false;
        unanswered = 0;
        fresh = true;
    }

    private bool fresh;

    /// <summary>Movement in pixels since the last call.</summary>
    public Vector2D Poll()
    {
        Vector2D result = new Vector2D(accumx, accumy);
        accumx = accumy = 0;
        return result;
    }
}
