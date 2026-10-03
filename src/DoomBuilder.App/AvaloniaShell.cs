using System;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App;

/// <summary>
/// Connects the Core to the Avalonia window: the Core talks to <see cref="IMainWindow"/>, this is the implementation.
/// It starts from the headless shell (everything is a no-op) and only overrides what the app really does so far.
/// More members move here as the shell grows (Phase 4).
/// </summary>
internal sealed class AvaloniaShell : HeadlessMainWindow
{
    private readonly MapViewport viewport;

    public AvaloniaShell(MapViewport viewport)
    {
        this.viewport = viewport;
    }

    /// <summary>Raised when the status line text changes.</summary>
    public event Action<string> StatusChanged;

    public override IRenderBackend CreateRenderBackend() => viewport.Backend;

    public override void RedrawDisplay() => viewport.RequestRedraw();

    public override void RunOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public override void DisplayStatus(StatusInfo newstatus)
    {
        base.DisplayStatus(newstatus);
        StatusChanged?.Invoke(newstatus.message);
    }

    public override void DisplayStatus(StatusType type, string message) => DisplayStatus(new StatusInfo(type, message));

    public override void DisplayReady() => DisplayStatus(StatusType.Ready, "Ready.");

    public override void UpdateZoom(float scale) => StatusChanged?.Invoke("Zoom " + (int)Math.Round(scale * 100) + "%");
}
