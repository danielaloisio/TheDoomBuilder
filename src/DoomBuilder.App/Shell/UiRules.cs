using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.App.Shell;

/// <summary>What the UI shows of an item right now.</summary>
public struct ItemState
{
    public bool Visible;
    public bool Enabled;
    public bool Checked;
    /// <summary>Replacement caption (Undo shows what it would undo), or null to keep the original.</summary>
    public string Text;
}

/// <summary>
/// Visibility, enabled and checked state of the menu and toolbar items, ported from UDB's MainForm.UpdateInterface
/// (UpdateFileMenu, UpdateEditMenu, UpdateViewMenu, UpdateToolsMenu, UpdateToolbar...). Rules are keyed by the item's
/// designer name; items without a rule are always visible and enabled.
/// </summary>
public static class UiRules
{
    private static bool MapOpen { get { return General.Map != null; } }
    private static bool Udmf { get { return General.Map != null && General.Map.UDMF; } }
    private static bool ModeAllowsCopyPaste { get { return MapOpen && General.Editing.Mode != null && General.Editing.Mode.Attributes.AllowCopyPaste; } }

    private static readonly HashSet<string> VisibleOnlyWithMap = new HashSet<string>(StringComparer.Ordinal)
    {
        // menus and items of the File menu that need a map
        "menuedit", "menuview", "menumode", "menuprefabs",
        "itemclosemap", "itemsavemap", "itemsavemapas", "itemsavemapinto", "itemopenmapincurwad", "itemimport", "itemexport",
        "seperatorfileopen", "seperatorfilesave", "buttonsavemap",
        "itemreloadresources", "seperatortoolsconfig", "itemsavescreenshot", "itemsaveeditareascreenshot",
        "separatortoolsscreenshots", "itemtestmap", "itemhelpeditmode",
        "itemtogglefixedthingsscale",
    };

    /// <summary>The state of an item, or null when no rule applies (the item keeps its defaults).</summary>
    public static ItemState? For(UiItem item)
    {
        string name = item.Name;
        bool map = MapOpen;

        // The window is built before the editor starts: no settings, no map, so only what needs neither is usable
        if (General.Settings == null)
            return VisibleOnlyWithMap.Contains(name) || name.StartsWith("button") ? new ItemState { Visible = false, Enabled = false } : (ItemState?)null;

        // the extra rules first: they refine the generic "needs a map"
        switch (name)
        {
            case "itemundo":
            case "buttonundo":
            {
                bool can = map && General.Map.UndoRedo.NextUndo != null;
                return new ItemState { Visible = name == "itemundo" || (map && General.Settings.ToolbarUndo), Enabled = can,
                    Text = can ? "Undo " + General.Map.UndoRedo.NextUndo.Description : "Undo" };
            }
            case "itemredo":
            case "buttonredo":
            {
                bool can = map && General.Map.UndoRedo.NextRedo != null;
                return new ItemState { Visible = name == "itemredo" || (map && General.Settings.ToolbarUndo), Enabled = can,
                    Text = can ? "Redo " + General.Map.UndoRedo.NextRedo.Description : "Redo" };
            }
            case "itemcut": case "itemcopy": case "itempaste": case "itempastespecial":
            case "buttoncut": case "buttoncopy": case "buttonpaste":
                return new ItemState { Visible = name.StartsWith("item") || (map && General.Settings.ToolbarCopy), Enabled = ModeAllowsCopyPaste };

            case "itemsplitjoinedsectors": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.SplitJoinedSectors };
            case "itemautoclearsidetextures": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.AutoClearSidedefTextures };
            case "itemsnaptogrid": return new ItemState { Visible = true, Enabled = map, Checked = General.Interface.SnapToGrid };
            case "itemautomerge": return new ItemState { Visible = true, Enabled = map, Checked = General.Interface.AutoMerge };
            case "itemdynamicgridsize": return new ItemState { Visible = true, Enabled = map, Checked = General.Settings.DynamicGridSize };

