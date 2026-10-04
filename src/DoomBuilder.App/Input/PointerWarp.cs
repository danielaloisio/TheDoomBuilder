using System;
using System.Runtime.InteropServices;

namespace DoomBuilder.App.Input;

/// <summary>Moves the OS pointer to a screen position. The one thing exclusive mouse mode needs from the platform.</summary>
public interface IPointerWarp
{
    /// <summary>False when this platform/session cannot move the pointer (a pure Wayland session, for example).</summary>
    bool Supported { get; }

    /// <summary>Moves the pointer to the given screen position, in physical pixels.</summary>
    void MoveTo(int x, int y);
}

public static class PointerWarp
{
    public static IPointerWarp Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPointerWarp();
        if (OperatingSystem.IsMacOS()) return new MacPointerWarp();
        if (OperatingSystem.IsLinux()) return X11PointerWarp.TryCreate() ?? (IPointerWarp)new UnsupportedPointerWarp();
        return new UnsupportedPointerWarp();
    }
}

public sealed class UnsupportedPointerWarp : IPointerWarp
{
    public bool Supported { get { return false; } }
    public void MoveTo(int x, int y) { }
}

public sealed class WindowsPointerWarp : IPointerWarp
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    public bool Supported { get { return true; } }
    public void MoveTo(int x, int y) { SetCursorPos(x, y); }
}

public sealed class MacPointerWarp : IPointerWarp
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint { public double X, Y; }

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(CoreGraphics)]
    private static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    // By default macOS freezes the pointer for a moment after a warp, which would swallow the next movements
    [DllImport(CoreGraphics)]
    private static extern int CGSetLocalEventsSuppressionInterval(double seconds);

    public MacPointerWarp()
    {
        try { CGSetLocalEventsSuppressionInterval(0.0); } catch (Exception) { }
    }

    public bool Supported { get { return true; } }
    public void MoveTo(int x, int y) { CGWarpMouseCursorPosition(new CGPoint { X = x, Y = y }); }
}

/// <summary>X11 (including XWayland): XWarpPointer on a private connection to the display.</summary>
public sealed class X11PointerWarp : IPointerWarp, IDisposable
{
    private const string LibX11 = "libX11.so.6";

    [DllImport(LibX11)] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport(LibX11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(LibX11)] private static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport(LibX11)] private static extern int XWarpPointer(IntPtr display, IntPtr srcw, IntPtr destw, int srcx, int srcy, uint srcwidth, uint srcheight, int destx, int desty);
    [DllImport(LibX11)] private static extern int XFlush(IntPtr display);
    [DllImport(LibX11)] private static extern bool XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootx, out int rooty, out int winx, out int winy, out uint mask);

    private IntPtr display;
    private IntPtr root;

    private X11PointerWarp(IntPtr display)
    {
        this.display = display;
        root = XDefaultRootWindow(display);
    }

    /// <summary>Null when there is no X server to talk to or libX11 is missing.</summary>
    public static X11PointerWarp TryCreate()
    {
        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return null;
            IntPtr display = XOpenDisplay(IntPtr.Zero);
            return display == IntPtr.Zero ? null : new X11PointerWarp(display);
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
        {
            return null;
        }
    }

    public bool Supported { get { return display != IntPtr.Zero; } }

    public void MoveTo(int x, int y)
    {
        if(display == IntPtr.Zero) return;
        XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
        XFlush(display);
    }

    /// <summary>Where the X server says the pointer is (used to verify warping).</summary>
    public (int x, int y) QueryPosition()
    {
        XQueryPointer(display, root, out _, out _, out int rx, out int ry, out _, out _, out _);
        return (rx, ry);
    }

    public void Dispose()
    {
        if (display != IntPtr.Zero)
        {
            XCloseDisplay(display);
            display = IntPtr.Zero;
        }
    }
}
