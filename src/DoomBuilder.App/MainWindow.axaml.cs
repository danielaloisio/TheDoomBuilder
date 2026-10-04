using System;
using System.IO;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using DoomBuilder.App.Input;
using Silk.NET.OpenGL;
using SkiaSharp;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Application = Avalonia.Application;

namespace DoomBuilder.App;

public partial class MainWindow : Avalonia.Controls.Window
{
    private readonly AvaloniaShell shell;
    private bool started;
    private int framecount;

    public MainWindow()
    {
        InitializeComponent();

        shell = new AvaloniaShell(Viewport);
        shell.StatusChanged += text => StatusText.Text = text;
        shell.HintsChanged += text => HintsText.Text = text;

        Viewport.Paint += OnPaint;
        Viewport.ContextFailed += reason => StatusText.Text = "OpenGL is not available: " + reason;
        Viewport.FramePainted += OnFramePainted;

        // Keyboard: tunnel so keys reach the editor wherever the focus is inside the window
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (s, e) => { if (started) shell.Input.ReleaseAllKeys(); };

        // Mouse
        Viewport.InputSurface = InputSurface;
        InputSurface.PointerEntered += OnPointerEntered;
        InputSurface.PointerExited += (s, e) => shell.Input.MouseLeave(EventArgs.Empty);
        InputSurface.PointerPressed += OnPointerPressed;
        InputSurface.PointerReleased += OnPointerReleased;
        InputSurface.PointerMoved += OnPointerMoved;
        InputSurface.PointerWheelChanged += OnPointerWheel;

        Opened += (s, e) => Dispatcher.UIThread.Post(StartEditor, DispatcherPriority.Background);
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
        string settingsdir = Program.SettingsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TheDoomBuilder");
        Directory.CreateDirectory(settingsdir);

        General.BuiltInPluginAssemblies.Add(typeof(ViewerPlug).Assembly);   // the viewer edit mode

        if (!General.Startup(Program.Arguments, () => shell, appdir, settingsdir))
        {
            StatusText.Text = "Startup failed, see the log in " + settingsdir;
            return;
        }

        // What MainForm.Shown did: open the map from the command line now that the window is up
        General.MainWindow.PerformAutoMapLoading();
        Viewport.RequestRedraw();
    }

    // ---- keyboard

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!started || FocusIsInTextInput()) return;

        Keys data = KeyMap.ToKeyData(e.Key, e.KeyModifiers);
        if (data == Keys.None) return;

        if (shell.Input.KeyDown(data)) e.Handled = true;
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!started || FocusIsInTextInput()) return;

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
        if (!started) return;
        shell.Input.MouseEnter(EventArgs.Empty);
        if (IsActive) Viewport.Focus();     // like UDB: the display takes the keyboard when the mouse is over it
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (!started) return;

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
        if (!started) return;

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
        if (!started) return;
        shell.Input.MouseMove(ToMouseArgs(e, shell.Input.MouseButtons, 0, 0));
    }

    private void OnPointerWheel(object sender, PointerWheelEventArgs e)
    {
        if (!started) return;

        if (e.Delta.Y != 0) shell.Input.Wheel(e.Delta.Y > 0 ? 120 : -120);
        if (e.Delta.X != 0) shell.Input.HorizontalWheel(e.Delta.X > 0 ? 120 : -120);
        e.Handled = true;
    }

    // Test/diagnostic hook: UDB_SCREENSHOT=file.png saves the first settled frame with a map in it and exits.
    private unsafe void OnFramePainted(GL gl, int fb, PixelSize size)
    {
        string path = Environment.GetEnvironmentVariable("UDB_SCREENSHOT");
        if (string.IsNullOrEmpty(path) || General.Map == null) return;
        if (++framecount < 4) { Viewport.RequestRedraw(); return; }   // let queued GPU work settle

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
        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }
}
