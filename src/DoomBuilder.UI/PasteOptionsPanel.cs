using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Config;

namespace DoomBuilder.UI;

/// <summary>UDB's PasteOptionsControl: what to do with the tags and actions of pasted elements.</summary>
public sealed class PasteOptionsPanel : UserControl
{
    private readonly RadioButton keeptags = new RadioButton { Content = "Keep tags the same as they were copied", GroupName = "tags" };
    private readonly RadioButton renumbertags = new RadioButton { Content = "Renumber tags to resolve conflicts with existing tags", GroupName = "tags" };
    private readonly RadioButton removetags = new RadioButton { Content = "Remove all tags", GroupName = "tags" };
    private readonly CheckBox removeactions = new CheckBox { Content = "Remove all actions", Margin = new Thickness(0, 6, 0, 0) };

    public PasteOptionsPanel()
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = "Tags and Actions", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        stack.Children.Add(keeptags);
        stack.Children.Add(renumbertags);
        stack.Children.Add(removetags);
        stack.Children.Add(removeactions);
        Content = stack;
    }

    /// <summary>Shows <paramref name="options"/>.</summary>
    public void Setup(PasteOptions options)
    {
        keeptags.IsChecked = options.ChangeTags == PasteOptions.TAGS_KEEP;
        renumbertags.IsChecked = options.ChangeTags == PasteOptions.TAGS_RENUMBER;
        removetags.IsChecked = options.ChangeTags == PasteOptions.TAGS_REMOVE;
        removeactions.IsChecked = options.RemoveActions;
    }

    /// <summary>The options as set by the user.</summary>
    public PasteOptions GetOptions()
    {
        var options = new PasteOptions();
        if (keeptags.IsChecked == true) options.ChangeTags = PasteOptions.TAGS_KEEP;
        else if (renumbertags.IsChecked == true) options.ChangeTags = PasteOptions.TAGS_RENUMBER;
        else if (removetags.IsChecked == true) options.ChangeTags = PasteOptions.TAGS_REMOVE;
        options.RemoveActions = removeactions.IsChecked == true;
        return options;
    }
}
