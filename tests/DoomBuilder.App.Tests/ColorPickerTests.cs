using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.ColorPicker;
using CodeImp.DoomBuilder.ColorPicker.Controls;
using CodeImp.DoomBuilder.ColorPicker.Windows;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The color picker plugin: a window to pick the color of sectors (UDMF) and the color and radii of dynamic lights.</summary>
public class ColorPickerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // The sample sector and a point light (red, radius 64)
    private const string Map = UdmfSample + "thing { x = 32.0; y = 32.0; type = 9800; arg0 = 255; arg1 = 0; arg2 = 0; arg3 = 64; skill1 = true; skill2 = true; single = true; }\n";

    private void OpenMap() => OpenEditor(wadPath: WriteUdmfWad(Map), config: "GZDoom_DoomUDMF.cfg");

    private static Sector Sector => General.Map.Map.Sectors.First();
    private static Thing Light => General.Map.Map.Things.First(t => t.Type == 9800);

    private void SelectSector()
    {
        General.Editing.ChangeMode("SectorsMode");
        General.Map.Map.ClearAllSelected();
        Sector.Selected = true;
    }

    private void SelectLight()
    {
        General.Editing.ChangeMode("ThingsMode");
        General.Map.Map.ClearAllSelected();
        Light.Selected = true;
    }

    // ---------------------------------------------------------------- the math

    [Fact]
    public void Colors_go_to_HSV_and_back_with_all_values_from_0_to_255()
    {
        var red = ColorHandler.RGBtoHSV(new ColorHandler.RGB(255, 0, 0));
        Assert.Equal((0, 255, 255), (red.Hue, red.Saturation, red.value));
        var green = ColorHandler.RGBtoHSV(new ColorHandler.RGB(0, 255, 0));
        Assert.InRange(green.Hue, 85, 86);                            // a third of the way around
        var gray = ColorHandler.RGBtoHSV(new ColorHandler.RGB(128, 128, 128));
        Assert.Equal((0, 0, 128), (gray.Hue, gray.Saturation, gray.value));

        foreach (var c in new[] { (255, 128, 0), (10, 200, 90), (0, 0, 255), (255, 255, 255), (64, 64, 200) })
        {
            var back = ColorHandler.HSVtoRGB(ColorHandler.RGBtoHSV(new ColorHandler.RGB(c.Item1, c.Item2, c.Item3)));
            Assert.InRange(back.Red, c.Item1 - 5, c.Item1 + 5);
            Assert.InRange(back.Green, c.Item2 - 5, c.Item2 + 5);
            Assert.InRange(back.Blue, c.Item3 - 5, c.Item3 + 5);
        }
    }

    // ---------------------------------------------------------------- the plugin

    [AvaloniaFact]
    public void The_plugin_is_there_for_maps_that_are_not_Doom_maps_only()
    {
        OpenMap();
        Assert.NotNull(General.Actions.GetActionByName("colorpicker_togglelightpannel"));
        Assert.NotNull(BuilderPlug.Me.Tools);
        Assert.EndsWith("togglelightpannel", (string)BuilderPlug.Me.Tools.Button.Tag);
        Assert.EndsWith("togglelightpannel", (string)BuilderPlug.Me.Tools.Menu.Tag);
    }

    [AvaloniaFact]
    public void The_action_refuses_modes_that_have_no_colors()
    {
        OpenMap();
        General.Editing.ChangeMode("VerticesMode");
        General.Actions.InvokeAction("colorpicker_togglelightpannel");     // only a status message: no window (it would wait for an answer)
        Flush();
        Assert.Null(BuilderPlug.Me.Picker);

        // Things mode, but nothing selected or highlighted
        General.Editing.ChangeMode("ThingsMode");
        General.Map.Map.ClearAllSelected();
        General.Actions.InvokeAction("colorpicker_togglelightpannel");
        Flush();
        Assert.Null(BuilderPlug.Me.Picker);
    }

    // ---------------------------------------------------------------- the wheel

    [AvaloniaFact]
    public void The_wheel_picks_hue_and_saturation_on_the_disk_and_brightness_on_the_bar()
    {
        var wheel = new ColorWheelControl();
        ColorChangedEventArgs last = null;
        wheel.ColorChanged += (s, e) => last = e;
        wheel.SetColor(new ColorHandler.RGB(255, 255, 255));
        Assert.Null(last);                                              // showing a color is not picking one

        // East of the center: hue 0, half way out: a pale red
        wheel.Press(new Point(88 + 44, 88));
        Assert.NotNull(last);
        Assert.Equal(0, last.HSV.Hue);
        Assert.InRange(last.HSV.Saturation, 125, 129);
        Assert.Equal(255, last.RGB.Red);
        Assert.InRange(last.RGB.Green, 125, 130);
        wheel.Release();

        // North of the center, on the edge: hue 90 degrees
        wheel.Press(new Point(88, 0));
        Assert.Equal(90 * 255 / 360, last.HSV.Hue);
        Assert.InRange(last.HSV.Saturation, 250, 255);

        // Dragging out of the disk keeps the pointer on its edge
        wheel.Drag(new Point(88 + 500, 88));
        Assert.Equal(0, last.HSV.Hue);
        Assert.Equal(255, last.HSV.Saturation);
        Assert.Equal(88 + 88, wheel.ColorPoint.X);
        wheel.Release();

        // The bar: halfway down is half the brightness; above and below it is clamped
        wheel.Press(new Point(190, 88));
        Assert.InRange(last.HSV.value, 125, 129);
        wheel.Drag(new Point(190, -50));
        Assert.Equal(255, last.HSV.value);
        wheel.Drag(new Point(190, 900));
        Assert.Equal(0, last.HSV.value);
        wheel.Release();

        // A click outside of both does nothing
        last = null;
        wheel.Press(new Point(170, 175));
        Assert.Null(last);
    }

    // ---------------------------------------------------------------- sectors

    [AvaloniaFact]
    public void The_light_color_of_the_selected_sectors_follows_the_picker_and_OK_keeps_it()
    {
        OpenMap();
        SelectSector();
        Assert.False(Sector.Fields.ContainsKey("lightcolor"));
        int undos = General.Map.UndoRedo.GetUndoList().Count;

        bool shown = false;
        WhenShown<SectorColorPicker>(w =>
        {
            shown = true;
            Assert.Equal("Editing 1 sector", w.Title);
            w.SectorColorButton.IsChecked = true;                  // (the picker remembers which color was edited last)

            // Hex mode: type the color
            w.Picker.InfoBox.SelectedIndex = 1;
            Assert.True(w.Picker.TextValues.IsVisible);
            w.Picker.TextValues.Text = "FF8000";
            Assert.Equal(0xFF8000, (int)Sector.Fields["lightcolor"].Value);              // live in the map
            Assert.Equal(255, w.Picker.CurrentColor.Red);

            // RGB mode: the numbers
            w.Picker.InfoBox.SelectedIndex = 0;
            w.Picker.GreenBox.Value = 64;
            Assert.Equal(0xFF4000, (int)Sector.Fields["lightcolor"].Value);

            // The fade color: its own value, which starts at black
            w.FadeColorButton.IsChecked = true;
            Assert.Equal(0, w.Picker.CurrentColor.Red);
            w.Picker.GreenBox.Value = 20;
            Assert.Equal(0x001400, (int)Sector.Fields["fadecolor"].Value);
            Assert.Equal(0xFF4000, (int)Sector.Fields["lightcolor"].Value);
            w.Picker.GreenBox.Value = 0;

            Click(w.Picker.OkButton);
        });
        General.Actions.InvokeAction("colorpicker_togglelightpannel");
        Flush();
        Assert.True(shown);
        Assert.Equal(0xFF4000, (int)Sector.Fields["lightcolor"].Value);
        Assert.False(Sector.Fields.ContainsKey("fadecolor"));                           // the default is not stored
        Assert.Equal(undos + 1, General.Map.UndoRedo.GetUndoList().Count);
        Assert.Equal("Edit color of 1 sector", General.Map.UndoRedo.NextUndo.Description);
    }

    [AvaloniaFact]
    public void Cancel_withdraws_what_the_picker_did_to_the_sectors()
    {
        OpenMap();
        SelectSector();
        int undos = General.Map.UndoRedo.GetUndoList().Count;

        bool shown = false;
        WhenShown<SectorColorPicker>(w =>
        {
            shown = true;
            w.SectorColorButton.IsChecked = true;                  // (the picker remembers which color was edited last)
            w.Picker.InfoBox.SelectedIndex = 1;
            w.Picker.TextValues.Text = "00FF00";
            Assert.Equal(0x00FF00, (int)Sector.Fields["lightcolor"].Value);
            Click(w.Picker.CancelButton);
        });
        General.Actions.InvokeAction("colorpicker_togglelightpannel");
        Flush();
        Assert.True(shown);
        Assert.False(Sector.Fields.ContainsKey("lightcolor"));
        Assert.Equal(undos, General.Map.UndoRedo.GetUndoList().Count);
    }

    // ---------------------------------------------------------------- lights

    [AvaloniaFact]
    public void The_color_and_the_radius_of_a_light_follow_the_picker()
    {
        OpenMap();
        SelectLight();

        bool shown = false;
        WhenShown<LightColorPicker>(w =>
        {
            shown = true;
            Assert.Equal("Editing 1 light", w.Title);
            Assert.Equal(64, w.Slider1.Value);                                          // the radius of the light
            Assert.False(w.Slider2.IsVisible);                                          // a point light has one radius
            Assert.Equal(255, w.Picker.CurrentColor.Red);

            w.Picker.InfoBox.SelectedIndex = 1;
            w.Picker.TextValues.Text = "00FF40";
            Assert.Equal(new[] { 0, 255, 0x40 }, Light.Args.Take(3).ToArray());
            Assert.Equal(64, Light.Args[3]);

            w.Slider1.Number.Value = 128;
            Assert.Equal(128, Light.Args[3]);
            Click(w.Slider1.Presets[2]);                                                // the 256 button
            Assert.Equal(256, Light.Args[3]);
            Click(w.Picker.OkButton);
        });
        General.Actions.InvokeAction("colorpicker_togglelightpannel");
        Flush();
        Assert.True(shown);
        Assert.Equal(new[] { 0, 255, 0x40, 256 }, Light.Args.Take(4).ToArray());
        Assert.Equal("Editing 1 light", General.Map.UndoRedo.NextUndo.Description);       // (the title of the window, like in UDB)
    }

    [AvaloniaFact]
    public void In_relative_mode_the_sliders_add_to_what_the_lights_have_and_cancel_goes_back()
    {
        OpenMap();
        SelectLight();

        WhenShown<LightColorPicker>(w =>
        {
            w.RelativeModeBox.IsChecked = true;
            Assert.Equal(0, w.Slider1.Value);                                           // relative: nothing added yet
            w.Slider1.Number.Value = 36;
            Assert.Equal(64 + 36, Light.Args[3]);
            w.Slider1.Number.Value = -100;                                              // never below 0
            Assert.Equal(0, Light.Args[3]);
            w.RelativeModeBox.IsChecked = false;                                        // (the choice is remembered by the picker)
            Click(w.Picker.CancelButton);
        });
        General.Actions.InvokeAction("colorpicker_togglelightpannel");
        Flush();
        Assert.Equal(64, Light.Args[3]);
        Assert.Equal(255, Light.Args[0]);
    }
}
