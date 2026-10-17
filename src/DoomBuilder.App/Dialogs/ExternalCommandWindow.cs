using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Linq;
using CodeImp.DoomBuilder.Windows;

using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// Runs an external command and shows what it prints (UDB's RunExternalCommandForm). It starts when it opens, closes by itself when
/// the command succeeded and the settings say so, and otherwise waits for "Continue" (answer OK), "Run again" or "Cancel".
/// It cannot be closed while the command runs (Cancel kills it).
/// </summary>
public sealed class ExternalCommandWindow : Window
{
    private readonly ExternalCommandRunner runner;
    private readonly bool autoclose;
    private readonly SelectableTextBlock output = new SelectableTextBlock { FontFamily = new FontFamily("Consolas, Menlo, DejaVu Sans Mono, monospace"), TextWrapping = TextWrapping.NoWrap };
    private readonly ScrollViewer scroll;

    public Button ContinueButton { get; } = new Button { Content = Loc.T("Continue"), MinWidth = 90, IsEnabled = false, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button RetryButton { get; } = new Button { Content = Loc.T("Run again"), MinWidth = 90, IsEnabled = false, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button CancelButton { get; } = new Button { Content = Loc.T("Cancel"), MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>The text shown so far (tests read it).</summary>
    public string OutputText => string.Concat(output.Inlines.OfType<Run>().Select(r => r.Text));

    public ExternalCommandWindow(ExternalCommandRunner runner, bool autoCloseOnSuccess)
    {
        this.runner = runner;
        autoclose = autoCloseOnSuccess;
        Title = Loc.T("Running external command");
        Width = 720;
        Height = 420;
        MinWidth = 400;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        scroll = new ScrollViewer { Content = output, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(ContinueButton);
        buttons.Children.Add(RetryButton);
        buttons.Children.Add(CancelButton);
        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(4), Child = scroll });
        Content = layout;

        runner.OutputReceived += (text, iserror) => Dispatcher.UIThread.Post(() => Append(text + Environment.NewLine, iserror));
        runner.Finished += () => Dispatcher.UIThread.Post(OnFinished);

        ContinueButton.Click += (s, e) => Close(true);
        RetryButton.Click += (s, e) => Start();
        CancelButton.Click += (s, e) =>
        {
            if (runner.IsRunning) runner.Stop();      // the kill ends the run; the window stays for the output
            else Close(false);
        };
        Closing += (s, e) => { if (runner.IsRunning) e.Cancel = true; };
        Opened += (s, e) => Start();
    }

    private void Start()
    {
        output.Inlines.Clear();
        ContinueButton.IsEnabled = false;
        RetryButton.IsEnabled = false;
        runner.Start();
    }

    private void Append(string text, bool iserror)
    {
        output.Inlines.Add(new Run(text) { Foreground = iserror ? Brushes.IndianRed : null });
        scroll.ScrollToEnd();
    }

    private void OnFinished()
    {
        ContinueButton.IsEnabled = true;
        RetryButton.IsEnabled = true;
        if (runner.HasErrors)
        {
            Append(Environment.NewLine + "There were errors during the execution of the external commands." + Environment.NewLine, true);
            Append("Exit code: " + runner.ExitCode + Environment.NewLine, true);
        }
        else if (autoclose) Close(true);
    }
}
