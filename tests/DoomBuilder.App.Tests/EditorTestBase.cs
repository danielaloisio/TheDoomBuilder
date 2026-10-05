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

/// <summary>Opens the real MainWindow on the sample map with isolated assets and settings, and tears everything down.</summary>
public abstract class EditorTestBase : IDisposable
{
    protected readonly string dir = Path.Combine(Path.GetTempPath(), "udb-app-" + Guid.NewGuid().ToString("N"));
    protected MainWindow window;

    private Exception deferred;
    private readonly System.Collections.Generic.List<DispatcherTimer> timers = new System.Collections.Generic.List<DispatcherTimer>();

    /// <summary>When a dialog of type T appears over the main window, runs <paramref name="act"/> on it (once).</summary>
    protected void WhenShown<T>(Action<T> act) where T : Window
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        timers.Add(timer);
        timer.Tick += (s, e) =>
        {
            var dialog = window.OwnedWindows.OfType<T>().FirstOrDefault();
            if (dialog == null || !dialog.IsVisible) return;
            timer.Stop();
            // A failed assertion in here would leave the dialog (and the nested loop waiting for it) open forever: close it and report at the end
            try { act(dialog); }
            catch (Exception ex) { deferred ??= ex; if (dialog.IsVisible) dialog.Close(false); }
        };
        timer.Start();
    }

    protected static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    public virtual void Dispose()
    {
        foreach (var t in timers) t.Stop();
        // Closing asks to save a changed map; a test that left changes behind must not hang its teardown on that question
        // (IsChanged can be recomputed, so also swap in a service that answers "No" without showing anything)
        if (General.Map != null) General.Map.IsChanged = false;
        General.Dialogs = new CodeImp.DoomBuilder.Windows.NoDialogs();
        if (window != null && window.IsVisible)
        {
            window.Close();                         // the real shutdown path (cancels, then closes from the dispatcher)
            Dispatcher.UIThread.RunJobs();
        }
        General.ShutdownHeadless();
        if (deferred != null) { var failure = deferred; deferred = null; System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
        General.BuiltInPluginAssemblies.Clear();
        Program.Arguments = Array.Empty<string>();
        Program.ApplicationDirectory = null;
        Program.SettingsDirectory = null;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { }
    }

    /// <summary>A WAD with MAP01 as UDMF text (for the game configurations that edit UDMF maps).</summary>
    protected string WriteUdmfWad(string textmap)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "udmf.wad");
        using (var wad = new CodeImp.DoomBuilder.IO.WAD(path))
        {
            void Add(string name, byte[] data)
            {
                var lump = wad.Insert(name, wad.Lumps.Count, data.Length);
                lump.Stream.Write(data, 0, data.Length);
            }
            Add("MAP01", Array.Empty<byte>());
            Add("TEXTMAP", System.Text.Encoding.ASCII.GetBytes(textmap));
            Add("ENDMAP", Array.Empty<byte>());
            wad.WriteHeaders();
        }
        return path;
    }

    /// <summary>Two sectors, a few lines and things: enough to edit.</summary>
    protected const string UdmfSample = @"
namespace = ""zdoom"";
vertex { x = 0.0; y = 0.0; }
vertex { x = 128.0; y = 0.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 0.0; y = 128.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 8; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; id = 5; }
thing { x = 64.0; y = 64.0; type = 1; angle = 90; skill1 = true; skill2 = true; single = true; }
";

    protected static CodeImp.DoomBuilder.Rendering.IRenderer2D Renderer => General.Map.Renderer2D;

    protected static string FindRepoFile(params string[] parts)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string candidate = Path.Combine(new[] { d.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(Path.Combine(parts));
    }

    /// <summary>Opens the main window on the sample map and waits until the editor has started and loaded it.</summary>
    protected void OpenEditor(bool withMap = true, string wadPath = null, string config = "Doom_DoomDoom.cfg")
    {
        Directory.CreateDirectory(dir);

        // The packaging layout: assets/Common + the platform folder, in one folder
        string app = Path.Combine(dir, "app");
        CopyDirectory(FindRepoFile("assets", "Common"), app);
        CopyDirectory(FindRepoFile("assets", OperatingSystem.IsWindows() ? "Windows" : "Linux"), app);

        Program.ApplicationDirectory = app;
        Program.SettingsDirectory = Path.Combine(dir, "settings");
        Program.Arguments = withMap
            ? new[] { wadPath ?? FindRepoFile("assets", "samples", "sample.wad"), "-map", "MAP01", "-cfg", config, "-nosettings" }
            : new[] { "-nosettings" };

        window = new MainWindow();

        // Startup warnings open the (modal) errors window by default, like in the application: nobody is there to read it
        var startup = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        startup.Tick += (s, e) => window.OwnedWindows.OfType<DoomBuilder.App.Dialogs.ErrorsWindow>().FirstOrDefault()?.Close();
        startup.Start();
        window.Show();

        // StartEditor is posted from Opened; it opens the map synchronously
        for (int i = 0; i < 400 && (withMap ? General.Map == null : General.Actions == null); i++)
        {
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(10);
        }
        if (withMap)
        {
            Assert.NotNull(General.Map);
            Assert.NotNull(General.Editing.Mode);
        }
        Assert.NotNull(General.Actions);
        Dispatcher.UIThread.RunJobs();
        startup.Stop();
        if (General.Settings != null) General.Settings.ShowErrorsWindow = false;   // later maps would open it too; tests that want it call ShowErrors()
    }

    /// <summary>A position in the display (viewport) as the window's coordinates, which is what synthetic input uses.</summary>
    protected Point InView(double x, double y)
    {
        var surface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        return surface.TranslatePoint(new Point(x, y), window) ?? new Point(x, y);
    }

    /// <summary>The center of the display in window coordinates.</summary>
    protected Point ViewCenter()
    {
        var surface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        return InView(surface.Bounds.Width / 2, surface.Bounds.Height / 2);
    }

    /// <summary>MainForm.UpdateInterface: the shell refreshes its menus from the editor state.</summary>
    protected void RefreshShell()
    {
        General.MainWindow.UpdateInterface();
        Dispatcher.UIThread.RunJobs();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string f in Directory.GetFiles(source)) File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);
        foreach (string d in Directory.GetDirectories(source)) CopyDirectory(d, Path.Combine(target, Path.GetFileName(d)));
    }
}

