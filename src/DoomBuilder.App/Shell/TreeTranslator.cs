using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace DoomBuilder.App.Shell;

/// <summary>
/// Puts the fixed texts of the controls in the language in use as they are loaded: the dialogs and dockers of the plugins are built in English
/// in code, so this translates their labels, buttons, headers, tooltips and window titles without each plugin knowing about the language.
/// Only texts that have an entry of their own are touched (see <see cref="Loc.Exact"/>); editable text is never changed.
/// </summary>
internal static class TreeTranslator
{
    private static bool installed;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Control.LoadedEvent.AddClassHandler<Control>((c, _) => Translate(c));
    }

    internal static void Translate(Control c)
    {
        switch (c)
        {
            case Window w:
                if (w.Title is string title && Loc.Exact(title) is { } t) w.Title = t;
                break;
            case TextBox box:
                if (box.PlaceholderText is string mark && Loc.Exact(mark) is { } m) box.PlaceholderText = m;
                break;
            case TextBlock block:
                if (block.Inlines == null || block.Inlines.Count == 0)
                    if (!string.IsNullOrEmpty(block.Text) && Loc.Exact(block.Text) is { } b) block.Text = b;
                break;
            case HeaderedContentControl hc:
                if (hc.Header is string header && Loc.Exact(header) is { } h) hc.Header = h;
                TranslateContent(hc);
                break;
            case ContentControl cc:
                TranslateContent(cc);
                break;
        }

        // The tooltip text of any control
        if (ToolTip.GetTip(c) is string tip && Loc.Exact(tip) is { } tipt) ToolTip.SetTip(c, tipt);
    }

    private static void TranslateContent(ContentControl cc)
    {
        if (cc.Content is string s && Loc.Exact(s) is { } translated) cc.Content = translated;
    }
}
