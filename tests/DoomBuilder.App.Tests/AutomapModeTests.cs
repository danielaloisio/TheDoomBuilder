using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.AutomapMode;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The automap mode: the map as the automap of the game shows it, with its own toolbar items.</summary>
public class AutomapModeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static T Private<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    private static MenusForm Menus => Private<MenusForm>(General.Editing.Mode, "menusform");
    private static List<Linedef> Valid => Private<List<Linedef>>(General.Editing.Mode, "validlinedefs");

    private void Engage()
    {
        General.Actions.InvokeAction("automapmode_automapmode");
        Flush();
        Assert.Equal("AutomapMode", General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void The_mode_is_registered_and_its_hidden_lines_follow_the_toolbar_button()
    {
        OpenEditor();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "AutomapMode");
        Engage();

        // Hidden lines are not valid (not shown) unless the button is down
        var line = General.Map.Map.Linedefs.First();
        int all = General.Map.Map.Linedefs.Count;
        Assert.False(Menus.ShowHiddenLines);
        line.SetFlag("128", true);                                   // "don't draw on the automap"
        Menus.HiddenLinesButton.PerformClick();                      // down
        Assert.True(Menus.ShowHiddenLines);
        Assert.Contains(line, Valid);
        Menus.HiddenLinesButton.PerformClick();                      // up
        Assert.False(Menus.ShowHiddenLines);
        Assert.DoesNotContain(line, Valid);
        Assert.True(Valid.Count < all);
    }

    [AvaloniaFact]
    public void The_color_preset_is_chosen_in_the_toolbar_and_the_choices_are_kept()
    {
        OpenEditor();
        Engage();
        Assert.Equal(new object[] { "Doom", "Hexen", "Strife" }, Menus.ColorPresetBox.Items.ToArray());
        Assert.Equal(0, Menus.ColorPresetBox.SelectedIndex);
        Menus.ColorPresetBox.SelectedIndex = 2;
        Assert.Equal(2, Menus.ColorPresetBox.SelectedIndex);
        Menus.TexturesButton.PerformClick();
        bool textures = Menus.ShowTextures;

        General.Editing.ChangeMode("VerticesMode");                  // leaving the mode keeps its settings
        Flush();
        Engage();
        Assert.Equal(2, Menus.ColorPresetBox.SelectedIndex);
        Assert.Equal(textures, Menus.ShowTextures);
    }
}
