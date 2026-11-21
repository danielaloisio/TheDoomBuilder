using System;
using System.IO;
using System.Windows.Forms;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>
/// The dialog flows of the Core (open map, save map as, ask to save changes), driven through a scripted IDialogService:
/// the same call sites the Avalonia dialogs will answer.
/// </summary>
[Collection("General static state")]
public class DialogFlowTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-dialogs-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();
    private readonly ScriptedDialogs dialogs = new ScriptedDialogs();

    public DialogFlowTests()
    {
        Directory.CreateDirectory(dir);
        General.Dialogs = dialogs;
        Assert.True(General.Startup(new[] { "-nosettings" }, () => new HeadlessMainWindow(), appdir, dir));
    }

    public void Dispose()
    {
        General.Dialogs = null;
        General.ShutdownHeadless();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    private MapOptions DoomOptions(string mapname = "MAP01")
    {
        string cfg = "Doom_DoomDoom.cfg";
        var options = new MapOptions(new CodeImp.DoomBuilder.IO.Configuration(true), mapname,
            General.GetConfigurationInfo(cfg).Configuration.ReadSetting("longtexturenames", false));
        options.ConfigFile = cfg;
        return options;
    }

    [Fact]
    public void Message_boxes_go_to_the_dialog_service_and_get_its_answer()
    {
        dialogs.MessageAnswers.Enqueue(DialogResult.Yes);

        var result = MessageBox.Show("Save changes?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        Assert.Equal(DialogResult.Yes, result);
        Assert.Equal(new[] { "Question: Save changes?" }, dialogs.Messages);
    }

    [Fact]
    public void Without_a_host_service_dialogs_are_cancelled()
    {
        General.Dialogs = null;                         // back to the default
        Assert.Equal(DialogResult.No, MessageBox.Show("x", "y", MessageBoxButtons.YesNo));
        Assert.Equal(DialogResult.OK, MessageBox.Show("x", "y"));
        Assert.Equal(DialogResult.Cancel, new OpenFileDialog().ShowDialog());
    }

    [Fact]
    public void Open_map_asks_for_a_wad_then_for_the_map_options_then_opens_it()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        dialogs.FilesToChoose.Enqueue(wad);
        OpenMapOptionsForm shown = null;
        dialogs.OnOpenMapOptions = form => { shown = form; form.Options = DoomOptions(); return DialogResult.OK; };

        General.Actions.InvokeAction("builder_openmap");

        var filedialog = Assert.Single(dialogs.FileDialogsShown);
        Assert.Equal("Open Map", filedialog.Title);
        Assert.Contains("*.wad", filedialog.Filter);
        Assert.Equal(wad, shown.FileName);

        Assert.NotNull(General.Map);
        Assert.Equal(4, General.Map.Map.Vertices.Count);
    }

    [Fact]
    public void Cancelling_the_file_dialog_opens_nothing()
    {
        General.Actions.InvokeAction("builder_openmap");

        Assert.Single(dialogs.FileDialogsShown);
        Assert.Null(General.Map);
    }

    [Fact]
    public void Cancelling_the_map_options_opens_nothing()
    {
        dialogs.FilesToChoose.Enqueue(MapFiles.WriteSquareRoomWad(dir));
        dialogs.OnOpenMapOptions = form => DialogResult.Cancel;

        General.Actions.InvokeAction("builder_openmap");

        Assert.Null(General.Map);
    }

    [Fact]
    public void Save_as_writes_a_new_wad_the_map_can_be_read_back_from()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        General.OpenMapFileWithOptions(wad, DoomOptions());
        Assert.NotNull(General.Map);

        string target = Path.Combine(dir, "copy.wad");
        dialogs.FilesToChoose.Enqueue(target);
        General.Actions.InvokeAction("builder_savemapas");

        Assert.True(File.Exists(target), string.Join(" | ", dialogs.Messages));
        using var saved = new WAD(target, true);
        Assert.NotNull(saved.FindLump("MAP01"));
        Assert.NotNull(saved.FindLump("VERTEXES"));
        Assert.Equal(4 * 4, saved.FindLump("VERTEXES").Length);   // 4 vertices, 4 bytes each
    }
}
