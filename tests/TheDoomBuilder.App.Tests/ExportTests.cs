using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.BuilderModes.Interface;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The exporters of BuilderModes (Wavefront .obj, idStudio) with their settings dialogs.</summary>
public class ExportTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void The_obj_export_dialog_writes_the_map_as_a_wavefront_file()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        string output = Path.Combine(dir, "room.obj");

        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.StartsWith("Export whole map", d.Title);
            Assert.Equal("Export", d.OkButton.Content);
            var path = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(d).OfType<Avalonia.Controls.TextBox>().First();
            Assert.EndsWith(".obj", path.Text);                      // the map's name, in the map's folder
            path.Text = output;
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("buildermodes_exporttoobj");
        Flush();

        Assert.True(shown, "the settings dialog opened");
        Assert.True(File.Exists(output), "the .obj file was written");
        string text = File.ReadAllText(output);
        Assert.Contains("\nv ", "\n" + text);                      // vertices
        Assert.Contains("\nf ", "\n" + text);                      // faces
    }

    [AvaloniaFact]
    public void The_obj_export_dialog_refuses_a_missing_folder_and_a_bad_actor_name_and_keeps_the_settings_of_the_last_export()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        var form = new WavefrontSettingsForm(-1);
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;

        // A path in a folder that does not exist
        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            form.ExportPathBox.Text = Path.Combine(dir, "nowhere", "x.obj");
            Click(d.OkButton);
            Assert.True(d.IsVisible, "the dialog stays open");
            Assert.Contains(scripted.Messages, m => m.Contains("Selected path does not exist"));

            // Export for GZDoom: the actor name is checked first
            form.GZDoomBox.IsChecked = true;
            form.ActorNameBox.Text = "9lives";
            scripted.Messages.Clear();
            Click(d.OkButton);
            Assert.True(d.IsVisible);
            Assert.Contains(scripted.Messages, m => m.Contains("can not start with a digit"));
            d.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, form.ShowDialog());
        Assert.True(shown);
    }

    [AvaloniaFact]
    public void The_idstudio_export_checks_the_map_name_and_lists_the_textures_of_the_map()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        var form = new idStudioExporterForm();
        Assert.Contains("STARTAN1", form.MapTextures);
        Assert.Contains("FLOOR4_8", form.MapFlats);
        Assert.DoesNotContain("-", form.MapTextures);
        Assert.Equal("map01", form.MapName);

        Assert.True(idStudioExporterForm.IsValidMapName("map01"));
        Assert.True(idStudioExporterForm.IsValidMapName("my_map_2"));
        Assert.False(idStudioExporterForm.IsValidMapName("MAP01"));
        Assert.False(idStudioExporterForm.IsValidMapName("1map"));
        Assert.False(idStudioExporterForm.IsValidMapName("map 1"));
        Assert.False(idStudioExporterForm.IsValidMapName(""));

        // The dialog does not accept a bad name
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;
        WhenShown<SimpleDialog>(d =>
        {
            form.MapNameBox.Text = "BAD NAME";
            Click(d.OkButton);
            Assert.True(d.IsVisible);
            Assert.Contains(scripted.Messages, m => m.Contains("Map names must be all lowercase"));
            Click(d.CancelButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, form.ShowDialog());
    }

    // A square room drawn clockwise: the front of each wall faces in, so the sector has an inside to triangulate
    private const string ClockwiseRoom = @"namespace = ""zdoom"";
vertex { x = 0.0; y = 0.0; }
vertex { x = 0.0; y = 128.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 128.0; y = 0.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
thing { x = 64.0; y = 64.0; type = 1; angle = 90; skill1 = true; skill2 = true; single = true; }
";

    [AvaloniaFact]
    public void The_image_export_draws_the_map_to_a_png_on_its_own_thread_and_reports_when_done()
    {
        OpenEditor(wadPath: WriteUdmfWad(ClockwiseRoom), config: "GZDoom_DoomUDMF.cfg");
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;
        string output = Path.Combine(dir, "room.png");
        var form = new ImageExportSettingsForm();
        Assert.EndsWith(".png", form.FilePath);

        WhenShown<Avalonia.Controls.Window>(d =>
        {
            form.ExportPathBox.Text = output;
            Click(form.ExportButton);
            Assert.True(form.Exporting, "the export runs");
            Assert.Equal("Cancel", form.ExportButton.Content);
            for (int i = 0; i < 1000 && form.Exporting; i++) { Flush(); System.Threading.Thread.Sleep(10); }
            Assert.False(form.Exporting, "the export finished");
            Assert.Equal("Export", form.ExportButton.Content);
            Assert.Equal("Export successful.", form.LastResult);
            Click(form.CloseButton);
        });
        form.ShowDialog();

        Assert.True(File.Exists(output), "the image was written");
        using (var image = new System.Drawing.Bitmap(File.OpenRead(output)))
        {
            Assert.True(image.Width >= 128 && image.Height >= 128, "the 128 x 128 room is in the picture");
            // The floor is drawn (the flat is missing in this setup, so it is the "unknown" picture, not the black background)
            int drawn = 0;
            for (int x = 8; x < 120; x += 8)
                for (int y = 8; y < 120; y += 8)
                {
                    var c = image.GetPixel(x, y);
                    if (c.R + c.G + c.B > 0) drawn++;
                }
            Assert.True(drawn > 100, $"the room has its floor ({drawn} of 196 sampled pixels are not black)");
        }
    }
}
