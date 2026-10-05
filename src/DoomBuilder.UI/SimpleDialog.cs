using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace DoomBuilder.UI;

/// <summary>
/// A small modal dialog: some content with OK and Cancel under it. OK asks <see cref="Validate"/> first (return false to stay
/// open); Escape and the close button mean Cancel. The answer is the dialog's result (true = OK), see <see cref="DialogHost.ShowModal"/>.
/// </summary>
public class SimpleDialog : Window
{
    private bool answered;

    public Button OkButton { get; } = new Button { Content = "OK", MinWidth = 80, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button CancelButton { get; } = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>Extra buttons next to OK and Cancel (on the left, like "Toggle All").</summary>
    public StackPanel ExtraButtons { get; } = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

    /// <summary>Called when OK is pressed; false keeps the dialog open.</summary>
    public Func<bool> Validate { get; set; }

    /// <summary>True when the user pressed OK.</summary>
    public bool Accepted { get; private set; }

    public SimpleDialog(string title, Control content, double width = double.NaN)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        if (!double.IsNaN(width)) { Width = width; SizeToContent = SizeToContent.Height; }
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        OkButton.Click += (s, e) => Finish(true);
        CancelButton.Click += (s, e) => Finish(false);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(OkButton);
        right.Children.Add(CancelButton);
        var buttons = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };
        DockPanel.SetDock(ExtraButtons, Dock.Left);
        buttons.Children.Add(ExtraButtons);
        buttons.Children.Add(right);

        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(content);
        Content = layout;

        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Finish(false); e.Handled = true; } };
        Closing += (s, e) => { answered = true; };
    }

    private void Finish(bool ok)
    {
        if (answered) return;
        if (ok && Validate != null && !Validate()) return;
        answered = true;
        Accepted = ok;
        Close(ok);
    }
}
