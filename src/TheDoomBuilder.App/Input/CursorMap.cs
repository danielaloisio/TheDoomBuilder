using Avalonia.Input;
using WinCursor = System.Windows.Forms.Cursor;
using WinCursors = System.Windows.Forms.Cursors;

namespace DoomBuilder.App.Input;

/// <summary>The Core still names cursors the WinForms way (Cursors.Cross...); this maps them to Avalonia's.</summary>
public static class CursorMap
{
    public static Cursor ToAvalonia(WinCursor cursor)
    {
        if (cursor == null || ReferenceEquals(cursor, WinCursors.Default) || ReferenceEquals(cursor, WinCursors.Arrow)) return new Cursor(StandardCursorType.Arrow);
        if (ReferenceEquals(cursor, WinCursors.Cross)) return new Cursor(StandardCursorType.Cross);
        if (ReferenceEquals(cursor, WinCursors.SizeAll)) return new Cursor(StandardCursorType.SizeAll);
        if (ReferenceEquals(cursor, WinCursors.Hand)) return new Cursor(StandardCursorType.Hand);
        if (ReferenceEquals(cursor, WinCursors.IBeam)) return new Cursor(StandardCursorType.Ibeam);
        if (ReferenceEquals(cursor, WinCursors.No)) return new Cursor(StandardCursorType.No);
        if (ReferenceEquals(cursor, WinCursors.WaitCursor)) return new Cursor(StandardCursorType.Wait);
        if (ReferenceEquals(cursor, WinCursors.AppStarting)) return new Cursor(StandardCursorType.AppStarting);
        if (ReferenceEquals(cursor, WinCursors.SizeNS) || ReferenceEquals(cursor, WinCursors.HSplit)) return new Cursor(StandardCursorType.SizeNorthSouth);
        if (ReferenceEquals(cursor, WinCursors.SizeWE) || ReferenceEquals(cursor, WinCursors.VSplit)) return new Cursor(StandardCursorType.SizeWestEast);
        if (ReferenceEquals(cursor, WinCursors.SizeNESW)) return new Cursor(StandardCursorType.TopRightCorner);
        if (ReferenceEquals(cursor, WinCursors.SizeNWSE)) return new Cursor(StandardCursorType.TopLeftCorner);
        return new Cursor(StandardCursorType.Arrow);
    }
}
