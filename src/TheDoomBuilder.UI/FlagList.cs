using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>A list of flags as check boxes (UDB's CheckboxArrayControl): three states, the middle one meaning the elements disagree.</summary>
public sealed class FlagList : UserControl
{
    private readonly WrapPanel panel = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
    private FlagSetModel model;

    /// <summary>A flag was clicked (its key and the new value).</summary>
    public event Action<string, bool> FlagChanged;

    public FlagList() { Content = new ScrollViewer { Content = panel }; }

    public IEnumerable<CheckBox> Boxes => panel.Children.OfType<CheckBox>();

    public void Setup(FlagSetModel flags)
    {
        model = flags;
        panel.Children.Clear();
        foreach (FlagItem item in flags.Items)
        {
            var box = new CheckBox { Content = item.Title, IsThreeState = true, Tag = item, Margin = new Thickness(0, 0, 16, 2), Width = 190 };
            box.IsChecked = item.Value;
            box.IsCheckedChanged += (s, e) =>
            {
                // A click on an undetermined flag turns it on; the model never goes back to undetermined by itself
                bool? state = box.IsChecked;
                if (state == null) { box.IsChecked = true; return; }
                if (item.Value == state) return;
                item.Value = state;
                FlagChanged?.Invoke(item.Key, state.Value);
            };
            panel.Children.Add(box);
        }
    }
}
