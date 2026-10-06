using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The image drawing example plugin: a mode that draws an image (an embedded resource) over the display.</summary>
public class ImageDrawingExampleTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_mode_loads_its_image_and_cancel_returns_to_the_previous_mode()
    {
        OpenEditor();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "ImageExampleMode");
        Assert.NotNull(General.Actions.GetActionByName("imagedrawingexample_imageexamplemode"));

        General.Actions.InvokeAction("imagedrawingexample_imageexamplemode");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("ImageExampleMode", General.Editing.Mode.GetType().Name);

        General.Editing.CancelMode();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotEqual("ImageExampleMode", General.Editing.Mode.GetType().Name);
    }
}
