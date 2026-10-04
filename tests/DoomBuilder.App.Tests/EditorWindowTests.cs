using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using Xunit;
using DoomBuilder.App.Input;

namespace DoomBuilder.App.Tests;

/// <summary>
/// Drives the real MainWindow with synthetic keyboard and mouse input and checks what the editor does: Avalonia event ->
/// KeyMap -> InputDispatcher -> ActionManager -> edit mode -> Renderer2D. No GPU needed (the GL context never exists, the
/// backend just queues its work), so this runs on every CI platform.
/// </summary>
public class EditorWindowTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-app-" + Guid.NewGuid().ToString("N"));
    private MainWindow window;

    public void Dispose()
    {
        window?.Close();
        General.ShutdownHeadless();
        General.BuiltInPluginAssemblies.Clear();
        Program.Arguments = Array.Empty<string>();
        Program.ApplicationDirectory = null;
        Program.SettingsDirectory = null;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { }
    }

    private static string FindRepoFile(params string[] parts)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(new[] { d.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }

    /// <summary>Opens the main window on the sample map and waits until the editor has started and loaded it.</summary>
    private void OpenEditor()
    {
        Directory.CreateDirectory(dir);

        // The packaging layout: assets/Common + the platform folder, in one folder
        string app = Path.Combine(dir, "app");
        CopyDirectory(FindRepoFile("assets", "Common"), app);
        CopyDirectory(FindRepoFile("assets", "Linux"), app);

        Program.ApplicationDirectory = app;
        Program.SettingsDirectory = Path.Combine(dir, "settings");
        Program.Arguments = new[] { FindRepoFile("assets", "samples", "sample.wad"), "-map", "MAP01", "-cfg", "Doom_DoomDoom.cfg", "-nosettings" };

        window = new MainWindow();
        window.Show();

        // StartEditor is posted from Opened; it opens the map synchronously
        for (int i = 0; i < 400 && General.Map == null; i++)
        {
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(10);
        }
        Assert.NotNull(General.Map);
        Assert.NotNull(General.Editing.Mode);
        Dispatcher.UIThread.RunJobs();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string f in Directory.GetFiles(source)) File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
        foreach (string d in Directory.GetDirectories(source)) CopyDirectory(d, Path.Combine(target, Path.GetFileName(d)));
    }

    private static CodeImp.DoomBuilder.Rendering.IRenderer2D Renderer => General.Map.Renderer2D;

    [AvaloniaFact]
    public void The_sample_map_opens_in_the_viewer_mode_and_is_fitted_to_the_window()
    {
        OpenEditor();

        Assert.IsAssignableFrom<ClassicMode>(General.Editing.Mode);
        Assert.Equal(16, General.Map.Map.Vertices.Count);
        Assert.True(Renderer.Scale > 0);
    }

    [AvaloniaFact]
    public void The_wheel_zooms_in_and_out()
    {
        OpenEditor();
        var over = new Point(window.Width / 2, window.Height / 2);
        window.MouseMove(over);
        float start = Renderer.Scale;

        window.MouseWheel(over, new Vector(0, 1));
        float zoomedin = Renderer.Scale;
        Assert.True(zoomedin > start, $"zoom in: {start} -> {zoomedin}");

        window.MouseWheel(over, new Vector(0, -1));
        window.MouseWheel(over, new Vector(0, -1));
        Assert.True(Renderer.Scale < zoomedin, $"zoom out: {zoomedin} -> {Renderer.Scale}");
    }

    [AvaloniaFact]
    public void Arrow_keys_scroll_the_view_and_Home_fits_the_map_again()
    {
        OpenEditor();
        window.MouseMove(new Point(window.Width / 2, window.Height / 2));   // the display must have the keyboard
        float fitted = Renderer.TranslateX;

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        float scrolled = Renderer.TranslateX;
        Assert.NotEqual(fitted, scrolled);

        window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        window.KeyRelease(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        Assert.Equal(fitted, Renderer.TranslateX, 3);
    }

    [AvaloniaFact]
    public void The_mouse_position_reaches_the_edit_mode_in_view_pixels()
    {
        OpenEditor();
        var mode = (ClassicMode)General.Editing.Mode;

        window.MouseMove(new Point(120, 80));

        Assert.True(General.Interface.MouseInDisplay);
        Assert.Equal(120, mode.MousePos.x, 1);
        Assert.Equal(80, mode.MousePos.y, 1);
    }

    [AvaloniaFact]
    public void Modifier_state_follows_the_keyboard_and_clears_when_the_window_loses_focus()
    {
        OpenEditor();
        window.MouseMove(new Point(50, 50));

        window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, null);
        Assert.True(General.Interface.CtrlState);
        window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.ControlLeft, null);
        Assert.False(General.Interface.CtrlState);
    }
}

public class MouseCaptureTests : IDisposable
{
    private sealed class FakeWarp : IPointerWarp
    {
        public readonly System.Collections.Generic.List<(int x, int y)> Moves = new();
        public bool Supported => true;
        public void MoveTo(int x, int y) => Moves.Add((x, y));
    }

    private MainWindow window;

    public void Dispose() => window?.Close();

    [AvaloniaFact]
    public void Exclusive_mode_reports_relative_movement_and_recenters_the_pointer()
    {
        window = new MainWindow { Width = 600, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var viewport = window.FindControl<MapViewport>("Viewport");
        var surface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        viewport.InputSurface = surface;
        var warp = new FakeWarp();

        using var capture = new DoomBuilder.App.Input.ViewportMouseCapture(viewport, warp);

        // starting a capture puts the pointer in the middle of the view
        Assert.Single(warp.Moves);
        var center = new Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2);

        window.MouseMove(center + new Point(12, -5));
        var delta = capture.Poll();

        Assert.Equal(12, delta.x, 1);
        Assert.Equal(-5, delta.y, 1);
        Assert.Equal(2, warp.Moves.Count);               // moved away from the center: recentered

        // the event the warp itself causes lands on the center and adds nothing
        window.MouseMove(center);
        Assert.Equal(0, capture.Poll().x, 3);
        Assert.Equal(2, warp.Moves.Count);
    }
}
