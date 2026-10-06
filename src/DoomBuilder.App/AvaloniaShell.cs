using System;
using System.Windows.Forms;
using Avalonia.Input;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Editing;
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

    /// <summary>The "opencommandpalette" action ran.</summary>
    public event Action CommandPaletteRequested;

    // Ends the action (not begins it) because of how keys are stored while it runs: a key still held would otherwise stay pressed
    [CodeImp.DoomBuilder.Actions.EndAction("opencommandpalette", BaseAction = true)]
    public void OpenCommandPalette() => CommandPaletteRequested?.Invoke();

    /// <summary>Draws the menus and buttons the plugins add; set by the window once its menus exist.</summary>
    internal Shell.PluginUi Plugins { get; set; }

    /// <summary>The tabs of the side panel.</summary>
    public DockerModel Dockers { get; } = new DockerModel();

    /// <summary>Opens or collapses the info panel under the map ("toggleinfopanel").</summary>
    [CodeImp.DoomBuilder.Actions.BeginAction("toggleinfopanel", BaseAction = true)]
    public void ToggleInfoPanel()
    {
        IsInfoPanelExpanded = !IsInfoPanelExpanded;
        if (IsInfoPanelExpanded) RefreshInfo();
        RaiseInterfaceChanged();
        FocusDisplay();
    }

    private bool snaptogrid = true, automerge = true;

    // Both start on, like the buttons of UDB's toolbar. Drawing and dragging read them for every point they place
    public override bool SnapToGrid { get { return snaptogrid; } }
    public override bool AutoMerge { get { return automerge; } }

    [CodeImp.DoomBuilder.Actions.BeginAction("togglesnap", BaseAction = true)]
    public void ToggleSnapToGrid()
    {
        snaptogrid = !snaptogrid;
        DisplayStatus(StatusType.Action, "Snap to grid is " + (snaptogrid ? "ENABLED" : "DISABLED"));
        RaiseInterfaceChanged();
        RedrawDisplay();
    }

    [CodeImp.DoomBuilder.Actions.BeginAction("toggleautomerge", BaseAction = true)]
    public void ToggleAutoMerge()
    {
        automerge = !automerge;
        DisplayStatus(StatusType.Action, "Snap to geometry is " + (automerge ? "ENABLED" : "DISABLED"));
        RaiseInterfaceChanged();
        RedrawDisplay();
    }

    private DispatcherTimer redrawTimer;

    /// <summary>A delayed redraw is waiting for its moment.</summary>
    public bool RedrawPending => redrawTimer != null && redrawTimer.IsEnabled;

    /// <summary>Asks for a redraw in a moment: many images arriving one after the other make one redraw, not hundreds.</summary>
    public override void DelayedRedraw()
    {
        if (redrawTimer == null)
        {
            redrawTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            redrawTimer.Tick += (s, e) => { redrawTimer.Stop(); RedrawDisplay(); };
        }
        if (!redrawTimer.IsEnabled) redrawTimer.Start();
    }

    private Docker hintsDocker;

    /// <summary>The panel of the "Help" docker.</summary>
    public Shell.HintsPanel HintsPanel { get; } = new Shell.HintsPanel();

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

    /// <summary>How many redraws were asked for (diagnostics and tests).</summary>
    public int RedrawRequests { get; private set; }

    public override void RedrawDisplay()
    {
        RedrawRequests++;
        viewport.RequestRedraw();
    }

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

    /// <summary>The game configurations dialog; on OK the interface, edit modes and plugins are refreshed and resources reloaded if needed.</summary>
    [CodeImp.DoomBuilder.Actions.BeginAction("configuration", BaseAction = true)]
    public override void ShowConfiguration() => ShowConfigurationPage(-1);

    public override void ShowConfigurationPage(int pageindex)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => ShowConfigurationPage(pageindex)); return; }

        if (General.Dialogs.ShowConfiguration(pageindex, out bool reload) != System.Windows.Forms.DialogResult.OK) return;

        UpdateInterface();
        General.Editing.UpdateCurrentEditModes();
        General.Plugins.ProgramReconfigure();
        General.SaveSettings();
        if (General.Map != null && reload) General.Actions.InvokeAction("builder_reloadresources");
        RedrawDisplay();
    }

    /// <summary>The preferences dialog; on OK the interface, colors, plugins and the open map are brought up to date.</summary>
    [CodeImp.DoomBuilder.Actions.BeginAction("preferences", BaseAction = true)]
    public void ShowPreferences()
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(ShowPreferences); return; }

        if (General.Dialogs.ShowPreferences(out bool reload) != System.Windows.Forms.DialogResult.OK) return;

        UpdateInterface();
        ApplyShortcutKeys();
        General.Colors.CreateCorrectionTable();
        General.Plugins.ProgramReconfigure();
        General.SaveSettings();

        if (General.Map != null)
        {
            General.Map.Graphics.SetupSettings();
            General.Map.UpdateConfiguration();
            if (reload) General.Actions.InvokeAction("builder_reloadresources");
        }
        RedrawDisplay();
    }

    /// <summary>Shows the list of errors and warnings (the "showerrors" action, the status bar indicator and the settings that open it on errors).</summary>
    [CodeImp.DoomBuilder.Actions.BeginAction("showerrors", BaseAction = true)]
    public override void ShowErrors()
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(ShowErrors); return; }
        General.Dialogs.ShowErrors();
        SetWarningsCount(General.ErrorLogger.ErrorsCount, false);
    }

    private int interfacechangedpending;

    // The interface is refreshed on the UI thread. Other threads (a UDBScript script changing the map) ask for it: one refresh covers
    // all the requests made before it runs.
    private void RaiseInterfaceChanged()
    {
        if (Dispatcher.UIThread.CheckAccess()) { InterfaceChanged?.Invoke(); return; }
        if (System.Threading.Interlocked.Exchange(ref interfacechangedpending, 1) == 1) return;
        Dispatcher.UIThread.Post(() => { System.Threading.Interlocked.Exchange(ref interfacechangedpending, 0); InterfaceChanged?.Invoke(); });
    }

    public override void UpdateInterface() => RaiseInterfaceChanged();
    public override void SetupInterface() => RaiseInterfaceChanged();
    public override void UpdateMapChangedStatus() => RaiseInterfaceChanged();
    public override void UpdateThingsFilters() => RaiseInterfaceChanged();
    public override void EditModeChanged()
    {
        // As MainForm did: check the button (and menu entry) of the mode that is active now
        string button = General.Editing?.Mode?.EditModeButtonName ?? string.Empty;
        OnUi(() => Plugins?.CheckEditModeButton(button));
        RaiseInterfaceChanged();
    }

    public override void ShowHints(string hints)
    {
        if (string.IsNullOrEmpty(hints)) HintsPanel.ClearHints(); else HintsPanel.SetHints(hints);
        HintsChanged?.Invoke(hints);
    }

    public override void ClearHints()
    {
        HintsPanel.ClearHints();
        HintsChanged?.Invoke(string.Empty);
    }

    // ---- the menus and toolbar buttons of plugins. The plugin is the one whose code calls us (its actions are "<plugin>_<name>")

    private static string PluginNameOf(System.Reflection.Assembly caller)
        => (General.Plugins?.FindPluginByAssembly(caller)?.Name ?? caller.GetName().Name).ToLowerInvariant();

    private void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action(); else Dispatcher.UIThread.Post(action);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddMenu(ToolStripItem menu) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddMenu(menu, MenuSection.Top, p)); }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddMenu(ToolStripItem menu, MenuSection section) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddMenu(menu, section, p)); }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddModesMenu(ToolStripItem menu, string group) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddModesMenu(menu, group, p)); }

    public override void RemoveMenu(ToolStripItem menu) => OnUi(() => Plugins?.RemoveMenu(menu));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddButton(ToolStripItem button) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddButton(button, ToolbarSection.Custom, p)); }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddButton(ToolStripItem button, ToolbarSection section) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddButton(button, section, p)); }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddModesButton(ToolStripItem toolbarButton, string group) { string p = PluginNameOf(System.Reflection.Assembly.GetCallingAssembly()); OnUi(() => Plugins?.AddModesButton(toolbarButton, group, p)); }

    public override void RemoveButton(ToolStripItem button) => OnUi(() => Plugins?.RemoveButton(button));

    public override void AddEditModeButton(EditModeInfo modeinfo) => OnUi(() => Plugins?.AddEditModeButton(modeinfo));
    public override void AddEditModeSeperator(string group) => OnUi(() => Plugins?.AddEditModeSeparator(group));
    public override void RemoveEditModeButtons() => OnUi(() => Plugins?.RemoveEditModeButtons());
    public override void CheckEditModeButton(string modeclassname) => OnUi(() => { Plugins?.CheckEditModeButton(modeclassname); InterfaceChanged?.Invoke(); });
    public override void ApplyShortcutKeys() => OnUi(() => { Plugins?.ApplyShortcutKeys(); });

    /// <summary>The click handler UDB's menus and buttons used: the item's Tag names the action to run.</summary>
    public override void InvokeTaggedAction(object sender, EventArgs e)
    {
        if (sender is ToolStripItem { Tag: string action } && General.Actions != null && General.Actions.Exists(action))
            General.Actions.InvokeAction(action);
    }

    // ---- dockers. A plugin's docker is named after the plugin (prefix_name), found from the assembly that calls us

    private static string PrefixOf(System.Reflection.Assembly caller)
    {
        string name = General.Plugins?.FindPluginByAssembly(caller)?.Name ?? caller.GetName().Name;
        return name.ToLowerInvariant();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddDocker(Docker d) => Dockers.Add(d, false, PrefixOf(System.Reflection.Assembly.GetCallingAssembly()));

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override void AddDocker(Docker d, bool notify) => Dockers.Add(d, notify, PrefixOf(System.Reflection.Assembly.GetCallingAssembly()));

    public override bool RemoveDocker(Docker d)
    {
        if (!Dockers.Contains(d)) return true;      // already removed or never added
        Input.ReleaseAllKeys();                      // the focus may move to the docker that takes over
        return Dockers.Remove(d);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public override bool SelectDocker(Docker d)
    {
        if (!Dockers.Contains(d)) return false;
        Input.ReleaseAllKeys();
        return Dockers.Select(d, PrefixOf(System.Reflection.Assembly.GetCallingAssembly()));
    }

    public override void SelectPreviousDocker()
    {
        Input.ReleaseAllKeys();
        Dockers.SelectPrevious();
    }

    public override string ActiveDockerTabName => Dockers.SelectedTitle;

    public override void AddHintsDocker()
    {
        hintsDocker ??= Shell.AvaloniaDocker.Create("hints", "Help", HintsPanel);
        if (!Dockers.Contains(hintsDocker)) Dockers.Add(hintsDocker, false);
    }

    public override void RemoveHintsDocker()
    {
        if (hintsDocker != null) Dockers.Remove(hintsDocker);
    }

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

    /// <summary>Whether the main window is the active one (not an edit dialog over it): modes only open their edit dialogs then.</summary>
    internal Func<bool> WindowIsActive { get; set; }

    public override bool IsActiveWindow => WindowIsActive?.Invoke() ?? false;

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

    IMouseCapture IInputHost.BeginMouseCapture() => new ViewportMouseCapture(viewport, warp, OperatingSystem.IsLinux() ? X11RawMotion.TryCreate() : null);

    void IInputHost.SetProcessing(bool enabled)
    {
        if (enabled) processor.Start();
        else processor.Stop();
    }

    bool IInputHost.CanProcessInput => true;
}
