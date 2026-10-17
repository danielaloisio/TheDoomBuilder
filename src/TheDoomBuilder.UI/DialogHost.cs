using System;
using Avalonia.Controls;

namespace DoomBuilder.UI;

/// <summary>
/// How code that has no window of its own (the plugins) shows a modal dialog: over the main window, with the answer on the
/// same stack (see <see cref="DialogPump"/>). The application sets <see cref="Owner"/> when its main window exists.
/// </summary>
public static class DialogHost
{
    /// <summary>The window dialogs open over (null while there is none).</summary>
    public static Func<Window> Owner { get; set; }

    /// <summary>Shows <paramref name="window"/> modally; false when it was closed without an answer or there is no owner.</summary>
    public static bool ShowModal(Window window)
    {
        Window owner = Owner?.Invoke();
        if (owner == null) return false;
        return DialogPump.Run(() => window.ShowDialog<bool>(owner));
    }
}
