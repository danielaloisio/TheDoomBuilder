using System;
using System.Windows.Forms;
using Avalonia.Input;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.App.Input;
using Action = System.Action;
using Cursor = System.Windows.Forms.Cursor;

namespace DoomBuilder.App;

/// <summary>
/// Connects the Core to the Avalonia window: the Core talks to <see cref="IMainWindow"/>, this is the implementation.
/// It starts from the headless shell (everything is a no-op) and only overrides what the app really does so far.
/// More members move here as the shell grows (Phase 4).
/// </summary>
internal sealed class AvaloniaShell : HeadlessMainWindow, IInputHost
{
    private readonly MapViewport viewport;
    private readonly IPointerWarp warp = PointerWarp.Create();
    private readonly DispatcherTimer processor;
    private readonly RenderTargetControl display = new RenderTargetControl();

    public AvaloniaShell(MapViewport viewport)
    {
        this.viewport = viewport;
        Input = new InputDispatcher(this);

        // The edit modes' ProcessMovement runs off this timer while processing is enabled (UDB used a 10 ms WinForms timer)
        processor = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Input, (s, e) => Input.Tick());
        processor.Stop();

        viewport.SizeChanged += (s, e) => display.ClientSize = new System.Drawing.Size(viewport.PixelSize.Width, viewport.PixelSize.Height);
    }

    /// <summary>Keyboard and mouse go through here.</summary>
    public InputDispatcher Input { get; }

    /// <summary>Raised when the status line text changes.</summary>
    public event Action<string> StatusChanged;

    /// <summary>Raised when an edit mode shows hints about its keys (null/empty clears them).</summary>
    public event Action<string> HintsChanged;

    /// <summary>The map, the mode or the settings changed: menus, toolbar and title must be refreshed.</summary>
    public event Action InterfaceChanged;

    public event Action<float> ZoomChanged;
    public event Action<double> GridChanged;
    public event Action<Vector2D, bool> CoordinatesChanged;
    public event Action<int, bool> WarningsChanged;

    /// <summary>The recent files list changed.</summary>
    public event Action RecentFilesChanged;

    public CodeImp.DoomBuilder.Windows.RecentFiles Recent { get; } = new CodeImp.DoomBuilder.Windows.RecentFiles();

    public override void AddRecentFile(string filename)
    {
        Recent.Add(filename);
        RecentFilesChanged?.Invoke();
    }

    // ---- rendering / threading

    public override IRenderBackend CreateRenderBackend() => viewport.Backend;

    public override void RedrawDisplay() => viewport.RequestRedraw();

    public override void RunOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    // ---- status

    public override void DisplayStatus(StatusInfo newstatus)
    {
        base.DisplayStatus(newstatus);
        StatusChanged?.Invoke(newstatus.message);
    }

    public override void DisplayStatus(StatusType type, string message) => DisplayStatus(new StatusInfo(type, message));

    public override void DisplayReady() => DisplayStatus(StatusType.Ready, "Ready.");

    public override void UpdateZoom(float scale) => ZoomChanged?.Invoke(scale);
    public override void UpdateGrid(double gridsize) => GridChanged?.Invoke(gridsize);
    public override void SetWarningsCount(int count, bool blink) => RunOnUIThread(() => WarningsChanged?.Invoke(count, blink));

    /// <summary>Shows the list of errors and warnings (the "showerrors" action, the status bar indicator and the settings that open it on errors).</summary>
    [CodeImp.DoomBuilder.Actions.BeginAction("showerrors", BaseAction = true)]
    public override void ShowErrors()
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(ShowErrors); return; }
        General.Dialogs.ShowErrors();
        SetWarningsCount(General.ErrorLogger.ErrorsCount, false);
    }

    public override void UpdateInterface() => InterfaceChanged?.Invoke();
    public override void SetupInterface() => InterfaceChanged?.Invoke();
    public override void UpdateMapChangedStatus() => InterfaceChanged?.Invoke();
    public override void UpdateThingsFilters() => InterfaceChanged?.Invoke();
    public override void EditModeChanged() => InterfaceChanged?.Invoke();
    public override void CheckEditModeButton(string modeclassname) => InterfaceChanged?.Invoke();

    public override void ShowHints(string hints) => HintsChanged?.Invoke(hints);

    public override void ClearHints() => HintsChanged?.Invoke(string.Empty);

    // ---- input state (what edit modes ask the main window)

    public override bool AltState => Input.AltState;
    public override bool CtrlState => Input.CtrlState;
    public override bool ShiftState => Input.ShiftState;
    public override MouseButtons MouseButtons => Input.MouseButtons;
    public override bool MouseInDisplay => Input.MouseInDisplay;
    public override bool MouseExclusive => Input.MouseExclusive;
    public override int ProcessingCount => Input.ProcessingCount;
    public override RenderTargetControl Display => display;

    public override bool FocusDisplay() => viewport.Focus();
    public override bool Focus() => viewport.Focus();

    public override void SetCursor(Cursor cursor) => viewport.Cursor = CursorMap.ToAvalonia(cursor);

    public override void StartExclusiveMouseInput() => Input.StartExclusiveMouseInput();
    public override void StopExclusiveMouseInput() => Input.StopExclusiveMouseInput();
    public override void BreakExclusiveMouseInput() => Input.BreakExclusiveMouseInput();
    public override void ResumeExclusiveMouseInput() => Input.ResumeExclusiveMouseInput();

    public override void EnableProcessing() => Input.EnableProcessing();
    public override void DisableProcessing() => Input.DisableProcessing();
    public override void StopProcessing() => Input.StopProcessing();
    public override void ResetClock()
    {
        General.AutoSaver?.BeforeClockReset();
        Clock.Reset();
        Input.ResetClock();
        if (General.Editing != null && General.Editing.Mode != null) General.Editing.Mode.OnClockReset();
    }

    public override void UpdateCoordinates(Vector2D coords) => UpdateCoordinates(coords, false);

    public override void UpdateCoordinates(Vector2D coords, bool snaptogrid) => CoordinatesChanged?.Invoke(coords, snaptogrid);

    // ---- IInputHost

    IMouseCapture IInputHost.BeginMouseCapture() => new ViewportMouseCapture(viewport, warp);

    void IInputHost.SetProcessing(bool enabled)
    {
        if (enabled) processor.Start();
        else processor.Stop();
    }

    bool IInputHost.CanProcessInput => true;
}
