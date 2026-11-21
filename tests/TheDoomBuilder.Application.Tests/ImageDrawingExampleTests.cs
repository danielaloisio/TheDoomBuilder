using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace TheDoomBuilder.Application.Tests;

/// <summary>The image drawing example plugin: demo/example code from the original UDB, not a real editing feature.
/// It's Hidden: left out of the "Editing modes" list and the modes bar, and never switchable, regardless of any
/// value saved for it in an older settings file (see EditModeAttribute.Hidden).</summary>
public class ImageDrawingExampleTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_mode_is_hidden_from_the_modes_bar_and_cannot_be_switched_to()
    {
        OpenEditor();
        var info = General.Editing.ModesInfo.First(m => m.Type.Name == "ImageExampleMode");
        Assert.True(info.Attributes.Hidden);

        // The action still exists (the plugin is still loaded), but since the mode is never in the
        // "used" modes, nothing is bound to it, so invoking it leaves the current mode unchanged.
        Assert.NotNull(General.Actions.GetActionByName("imagedrawingexample_imageexamplemode"));
        var modeBefore = General.Editing.Mode.GetType().Name;
        General.Actions.InvokeAction("imagedrawingexample_imageexamplemode");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(modeBefore, General.Editing.Mode.GetType().Name);

        // Even if an older settings file had it explicitly enabled, ChangeMode refuses it.
        General.Map.ConfigSettings.EditModes[info.Type.FullName] = true;
        General.Editing.UpdateCurrentEditModes();
        Assert.False(General.Editing.ChangeMode(new CodeImp.DoomBuilder.Statistics.ImageExampleMode()));
        Assert.Equal(modeBefore, General.Editing.Mode.GetType().Name);
    }
}