            case "itemfullbrightness": return new ItemState { Visible = true, Enabled = true, Checked = Renderer.FullBrightness };
            case "itemtogglegrid": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.RenderGrid };
            case "itemtogglecomments": return new ItemState { Visible = Udmf, Enabled = true, Checked = General.Settings.RenderComments };
            case "itemtogglefixedthingsscale": return new ItemState { Visible = map, Enabled = true, Checked = General.Settings.FixedThingsScale };
            case "itemtogglefog": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.GZDrawFog };
            case "itemtogglesky": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.GZDrawSky };
            case "itemtoggleclassicrendering": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.ClassicRendering };
            case "itemtoggleeventlines": return new ItemState { Visible = true, Enabled = true, Checked = General.Settings.GZShowEventLines };
            case "itemtogglevisualverts": return new ItemState { Visible = Udmf, Enabled = true, Checked = General.Settings.GZShowVisualVertices };

            case "itemhelpeditmode":
                return new ItemState { Visible = map, Enabled = map && General.Editing.Mode != null };

            case "itemReloadGldefs":
            case "itemReloadModedef":
                return new ItemState { Visible = map && !string.IsNullOrEmpty(General.Map.Config.DecorateGames), Enabled = true };

            // toolbar groups follow the user's toolbar settings and need a map
            case "buttonscripteditor": return Toolbar(General.Settings.ToolbarScript && map && General.Map.Config.HasScriptLumps());
            case "buttoninsertprefabfile": case "buttoninsertpreviousprefab": return Toolbar(General.Settings.ToolbarPrefabs && map);
            case "buttonthingsfilter": case "thingfilters": case "separatorlinecolors": case "buttonlinededfcolors": case "linedefcolorpresets":
                return Toolbar(General.Settings.ToolbarFilter && map);
            case "buttontest": return Toolbar(General.Settings.ToolbarTesting && map);
            case "buttonfullbrightness": return Toolbar(General.Settings.ToolbarViewModes && map, Renderer.FullBrightness);
            case "buttontogglegrid": return Toolbar(General.Settings.ToolbarViewModes && map, General.Settings.RenderGrid);
            case "buttontogglecomments": return Toolbar(General.Settings.ToolbarViewModes && map && Udmf, General.Settings.RenderComments);
            case "buttontogglefixedthingsscale": return Toolbar(General.Settings.ToolbarViewModes && map, General.Settings.FixedThingsScale);
            case "separatorfilters": case "separatorfullbrightness": case "buttonviewbrightness": case "buttonviewceilings":
            case "buttonviewfloors": case "buttonviewnormal": case "buttontoggleclassicrendering":
                return Toolbar(General.Settings.ToolbarViewModes && map);
            case "separatorgeomergemodes": case "buttonmergegeoclassic": case "buttonmergegeo": case "buttonplacegeo":
                return Toolbar(General.Settings.ToolbarGeometry && map);
            case "buttonsnaptogrid": return Toolbar(General.Settings.ToolbarGeometry && map, General.Interface.SnapToGrid);
            case "buttonautomerge": return Toolbar(General.Settings.ToolbarGeometry && map, General.Interface.AutoMerge);
            case "buttontoggledynamicgrid": return Toolbar(General.Settings.ToolbarGeometry && map, General.Settings.DynamicGridSize);
            case "buttonsplitjoinedsectors": return Toolbar(General.Settings.ToolbarGeometry && map, General.Settings.SplitJoinedSectors);
            case "buttonautoclearsidetextures": return Toolbar(General.Settings.ToolbarGeometry && map, General.Settings.AutoClearSidedefTextures);
            case "modelrendermode": case "dynamiclightmode": case "buttontoggleeventlines": case "separatorgzmodes":
                return Toolbar(General.Settings.GZToolbarGZDoom && map);
            case "buttontogglefog": return Toolbar(General.Settings.GZToolbarGZDoom && map, General.Settings.GZDrawFog);
            case "buttontogglesky": return Toolbar(General.Settings.GZToolbarGZDoom && map, General.Settings.GZDrawSky);
            case "buttontogglevisualvertices": return Toolbar(General.Settings.GZToolbarGZDoom && map && Udmf, General.Settings.GZShowVisualVertices);
            case "buttonnewmap": case "buttonopenmap": return Toolbar(General.Settings.ToolbarFile);
        }

        if (VisibleOnlyWithMap.Contains(name)) return new ItemState { Visible = map, Enabled = true };
        return null;
    }

    private static ItemState Toolbar(bool visible, bool isChecked = false)
        => new ItemState { Visible = visible, Enabled = true, Checked = isChecked };
}
