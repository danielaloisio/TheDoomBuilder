using CodeImp.DoomBuilder.Controls;

namespace DoomBuilder.App.Shell;

/// <summary>Makes a <see cref="Docker"/> whose panel is an Avalonia control (what plugins create now that they have no WinForms panels).</summary>
public static class AvaloniaDocker
{
    public static Docker Create(string name, string title, Avalonia.Controls.Control content)
    {
        // The Core's Docker holds its (shim) control; the real Avalonia control travels inside it
        var holder = new System.Windows.Forms.Panel { NativeControl = content };
        return new Docker(name, title, holder);
    }

    /// <summary>The Avalonia control behind a docker, or null for one that has none.</summary>
    public static Avalonia.Controls.Control ContentOf(Docker docker) => docker?.Control?.NativeControl as Avalonia.Controls.Control;
}
