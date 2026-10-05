using System;
using System.IO;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using DoomBuilder.App.Input;
using DoomBuilder.App.Shell;
using DoomBuilder.App.Dialogs;
using Silk.NET.OpenGL;
using SkiaSharp;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Application = Avalonia.Application;

namespace DoomBuilder.App;

public partial class MainWindow : Avalonia.Controls.Window
{
    private readonly AvaloniaShell shell;
    private readonly ShellUi ui;
    private bool started;
    private int framecount;

    public MainWindow() : this(true)
    {
    }

    /// <param name="startEditor">False builds the window (viewport, input wiring) without starting the editor core: for tests of the UI parts alone.</param>
    public MainWindow(bool startEditor)
    {
        InitializeComponent();

        shell = new AvaloniaShell(Viewport) { WindowIsActive = () => IsActive };
        ui = new ShellUi(new ShellCommands(exit: Close, openWebsite: ShellCommands.OpenWebsiteInBrowser));
        MenuHost.Content = ui.Menu;
        ToolbarHost.Content = ui.Toolbar;
        ModesHost.Content = ui.ModesBar;
        ModeControlsHost.Content = ui.ModeControlsBar;
        plugins = new PluginUi(ui);              // the menus and buttons of the plugins
        shell.Plugins = plugins;

        // The side panel goes in front of the display (the last child of a DockPanel takes the rest of the space)
        dockers = new DockerPanel(shell.Dockers) { IsVisible = false };
        var layout = (DockPanel)Content;
        layout.Children.Insert(layout.Children.IndexOf(InputSurface), dockers);
        shell.Dockers.Changed += () => Dispatcher.UIThread.Post(ApplyDockers);

        // The tooltip modes ask for over the display (comments of things, linedefs and sectors)
        displayTip = new DisplayToolTip();
        InputSurface.Children.Add(displayTip);
        shell.Display.ToolTipRequested += (title, text, x, y) => Dispatcher.UIThread.Post(() => displayTip.ShowAt(title, text, x, y, RenderScaling, InputSurface.Bounds.Size));
        shell.Display.ToolTipHidden += () => Dispatcher.UIThread.Post(displayTip.Hide);

        // The info of the element under the mouse
        infoPanel = new InfoPanel();
        InfoHost.Content = infoPanel;
        shell.InfoChanged += info => Dispatcher.UIThread.Post(() =>
        {
            if (info == null) infoPanel.IdleText = General.Editing?.Mode?.Attributes?.DisplayName ?? "";
            infoPanel.Show(info);
        });

        // Offsets that plugins give in display pixels follow the screen's scale
        UpdateDpiScaler();
        ScalingChanged += (s, e) => UpdateDpiScaler();
        Opened += (s, e) => UpdateDpiScaler();

        // The command palette floats over the top of the display
        palette = new CommandPalette();
        InputSurface.Children.Add(palette);
        shell.CommandPaletteRequested += () => Dispatcher.UIThread.Post(() => { if (EditorRunning) palette.Open(); });
        palette.Closed += () => Focus();
        AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (palette.IsVisible && e.Source is Visual v && !palette.IsVisualAncestorOf(v) && v != palette) palette.Close();
        }, RoutingStrategies.Tunnel);

        shell.StatusChanged += text => StatusText.Text = text;
        shell.HintsChanged += text => HintsText.Text = RtfText.ToPlain(text).Replace('\n', ' ');
        shell.ZoomChanged += scale => ZoomText.Text = (int)Math.Round(scale * 100) + "%";
        shell.GridChanged += size => GridText.Text = size == 0 ? "--" : size + " mp";
        shell.CoordinatesChanged += (coords, snapped) => CoordsText.Text = $"{coords.x:0}, {coords.y:0}";
        shell.WarningsChanged += (count, blink) => WarningsText.Text = count.ToString();
        WarningsText.Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand);
        WarningsText.PointerPressed += (s, e) => { if (EditorRunning) shell.ShowErrors(); };
        shell.InterfaceChanged += RefreshInterface;
        shell.RecentFilesChanged += ShowRecentFiles;
        Closing += OnClosing;
        PositionChanged += (s, e) => RememberNormalBounds();
        PropertyChanged += (s, e) => { if (e.Property == WidthProperty || e.Property == HeightProperty) RememberNormalBounds(); };
        Closed += (s, e) => General.ExitRequested -= OnExitRequested;

        Viewport.Paint += OnPaint;
        Viewport.ContextFailed += reason => StatusText.Text = "OpenGL is not available: " + reason;
        Viewport.FramePainted += OnFramePainted;

        // Keyboard: tunnel so keys reach the editor wherever the focus is inside the window
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (s, e) => { if (EditorRunning) shell.Input.ReleaseAllKeys(); };

        // Mouse
        Viewport.InputSurface = InputSurface;
        InputSurface.PointerEntered += OnPointerEntered;
        InputSurface.PointerExited += (s, e) => { displayTip.Hide(); shell.Input.MouseLeave(EventArgs.Empty); };
        InputSurface.PointerPressed += OnPointerPressed;
        InputSurface.PointerReleased += OnPointerReleased;
        InputSurface.PointerMoved += OnPointerMoved;
        InputSurface.PointerWheelChanged += OnPointerWheel;

        if (startEditor) Opened += (s, e) => Dispatcher.UIThread.Post(StartEditor, DispatcherPriority.Background);
    }

    private readonly DisplayToolTip displayTip;
    private readonly InfoPanel infoPanel;

    private void UpdateDpiScaler()
    {
        float scale = (float)RenderScaling;
        CodeImp.DoomBuilder.Windows.MainForm.DPIScaler = new System.Drawing.SizeF(scale, scale);
    }

    // MainForm.UpdateInterface: title, menus, toolbar and the status bar's config label
    private readonly PluginUi plugins;
    private readonly DockerPanel dockers;

    /// <summary>The menus and buttons plugins added (tests look into it).</summary>
    internal PluginUi Plugins => plugins;
    private readonly CommandPalette palette;

    /// <summary>The command palette (tests drive it).</summary>
    internal CommandPalette Palette => palette;

    /// <summary>The side panel of the dockers (tests look into it).</summary>
    internal DockerPanel Dockers => dockers;

    // Shown when a map is open and there is at least one docker; on the side the settings say (0 left, 1 right, 2 hidden)
    private void ApplyDockers()
    {
        int position = General.Settings?.DockersPosition ?? 1;
        bool show = position != 2 && General.Map != null && shell.Dockers.Dockers.Count > 0;
        dockers.IsVisible = show;
        if (!show) return;

        DockPanel.SetDock(dockers, position == 0 ? Dock.Left : Dock.Right);
        dockers.SetSide(position != 0);
        dockers.Width = Math.Max(160, General.Settings?.DockersWidth ?? 250);
    }

    private void RefreshInterface()
    {
        ui.Refresh();
        plugins.Refresh();
        ApplyDockers();

        InfoHost.IsVisible = General.Map != null && shell.IsInfoPanelExpanded;

        string program = "TheDoomBuilder";
        if (General.Map != null)
        {
            string maptitle = !string.IsNullOrEmpty(General.Map.Data?.MapInfo?.Title) ? ": " + General.Map.Data.MapInfo.Title : "";
            Title = (General.Map.IsChanged ? "\u25CF " : "") + General.Map.FileTitle + " (" + General.Map.Options.CurrentName + maptitle + ") - " + program;
            ConfigText.Text = General.Map.ConfigSettings?.Name ?? "";
        }
        else
        {
            Title = program;
            ConfigText.Text = "";
        }
    }

    // Terminate(true) comes from our own close (already closing); Terminate(false) is a fatal error: close the window
    private void OnExitRequested(bool proper)
    {
        if (!proper) { terminating = true; Dispatcher.UIThread.Post(Close); }
    }

    private void ShowRecentFiles() => ui.SetRecentFiles(shell.Recent.Files, OpenRecent);

    // MainForm.recentitem_Click
    private static void OpenRecent(string filename)
    {
        string existing = CodeImp.DoomBuilder.Windows.RecentFiles.FindExistingFile(filename);
        if (existing == null)
        {
            General.Interface.DisplayStatus(CodeImp.DoomBuilder.Windows.StatusType.Warning, $"The file '{filename}' could not be found.");
            return;
        }
        General.OpenMapFile(existing, null);
    }

    // MainForm.OnFormClosing: the map must be closed (asking to save) before the program ends. A modal question cannot be
    // asked from inside Closing (the window is already on its way out), so the close is cancelled, asked about from the
    // dispatcher, and requested again once the user agreed.
    private bool terminating, closeapproved, closeinprogress;

    // The core exists between a successful Startup and Terminate; input arriving outside that has nothing to talk to
    private bool EditorRunning => started && !terminating && General.Actions != null;

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (!started || terminating || closeapproved) return;

        e.Cancel = true;
        if (closeinprogress) return;       // already asking
        closeinprogress = true;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (!General.CloseMap()) return;       // the user chose Cancel (or saving failed): stay open

                General.WriteLogLine("Closing main interface window...");
                shell.Input.StopExclusiveMouseInput();
                shell.Input.StopProcessing();
                shell.Recent.Save();
                CapturePlacement().Save();

                closeapproved = true;
                General.Terminate(true);
                terminating = true;
                Close();
            }
            finally { closeinprogress = false; }
        });
    }

    // The size and position of the last session, on a screen that still exists
    private void RestorePlacement()
    {
        var saved = CodeImp.DoomBuilder.Windows.WindowPlacement.Load();
        if (saved == null) return;

        var screens = new System.Collections.Generic.List<int[]>();
        foreach (var screen in Screens.All) screens.Add(new[] { screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height });
        var fit = saved.FitTo(screens);

        WindowState = WindowState.Normal;
        Position = new PixelPoint(fit.X, fit.Y);
        Width = fit.Width / RenderScaling;
        Height = fit.Height / RenderScaling;
        if (fit.Maximized) WindowState = WindowState.Maximized;
    }

    // The restored bounds (not the maximized ones) are what comes back next time
    private CodeImp.DoomBuilder.Windows.WindowPlacement CapturePlacement()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (maximized && lastNormal != null) return new CodeImp.DoomBuilder.Windows.WindowPlacement(lastNormal.X, lastNormal.Y, lastNormal.Width, lastNormal.Height, true);
        return new CodeImp.DoomBuilder.Windows.WindowPlacement(Position.X, Position.Y, (int)Math.Round(Width * RenderScaling), (int)Math.Round(Height * RenderScaling), maximized);
    }

    private CodeImp.DoomBuilder.Windows.WindowPlacement lastNormal;   // bounds of the window while it was not maximized

    private void RememberNormalBounds()
    {
        if (WindowState == WindowState.Normal)
            lastNormal = new CodeImp.DoomBuilder.Windows.WindowPlacement(Position.X, Position.Y, (int)Math.Round(Width * RenderScaling), (int)Math.Round(Height * RenderScaling), false);
    }

    // What MainForm.RedrawDisplay did: let the active edit mode draw
    private void OnPaint()
    {
        if (General.Map == null || General.Editing.Mode == null) return;
        General.Editing.Mode.OnRedrawDisplay();
    }

    private void StartEditor()
    {
        if (started) return;
        started = true;

        string appdir = Program.ApplicationDirectory ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string settingsdir = Program.SettingsDirectory ?? Environment.GetEnvironmentVariable("UDB_SETTINGS_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TheDoomBuilder");
        Directory.CreateDirectory(settingsdir);

        General.ExitRequested += OnExitRequested;                           // the core wants the program to end (fatal errors, Terminate)
        DoomBuilder.UI.DialogHost.Owner = () => this;
        System.Windows.Forms.Clipboard.Provider = new SystemClipboard(() => this);   // copy and paste go through the system clipboard
        General.Dialogs = new AvaloniaDialogs(() => this);                  // the real message boxes, file pickers and map options
        General.BuiltInPluginAssemblies.Add(typeof(ViewerPlug).Assembly);   // the viewer edit mode
        General.BuiltInPluginAssemblies.Add(typeof(CodeImp.DoomBuilder.BuilderModes.BuilderPlug).Assembly);   // vertices, linedefs, sectors, things...

        if (!General.Startup(Program.Arguments, () => shell, appdir, settingsdir))
        {
            StatusText.Text = "Startup failed, see the log in " + settingsdir;
            return;
        }

        RestorePlacement();
        General.Actions.BindMethods(shell);                                 // "showerrors" lives in the shell, like MainForm's actions
        shell.Recent.Load();
        ShowRecentFiles();

        // What MainForm.Shown did: open the map from the command line now that the window is up
        General.MainWindow.PerformAutoMapLoading();
        Viewport.RequestRedraw();
    }

    // ---- keyboard

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!EditorRunning || FocusIsInTextInput()) return;

        Keys data = KeyMap.ToKeyData(e.Key, e.KeyModifiers);
        if (data == Keys.None) return;

        if (shell.Input.KeyDown(data)) e.Handled = true;
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!EditorRunning || FocusIsInTextInput()) return;

        Keys data = KeyMap.ToKeyData(e.Key, e.KeyModifiers);
        if (data == Keys.None) return;

        if (shell.Input.KeyUp(data)) e.Handled = true;
    }

    // Typing into a text box must not trigger editor shortcuts
    private bool FocusIsInTextInput() => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;

    // ---- mouse

    private MouseEventArgs ToMouseArgs(PointerEventArgs e, MouseButtons button, int clicks, int delta)
    {
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        Point p = e.GetPosition(InputSurface);
        return new MouseEventArgs(button, clicks, (int)(p.X * scale), (int)(p.Y * scale), delta);
    }

    private static MouseButtons ToButton(PointerUpdateKind kind)
    {
        switch (kind)
        {
            case PointerUpdateKind.LeftButtonPressed: case PointerUpdateKind.LeftButtonReleased: return MouseButtons.Left;
            case PointerUpdateKind.RightButtonPressed: case PointerUpdateKind.RightButtonReleased: return MouseButtons.Right;
            case PointerUpdateKind.MiddleButtonPressed: case PointerUpdateKind.MiddleButtonReleased: return MouseButtons.Middle;
            case PointerUpdateKind.XButton1Pressed: case PointerUpdateKind.XButton1Released: return MouseButtons.XButton1;
            case PointerUpdateKind.XButton2Pressed: case PointerUpdateKind.XButton2Released: return MouseButtons.XButton2;
            default: return MouseButtons.None;
        }
    }

    private void OnPointerEntered(object sender, PointerEventArgs e)
    {
        if (!EditorRunning) return;
        shell.Input.MouseEnter(EventArgs.Empty);
        if (IsActive) Viewport.Focus();     // like UDB: the display takes the keyboard when the mouse is over it
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (!EditorRunning) return;

        MouseButtons button = ToButton(e.GetCurrentPoint(InputSurface).Properties.PointerUpdateKind);
        if (button == MouseButtons.None) return;

        Viewport.Focus();
        e.Pointer.Capture(InputSurface);       // keep getting the move/release events when dragging outside the view

        var args = ToMouseArgs(e, button, e.ClickCount, 0);
        shell.Input.MouseDown(args);
        if (e.ClickCount >= 2) shell.Input.MouseDoubleClick(args);
    }

    private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (!EditorRunning) return;

        MouseButtons button = ToButton(e.InitialPressMouseButton switch
        {
            MouseButton.Left => PointerUpdateKind.LeftButtonReleased,
            MouseButton.Right => PointerUpdateKind.RightButtonReleased,
            MouseButton.Middle => PointerUpdateKind.MiddleButtonReleased,
            MouseButton.XButton1 => PointerUpdateKind.XButton1Released,
            MouseButton.XButton2 => PointerUpdateKind.XButton2Released,
            _ => PointerUpdateKind.Other,
        });
        if (button == MouseButtons.None) return;

        e.Pointer.Capture(null);
        var args = ToMouseArgs(e, button, 1, 0);
        shell.Input.MouseUp(args);
        shell.Input.MouseClick(args);
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (!EditorRunning) return;
        shell.Input.MouseMove(ToMouseArgs(e, shell.Input.MouseButtons, 0, 0));
    }

    private void OnPointerWheel(object sender, PointerWheelEventArgs e)
    {
        if (!EditorRunning) return;

        if (e.Delta.Y != 0) shell.Input.Wheel(e.Delta.Y > 0 ? 120 : -120);
        if (e.Delta.X != 0) shell.Input.HorizontalWheel(e.Delta.X > 0 ? 120 : -120);
        e.Handled = true;
    }

    // Test/diagnostic hook: UDB_SCREENSHOT=file.png saves the first settled frame with a map in it and exits.
    private unsafe void OnFramePainted(GL gl, int fb, PixelSize size)
    {
        string path = Environment.GetEnvironmentVariable("UDB_SCREENSHOT");
        if (string.IsNullOrEmpty(path) || General.Map == null) return;
        // UDB_SCREENSHOT_MODE=<mode class name> (e.g. BaseVisualMode) switches to that mode first
        string mode = Environment.GetEnvironmentVariable("UDB_SCREENSHOT_MODE");
        if (framecount == 1 && !string.IsNullOrEmpty(mode)) Dispatcher.UIThread.Post(() => General.Editing.ChangeMode(mode));
        // UDB_RUN_ACTION=<action name> runs an action once the map is loaded (to exercise e.g. builder_testmap from a script)
        string action = Environment.GetEnvironmentVariable("UDB_RUN_ACTION");
        if (framecount == 6 && !string.IsNullOrEmpty(action)) Dispatcher.UIThread.Post(() => General.Actions.InvokeAction(action));
        // UDB_SCREENSHOT_CAMERA=x,y,z,anglexy,anglez (degrees) places the 3D camera
        string cam = Environment.GetEnvironmentVariable("UDB_SCREENSHOT_CAMERA");
        if (framecount == 5 && !string.IsNullOrEmpty(cam))
            Dispatcher.UIThread.Post(() =>
            {
                string[] p = cam.Split(',');
                var c = General.Map.VisualCamera;
                c.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture));
                c.AngleXY = CodeImp.DoomBuilder.Geometry.Angle2D.DegToRad(double.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture));
                c.AngleZ = CodeImp.DoomBuilder.Geometry.Angle2D.DegToRad(double.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture));
            });
        int needed = int.TryParse(Environment.GetEnvironmentVariable("UDB_SCREENSHOT_FRAMES"), out int f) ? f : (string.IsNullOrEmpty(mode) ? 4 : 12);   // frames to let queued GPU work and image loading settle
        if (++framecount < needed) { Viewport.RequestRedraw(); return; }   // let queued GPU work settle

        var pixels = new byte[size.Width * size.Height * 4];
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)fb);
        fixed (byte* p = pixels)
            gl.ReadPixels(0, 0, (uint)size.Width, (uint)size.Height, Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, p);

        // GL rows start at the bottom
        var flipped = new byte[pixels.Length];
        int stride = size.Width * 4;
        for (int y = 0; y < size.Height; y++)
            System.Buffer.BlockCopy(pixels, y * stride, flipped, (size.Height - 1 - y) * stride, stride);

        using var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(flipped, 0, bitmap.GetPixels(), flipped.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var file = File.Create(path)) data.SaveTo(file);
        Console.WriteLine("[screenshot] " + path);

        // The window's own UI (menus, toolbar, status bar) too: the GL content is not part of what Avalonia renders here
        try
        {
            var size2 = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
            using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(size2);
            rtb.Render(this);
            rtb.Save(Path.ChangeExtension(path, ".ui.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            Console.WriteLine("[screenshot] " + Path.ChangeExtension(path, ".ui.png"));
        }
        catch (Exception e) { Console.Error.WriteLine("[screenshot] UI capture failed: " + e.Message); }
        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }
}
