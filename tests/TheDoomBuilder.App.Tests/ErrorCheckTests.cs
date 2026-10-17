using System;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The Map Analysis mode (BuilderModes' ErrorCheckMode and ErrorCheckForm) in the real window.</summary>
public class ErrorCheckTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static void WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 1000 && !condition(); i++) { Flush(); System.Threading.Thread.Sleep(10); }
        Assert.True(condition(), "timed out");
    }

    // The sample room plus a line with no sides at all, outside of it
    private const string Broken = UdmfSample + @"
vertex { x = 300.0; y = 0.0; }
vertex { x = 400.0; y = 0.0; }
linedef { v1 = 4; v2 = 5; }
";

    [AvaloniaFact]
    public void The_analysis_finds_a_line_without_sides_and_leaving_the_mode_hides_the_window()
    {
        OpenEditor(wadPath: WriteUdmfWad(Broken), config: "GZDoom_DoomUDMF.cfg");
        General.Actions.InvokeAction("buildermodes_errorcheckmode");
        Flush();
        Assert.Equal("ErrorCheckMode", General.Editing.Mode.GetType().Name);

        var form = CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.ErrorCheckForm;
        Assert.True(form.Window.IsVisible);
        Assert.NotEmpty(form.Checks);

        form.StartChecking();
        Assert.True(form.IsRunning);
        WaitUntil(() => !form.IsRunning);

        Assert.True(form.ResultCount > 0, "the line without sides is reported");
        Assert.Equal(form.ResultCount, form.ResultsList.ItemCount);

        // Selecting a result explains it and offers the fixes
        form.ResultsList.SelectedIndex = 0;
        Flush();
        Assert.Single(form.SelectedResults);
        Assert.False(string.IsNullOrEmpty(form.InfoText));
        Assert.Contains(form.FixButtons, b => b.IsVisible);

        General.Editing.CancelMode();
        Flush();
        Assert.NotEqual("ErrorCheckMode", General.Editing.Mode.GetType().Name);
        Assert.False(form.Window.IsVisible);
    }
}
