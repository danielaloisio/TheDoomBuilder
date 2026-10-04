using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomBuilder.App.Shell;

/// <summary>
/// One menu entry, toolbar button or separator of the main window. The model is extracted from UDB's WinForms MainForm
/// designer (tools/extract_mainform_ui.py) and embedded as mainform-ui.json, so the Avalonia shell has the same menus and
/// toolbars, with the same action names, as the original.
/// </summary>
public sealed class UiItem
{
    /// <summary>menu, button, dropdown, split, separator, label, combo, statuslabel</summary>
    public string Type { get; set; }
    public string Name { get; set; }

    /// <summary>Caption; "&amp;" marks the access key like in WinForms.</summary>
    public string Text { get; set; }
    public string Tooltip { get; set; }

    /// <summary>The action to invoke (builder_newmap...) or, for a few items, a numeric/enum argument for <see cref="Handler"/>.</summary>
    public string Action { get; set; }

    /// <summary>Name of the resource in UDB's Properties.Resources (the icon).</summary>
    public string Image { get; set; }
    public string Shortcut { get; set; }
    public bool CheckOnClick { get; set; }
    public bool Checked { get; set; }
    public string DisplayStyle { get; set; }

    /// <summary>InvokeTaggedAction (invoke <see cref="Action"/>) or the name of one of the few custom handlers.</summary>
    public string Handler { get; set; }

    public List<UiItem> Items { get; set; } = new List<UiItem>();

    [JsonIgnore]
    public bool IsSeparator { get { return Type == "separator"; } }

    [JsonIgnore]
    public bool InvokesAction { get { return Handler == "InvokeTaggedAction" && !string.IsNullOrEmpty(Action); } }

    /// <summary>Text without the access-key marker.</summary>
    [JsonIgnore]
    public string PlainText { get { return (Text ?? string.Empty).Replace("&", string.Empty); } }

    /// <summary>Every item below this one, depth first.</summary>
    public IEnumerable<UiItem> Descendants()
    {
        foreach (UiItem child in Items)
        {
            yield return child;
            foreach (UiItem grandchild in child.Descendants()) yield return grandchild;
        }
    }
}

/// <summary>A menu strip, tool strip or status strip.</summary>
public sealed class UiStrip
{
    public string Kind { get; set; }
    public List<UiItem> Items { get; set; } = new List<UiItem>();

    public IEnumerable<UiItem> AllItems()
    {
        foreach (UiItem item in Items)
        {
            yield return item;
            foreach (UiItem child in item.Descendants()) yield return child;
        }
    }
}

public static class UiModel
{
    private static Dictionary<string, UiStrip> cache;

    /// <summary>The strips of the main window by name: menumain, toolbar, statusbar.</summary>
    public static IReadOnlyDictionary<string, UiStrip> Load()
    {
        if (cache != null) return cache;

        Assembly asm = typeof(UiModel).Assembly;
        using Stream stream = asm.GetManifestResourceStream("DoomBuilder.App.Resources.mainform-ui.json")
            ?? throw new FileNotFoundException("The embedded mainform-ui.json is missing.");

        cache = JsonSerializer.Deserialize<Dictionary<string, UiStrip>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("mainform-ui.json is empty.");
        return cache;
    }
}
