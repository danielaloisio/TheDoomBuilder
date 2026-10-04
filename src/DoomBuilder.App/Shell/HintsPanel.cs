using Avalonia;
using Avalonia.Controls;

namespace DoomBuilder.App.Shell;

/// <summary>The "Help" docker: the hints of the active edit mode. They are RTF in the modes, shown here as plain text.</summary>
public sealed class HintsPanel : UserControl
{
    private readonly SelectableTextBlock text = new SelectableTextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public HintsPanel()
    {
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(10), Child = text } };
    }

    /// <summary>What the panel shows now.</summary>
    public string Text => text.Text ?? string.Empty;

    public void SetHints(string rtf) => text.Text = RtfText.ToPlain(rtf);

    public void ClearHints() => text.Text = string.Empty;
}
