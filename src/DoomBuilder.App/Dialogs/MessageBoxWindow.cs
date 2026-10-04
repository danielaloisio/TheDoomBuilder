using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using DoomBuilder.App.Shell;
using DialogResult = System.Windows.Forms.DialogResult;

namespace DoomBuilder.App.Dialogs;

/// <summary>The classic message box: icon, text and a row of buttons. Returns the <see cref="DialogResult"/> of the button used.</summary>
public sealed class MessageBoxWindow : Window
{
    private readonly Dictionary<DialogResult, Button> buttons = new Dictionary<DialogResult, Button>();

    public DialogResult Result { get; private set; } = DialogResult.None;

    /// <summary>The text the box shows.</summary>
    public string Message { get; }

    public MessageBoxWindow(string text, string caption, MessageBoxButtons kind, MessageBoxIcon icon, MessageBoxDefaultButton defaultbutton)
    {
        Message = text;
        Title = string.IsNullOrEmpty(caption) ? "TheDoomBuilder" : caption;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 320;
        MaxWidth = 640;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var answers = AnswersFor(kind);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        for (int i = 0; i < answers.Length; i++)
        {
            DialogResult answer = answers[i];
            var button = new Button { Content = NameOf(answer), MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, Tag = answer };
            button.Click += (s, e) => Finish(answer);
            buttons[answer] = button;
            row.Children.Add(button);
        }

        // Escape means Cancel (or No, or OK when that is all there is), like a system message box
        DialogResult escape = Array.IndexOf(answers, DialogResult.Cancel) >= 0 ? DialogResult.Cancel
                            : Array.IndexOf(answers, DialogResult.No) >= 0 ? DialogResult.No : answers[0];
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Finish(escape); e.Handled = true; } };
        Closing += (s, e) => { if (Result == DialogResult.None) Result = escape; };

        int defaultindex = Math.Min((int)defaultbutton / 256, answers.Length - 1);
        Opened += (s, e) => buttons[answers[defaultindex]].Focus();

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(20) };
        var picture = ImageCache.Get(IconName(icon));
        if (picture != null)
            content.Children.Add(new Image { Source = picture, Width = 32, Height = 32, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top });
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 520 });
        body.Children.Add(row);
        Grid.SetColumn(body, 1);
        content.Children.Add(body);
        Content = content;
    }

    /// <summary>The button the user pressed (for tests that click through).</summary>
    public Button ButtonFor(DialogResult answer) => buttons[answer];

    private void Finish(DialogResult answer)
    {
        Result = answer;
        Close(answer);
    }

    private static DialogResult[] AnswersFor(MessageBoxButtons kind)
    {
        switch (kind)
        {
            case MessageBoxButtons.OKCancel: return new[] { DialogResult.OK, DialogResult.Cancel };
            case MessageBoxButtons.YesNo: return new[] { DialogResult.Yes, DialogResult.No };
            case MessageBoxButtons.YesNoCancel: return new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel };
            case MessageBoxButtons.RetryCancel: return new[] { DialogResult.Retry, DialogResult.Cancel };
            case MessageBoxButtons.AbortRetryIgnore: return new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore };
            default: return new[] { DialogResult.OK };
        }
    }

    private static string NameOf(DialogResult answer) => answer == DialogResult.OK ? "OK" : answer.ToString();

    // UDB's own icons for the message boxes
    private static string IconName(MessageBoxIcon icon)
    {
        switch (icon)
        {
            case MessageBoxIcon.Error: return "ErrorLarge";
            case MessageBoxIcon.Warning: return "WarningLarge";
            case MessageBoxIcon.Question: return "Question";
            default: return null;
        }
    }
}
