using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Plugins;

namespace DoomBuilder.Core.Tests;

/// <summary>Plugin entry so the test assembly can be loaded as a built-in plugin (General.BuiltInPluginAssemblies).</summary>
public class ProbePlug : Plug { }

/// <summary>An edit mode that records everything the input layer sends it.</summary>
[EditMode(DisplayName = "Probe", SwitchAction = "probemode", ButtonGroup = "000_editing", Optional = false, UseByDefault = true)]
public class ProbeMode : EditMode
{
    public static ProbeMode Current { get; private set; }
    public readonly List<string> Events = new List<string>();
    public readonly List<Vector2D> MouseInputs = new List<Vector2D>();
    public int ProcessCalls;

    public ProbeMode() { Current = this; }

    public override void OnMouseDown(MouseEventArgs e) => Events.Add($"down {e.Button} {e.X},{e.Y}");
    public override void OnMouseUp(MouseEventArgs e) => Events.Add($"up {e.Button} {e.X},{e.Y}");
    public override void OnMouseMove(MouseEventArgs e) => Events.Add($"move {e.X},{e.Y}");
    public override void OnMouseClick(MouseEventArgs e) => Events.Add($"click {e.Button}");
    public override void OnMouseDoubleClick(MouseEventArgs e) => Events.Add($"doubleclick {e.Button}");
    public override void OnMouseEnter(EventArgs e) => Events.Add("enter");
    public override void OnMouseLeave(EventArgs e) => Events.Add("leave");
    public override void OnKeyDown(KeyEventArgs e) => Events.Add($"keydown {e.KeyData}");
    public override void OnKeyUp(KeyEventArgs e) => Events.Add($"keyup {e.KeyData}");
    public override void OnMouseInput(Vector2D mousedelta) => MouseInputs.Add(mousedelta);
    public override void OnProcess(long deltatime) => ProcessCalls++;
}

/// <summary>Records the actions that fired, by the order of begin/end calls.</summary>
public class ActionProbe
{
    public readonly List<string> Log = new List<string>();

    [BeginAction("testkey")] void KeyBegin() => Log.Add("testkey begin");
    [EndAction("testkey")] void KeyEnd() => Log.Add("testkey end");
    [BeginAction("testctrlkey")] void CtrlBegin() => Log.Add("testctrlkey begin");
    [EndAction("testctrlkey")] void CtrlEnd() => Log.Add("testctrlkey end");
    [BeginAction("testmiddle")] void MiddleBegin() => Log.Add("testmiddle begin");
    [EndAction("testmiddle")] void MiddleEnd() => Log.Add("testmiddle end");
    [BeginAction("testscrollup")] void ScrollBegin() => Log.Add("testscrollup begin");
    [EndAction("testscrollup")] void ScrollEnd() => Log.Add("testscrollup end");
}

/// <summary>Stands in for the toolkit shell.</summary>
public class FakeInputHost : IInputHost
{
    public readonly List<bool> ProcessingStates = new List<bool>();
    public int CapturesStarted, CapturesReleased;
    public Vector2D NextPoll;
    public bool CanProcess = true;

    public bool CanProcessInput => CanProcess;
    public void SetProcessing(bool enabled) => ProcessingStates.Add(enabled);

    public IMouseCapture BeginMouseCapture()
    {
        CapturesStarted++;
        return new Capture(this);
    }

    private sealed class Capture : IMouseCapture
    {
        private readonly FakeInputHost host;
        public Capture(FakeInputHost host) { this.host = host; }
        public Vector2D Poll() { var v = host.NextPoll; host.NextPoll = new Vector2D(); return v; }
        public void Dispose() => host.CapturesReleased++;
    }
}
