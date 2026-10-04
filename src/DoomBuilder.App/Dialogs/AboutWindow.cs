using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using CodeImp.DoomBuilder;
using DoomBuilder.App.Shell;

namespace DoomBuilder.App.Dialogs;

/// <summary>Program name, version and the links of UDB's AboutForm.</summary>
public sealed class AboutWindow : Window
{
    private static readonly (string Label, string Url)[] Links =
    {
        ("TheDoomBuilder: port of Ultimate Doom Builder to Avalonia", "https://github.com/danielaloisio/TheDoomBuilder"),
        ("Ultimate Doom Builder", "https://github.com/jewalky/UltimateDoomBuilder"),
        ("Ultimate Doom Builder thread on the ZDoom forum", "https://forum.zdoom.org/viewtopic.php?f=232&t=66745"),
        ("GZDoom Builder (mxd)", "https://github.com/m-x-d/GZDoom-Builder"),
        ("Doom Builder (Pascal vd Heiden)", "http://www.doombuilder.com"),
    };

    public AboutWindow()
    {
        Title = "About";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };

        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(24), MinWidth = 380 };
        panel.Children.Add(new TextBlock { Text = ProductName, FontSize = 22, FontWeight = Avalonia.Media.FontWeight.Bold });
        VersionBlock = new TextBlock { Text = VersionText };
        panel.Children.Add(VersionBlock);
        panel.Children.Add(new TextBlock
        {
            Text = "Based on Doom Builder by Pascal vd Heiden, with the changes of GZDoom Builder and Ultimate Doom Builder. Released under the GNU General Public License.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 380, Opacity = 0.7, Margin = new Thickness(0, 6, 0, 6),
        });
        foreach (var (label, url) in Links)
        {
            var link = new Button { Content = label, Tag = url, Padding = new Thickness(0), Background = Avalonia.Media.Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, Cursor = new Cursor(StandardCursorType.Hand) };
            link.Classes.Add("link");
            link.Foreground = Avalonia.Media.Brushes.CornflowerBlue;
            link.Click += (s, e) => ShellCommands.OpenWebsiteInBrowser(url);
            panel.Children.Add(link);
        }

        var copy = new Button { Content = "Copy version" };
        copy.Click += async (s, e) =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null) try { await clipboard.SetTextAsync(PlainVersion); } catch (Exception) { }
        };
        var close = new Button { Content = "Close", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
        close.Click += (s, e) => Close();
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(copy);
        row.Children.Add(close);
        panel.Children.Add(row);
        Content = panel;
    }

    public TextBlock VersionBlock { get; }

    public static string ProductName => "TheDoomBuilder";

    /// <summary>The version number, without the commit hash a build from git adds after a plus sign.</summary>
    public static string PlainVersion
    {
        get
        {
            string version = typeof(General).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? typeof(General).Assembly.GetName().Version?.ToString() ?? "";
            int plus = version.IndexOf('+');
            return plus >= 0 ? version.Substring(0, plus) : version;
        }
    }

    public static string VersionText
    {
        get
        {
            string hash = General.CommitHash;
            return ProductName + " v" + PlainVersion + (string.IsNullOrEmpty(hash) ? "" : " (" + hash + ")");
        }
    }
}
