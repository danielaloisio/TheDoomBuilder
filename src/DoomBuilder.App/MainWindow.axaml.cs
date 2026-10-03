using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using Silk.NET.OpenGL;
using SkiaSharp;

namespace DoomBuilder.App;

public partial class MainWindow : Window
{
    private readonly AvaloniaShell shell;
    private readonly MapViewer viewer = new MapViewer();
    private bool started;
    private Point? dragstart;
    private int framecount;

    public MainWindow()
    {
        InitializeComponent();

        shell = new AvaloniaShell(Viewport);
        shell.StatusChanged += text => StatusText.Text = text;

        Viewport.Paint += OnPaint;
        Viewport.ContextFailed += reason => StatusText.Text = "OpenGL is not available: " + reason;
        Viewport.FramePainted += OnFramePainted;

        Viewport.PointerWheelChanged += OnWheel;
        Viewport.PointerPressed += OnPointerPressed;
        Viewport.PointerMoved += OnPointerMoved;
        Viewport.PointerReleased += (s, e) => dragstart = null;

        Opened += (s, e) => Dispatcher.UIThread.Post(StartEditor, DispatcherPriority.Background);
    }

    // What MainForm.RedrawDisplay did: let the active edit mode draw
    private void OnPaint()
    {
        if (General.Map == null || General.Editing.Mode == null) return;
        viewer.PrepareFrame();
        General.Editing.Mode.OnRedrawDisplay();
    }

    private void StartEditor()
    {
        if (started) return;
        started = true;

        string appdir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string settingsdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TheDoomBuilder");
        Directory.CreateDirectory(settingsdir);

        General.BuiltInPluginAssemblies.Add(typeof(ViewerPlug).Assembly);   // the viewer edit mode

        string[] args = Program.Arguments;
        if (!General.Startup(args, () => shell, appdir, settingsdir))
        {
            StatusText.Text = "Startup failed, see the log in " + settingsdir;
            return;
        }

        // What MainForm.Shown did: open the map from the command line now that the window is up
        General.MainWindow.PerformAutoMapLoading();
        Viewport.RequestRedraw();
    }

    private void OnWheel(object sender, PointerWheelEventArgs e)
    {
        Point p = e.GetPosition(Viewport);
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        viewer.ZoomAt(Math.Pow(1.2, e.Delta.Y), p.X * scaling, p.Y * scaling);
        Viewport.RequestRedraw();
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        PointerPointProperties props = e.GetCurrentPoint(Viewport).Properties;
        if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed)
        {
            dragstart = e.GetPosition(Viewport);
            e.Pointer.Capture(Viewport);
        }
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (dragstart == null) return;
        Point p = e.GetPosition(Viewport);
        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        viewer.Pan((p.X - dragstart.Value.X) * scaling, (p.Y - dragstart.Value.Y) * scaling);
        dragstart = p;
        Viewport.RequestRedraw();
    }

    // Test/diagnostic hook: UDB_SCREENSHOT=file.png saves the first settled frame with a map in it and exits.
    private unsafe void OnFramePainted(GL gl, int fb, PixelSize size)
    {
        string path = Environment.GetEnvironmentVariable("UDB_SCREENSHOT");
        if (string.IsNullOrEmpty(path) || !viewer.HasMap) return;
        if (++framecount < 4) { Viewport.RequestRedraw(); return; }   // let queued GPU work settle

        var pixels = new byte[size.Width * size.Height * 4];
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)fb);
        fixed (byte* p = pixels)
            gl.ReadPixels(0, 0, (uint)size.Width, (uint)size.Height, PixelFormat.Rgba, PixelType.UnsignedByte, p);

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
