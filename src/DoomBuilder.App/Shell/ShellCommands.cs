using System;
using System.Diagnostics;
using System.Globalization;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App.Shell;

/// <summary>
/// What clicking a menu entry or toolbar button does. Most items just invoke an action by name (InvokeTaggedAction); the
/// rest are the handful of custom handlers MainForm had (zoom to, grid size, rendering modes, exit, help links).
/// </summary>
public sealed class ShellCommands
{
    private readonly Action exit;
    private readonly Action<string> openWebsite;

    public ShellCommands(Action exit, Action<string> openWebsite)
    {
        this.exit = exit;
        this.openWebsite = openWebsite;
    }

    /// <summary>Runs the command of an item. Returns false when nothing is wired to it yet.</summary>
    public bool Invoke(UiItem item)
    {
        if (item == null) return false;

        if (item.InvokesAction)
        {
            if (General.Actions == null || !General.Actions.Exists(item.Action)) return false;
            General.Actions.InvokeAction(item.Action);
            return true;
        }

        switch (item.Handler)
        {
            case "itemexit_Click": exit(); return true;
            case "itemzoomto_Click": return ZoomTo(item);
            case "itemgridsize_Click": return GridSize(item);
            case "ChangeModelRenderingMode": return ModelMode(item);
            case "ChangeLightRenderingMode": return LightMode(item);
            case "itemzoomfittoscreen_Click": return Classic(mode => mode.CenterInScreen());
            case "itemgridcustom_Click": if (General.Map == null) return false; GridSetup.ShowGridSetup(); return true;
            case "itemhelpissues_Click": openWebsite("https://github.com/jewalky/GZDoom-Builder-Bugfix/issues"); return true;
            case "itemhelpeditmode_Click":
                if (General.Map == null || General.Editing.Mode == null) return false;
                General.Editing.Mode.OnHelp();
                return true;
            case "itemhelprefmanual_Click": General.ShowHelp("introduction.html"); return true;
            case "itemhelpabout_Click": General.Dialogs.ShowAbout(); return true;
            case "warnsLabel_Click": General.MainWindow.ShowErrors(); return true;
            case "itemopenconfigfolder_Click": return OpenFolder(General.SettingsPath);
            default: return false;
        }
    }

    // Runs something on the classic (2D) mode when there is one
    private static bool Classic(Action<ClassicMode> command)
    {
        if (General.Map == null || !(General.Editing.Mode is ClassicMode mode)) return false;
        command(mode);
        return true;
    }

    private static bool ZoomTo(UiItem item)
    {
        int zoom = int.Parse(item.Action, CultureInfo.InvariantCulture);
        return Classic(mode => mode.SetZoom(zoom / 100f));
    }

    private static bool GridSize(UiItem item)
    {
        float size = float.Parse(item.Action, CultureInfo.InvariantCulture);
        return Classic(mode =>
        {
            General.MainWindow.DisableDynamicGridResize();   // a size picked by hand turns the automatic one off
            General.Map.Grid.SetGridSize(size);
            General.MainWindow.RedrawDisplay();
        });
    }

    private static bool ModelMode(UiItem item)
    {
        General.Settings.GZDrawModelsMode = (ModelRenderMode)int.Parse(item.Action, CultureInfo.InvariantCulture);
        string text;
        switch (General.Settings.GZDrawModelsMode)
        {
            case ModelRenderMode.NONE: text = "NONE"; break;
            case ModelRenderMode.SELECTION: text = "SELECTION ONLY"; break;
            case ModelRenderMode.ACTIVE_THINGS_FILTER: text = "ACTIVE THINGS FILTER ONLY"; break;
            default: text = "ALL"; break;
        }
        General.MainWindow.DisplayStatus(StatusType.Action, "Models rendering mode: " + text);
        General.MainWindow.UpdateInterface();
        General.MainWindow.UpdateGZDoomPanel();
        General.MainWindow.RedrawDisplay();
        return true;
    }

    private static bool LightMode(UiItem item)
    {
        General.Settings.GZDrawLightsMode = (LightRenderMode)int.Parse(item.Action, CultureInfo.InvariantCulture);
        string text;
        switch (General.Settings.GZDrawLightsMode)
        {
            case LightRenderMode.NONE: text = "NONE"; break;
            case LightRenderMode.ALL_ANIMATED: text = "ANIMATED"; break;
            default: text = "ALL"; break;
        }
        General.MainWindow.DisplayStatus(StatusType.Action, "Dynamic lights rendering mode: " + text);
        General.MainWindow.UpdateInterface();
        General.MainWindow.UpdateGZDoomPanel();
        General.MainWindow.RedrawDisplay();
        return true;
    }

    // Opens a folder in the platform's file manager
    private static bool OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path)) return false;
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            else Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", "\"" + path + "\"");
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Opens a web page in the default browser.</summary>
    public static void OpenWebsiteInBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url);
        }
        catch (Exception) { }
    }
}
