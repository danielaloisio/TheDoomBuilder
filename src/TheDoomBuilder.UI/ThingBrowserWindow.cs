using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using CodeImp.DoomBuilder;

namespace DoomBuilder.UI;

/// <summary>UDB's "Browse thing type" dialog: the thing browser with OK and Cancel.</summary>
public sealed class ThingBrowserWindow : Window
{
    private readonly ThingBrowser browser = new ThingBrowser();
    private readonly Button ok = new Button { Content = "OK", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button cancel = new Button { Content = "Cancel", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

    public ThingBrowser Browser => browser;

    /// <summary>The type chosen (the one given when cancelled).</summary>
    public int SelectedType { get; private set; }

    public ThingBrowserWindow(int type)
    {
        Title = "Browse thing type";
        Width = 520;
        Height = 640;
        MinWidth = 400;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SelectedType = type;

        ok.Click += (s, e) => Accept();
        cancel.Click += (s, e) => Close(false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(browser);
        Content = layout;

        browser.SelectType(type);
        browser.TypeDoubleClicked += Accept;
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(false); e.Handled = true; } };
        Opened += (s, e) => browser.FocusFilter();
    }

    private void Accept()
    {
        SelectedType = browser.GetResult(SelectedType);
        Close(true);
    }
}
