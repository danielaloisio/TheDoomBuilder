using System;
using System.Runtime.InteropServices;
using CodeImp.DoomBuilder.Geometry;

namespace DoomBuilder.App.Input;

/// <summary>A source of relative mouse movement that does not depend on where the pointer is.</summary>
public interface IRelativeMotionSource : IDisposable
{
    /// <summary>Movement since the last call, in the device's units (like the raw input UDB reads on Windows).</summary>
    Vector2D Poll();
}

/// <summary>
/// Relative pointer movement from the X server's XInput2 raw events (XI_RawMotion), on a private connection to the display. It works on
/// plain X11 and on XWayland, where moving the pointer (XWarpPointer) only changes the X server's own idea of where it is while the real
/// pointer stays put, so any scheme that measures positions and recenters ends up adding the jump back to the real pointer to the
/// movement and turns the view by itself. Raw motion is the movement of the device, whatever happens to the position.
/// </summary>
public sealed class X11RawMotion : IRelativeMotionSource
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXi = "libXi.so.6";

    private const int GenericEvent = 35;
    private const int XI_RawMotion = 17;
    private const int XIAllMasterDevices = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct XIEventMask { public int deviceid; public int mask_len; public IntPtr mask; }

    [DllImport(LibX11)] private static extern IntPtr XOpenDisplay(IntPtr name);
    [DllImport(LibX11)] private static extern int XCloseDisplay(IntPtr display);
    [DllImport(LibX11)] private static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport(LibX11)] private static extern bool XQueryExtension(IntPtr display, string name, out int opcode, out int firstevent, out int firsterror);
    [DllImport(LibX11)] private static extern int XPending(IntPtr display);
    [DllImport(LibX11)] private static extern int XNextEvent(IntPtr display, IntPtr eventbuffer);
    [DllImport(LibX11)] private static extern bool XGetEventData(IntPtr display, IntPtr cookie);
    [DllImport(LibX11)] private static extern void XFreeEventData(IntPtr display, IntPtr cookie);
    [DllImport(LibX11)] private static extern int XFlush(IntPtr display);
    [DllImport(LibXi)] private static extern int XIQueryVersion(IntPtr display, ref int major, ref int minor);
    [DllImport(LibXi)] private static extern int XISelectEvents(IntPtr display, IntPtr window, ref XIEventMask masks, int nmasks);

    private IntPtr display;
    private readonly int opcode;
    private readonly IntPtr buffer = Marshal.AllocHGlobal(192);   // an XEvent

    private X11RawMotion(IntPtr display, int opcode)
    {
        this.display = display;
        this.opcode = opcode;
    }

    /// <summary>Null when there is no X server, no XInput 2.0 or the selection failed.</summary>
    public static X11RawMotion TryCreate()
    {
        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) return null;
            IntPtr display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return null;

            if (!XQueryExtension(display, "XInputExtension", out int opcode, out _, out _)) { XCloseDisplay(display); return null; }
            int major = 2, minor = 0;
            if (XIQueryVersion(display, ref major, ref minor) != 0 || major < 2) { XCloseDisplay(display); return null; }

            // Raw motion of every master pointer, selected on the root window (raw events are not tied to a window)
            byte[] bits = new byte[(XI_RawMotion >> 3) + 1];
            bits[XI_RawMotion >> 3] |= (byte)(1 << (XI_RawMotion & 7));
            IntPtr mask = Marshal.AllocHGlobal(bits.Length);
            Marshal.Copy(bits, 0, mask, bits.Length);
            var selection = new XIEventMask { deviceid = XIAllMasterDevices, mask_len = bits.Length, mask = mask };
            int result = XISelectEvents(display, XDefaultRootWindow(display), ref selection, 1);
            Marshal.FreeHGlobal(mask);
            XFlush(display);
            if (result != 0) { XCloseDisplay(display); return null; }

            var source = new X11RawMotion(display, opcode);
            source.Poll();      // whatever happened before is not movement
            return source;
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
        {
            return null;
        }
    }

    public Vector2D Poll()
    {
        double dx = 0, dy = 0;
        if (display == IntPtr.Zero) return new Vector2D();

        while (XPending(display) > 0)
        {
            XNextEvent(display, buffer);
            if (Marshal.ReadInt32(buffer, 0) != GenericEvent) continue;

            // XGenericEventCookie: type 0, serial 8, send_event 16, display 24, extension 32, evtype 36, cookie 40, data 48
            if (Marshal.ReadInt32(buffer, 32) != opcode || Marshal.ReadInt32(buffer, 36) != XI_RawMotion) continue;
            if (!XGetEventData(display, buffer)) continue;
            try
            {
                IntPtr raw = Marshal.ReadIntPtr(buffer, 48);
                ReadRawValues(raw, ref dx, ref dy);
            }
            finally { XFreeEventData(display, buffer); }
        }

        return new Vector2D(dx, dy);
    }

    // XIRawEvent: valuators.mask_len at 64, valuators.mask at 72, raw_values at 88. raw_values holds one number for each valuator whose
    // bit is set in the mask, in order of the valuators: 0 is x, 1 is y.
    private static void ReadRawValues(IntPtr raw, ref double dx, ref double dy)
    {
        int masklen = Marshal.ReadInt32(raw, 64);
        IntPtr mask = Marshal.ReadIntPtr(raw, 72);
        IntPtr values = Marshal.ReadIntPtr(raw, 88);
        if (mask == IntPtr.Zero || values == IntPtr.Zero || masklen <= 0) return;

        int index = 0;
        for (int valuator = 0; valuator < Math.Min(2, masklen * 8); valuator++)
        {
            if ((Marshal.ReadByte(mask, valuator >> 3) & (1 << (valuator & 7))) == 0) continue;
            double value = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(values, index * 8));
            if (valuator == 0) dx += value; else dy += value;
            index++;
        }
    }

    public void Dispose()
    {
        if (display != IntPtr.Zero)
        {
            XCloseDisplay(display);
            display = IntPtr.Zero;
        }
        Marshal.FreeHGlobal(buffer);
    }
}
