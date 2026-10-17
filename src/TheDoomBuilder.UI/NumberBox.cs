using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Controls;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's ButtonsNumericTextbox: a text box for numbers with up/down buttons and the mouse wheel. It takes expressions and
/// relative values (see <see cref="NumericInput"/>); the step follows Ctrl (small) and Shift (big) when asked to.
/// </summary>
public sealed class NumberBox : UserControl
{
    private readonly TextBox box = new TextBox { MinWidth = 60 };
    private readonly NumericInput input = new NumericInput();

    /// <summary>The text changed (typed or by the buttons).</summary>
    public event EventHandler WhenTextChanged;
    public event EventHandler WhenButtonsClicked;
    public event EventHandler WhenEnterPressed;

    public bool AllowDecimal { get => input.AllowDecimal; set => input.AllowDecimal = value; }
    public bool AllowNegative { get => input.AllowNegative; set => input.AllowNegative = value; }
    public bool AllowRelative { get => input.AllowRelative; set { input.AllowRelative = value; Restyle(); } }
    public bool AllowExpressions { get => input.AllowExpressions; set { input.AllowExpressions = value; Restyle(); } }
    public int ButtonStep { get; set; } = 1;
    public float ButtonStepFloat { get; set; } = 1.0f;
    public float ButtonStepBig { get; set; } = 10.0f;
    public float ButtonStepSmall { get; set; } = 0.1f;
    public StepsList StepValues { get; set; }
    public bool ButtonStepsWrapAround { get; set; }
    public bool ButtonStepsUseModifierKeys { get; set; }

    public string Text
    {
        get => box.Text ?? "";
        set { box.Text = value ?? ""; input.Text = box.Text; Restyle(); }   // like UDB's textbox, setting the text raises WhenTextChanged
    }

    public NumberBox()
    {
        var up = new RepeatButton { Content = "▲", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
        var down = new RepeatButton { Content = "▼", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
        up.Click += (s, e) => Step(-1);     // the buttons count up: UDB's spinner reports negative for "up"
        down.Click += (s, e) => Step(1);
        var buttons = new StackPanel { Spacing = 0 };
        buttons.Children.Add(up);
        buttons.Children.Add(down);

        var row = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(box);
        Content = row;

        box.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            input.Text = box.Text ?? "";
            Restyle();
            WhenTextChanged?.Invoke(this, EventArgs.Empty);
        };
        box.KeyDown += (s, e) => { if (e.Key == Key.Enter) WhenEnterPressed?.Invoke(this, EventArgs.Empty); };
        box.GotFocus += (s, e) => box.SelectAll();
        box.PointerWheelChanged += (s, e) =>
        {
            if (!box.IsFocused) return;
            Step(-Math.Sign(e.Delta.Y), e.KeyModifiers);
            e.Handled = true;
        };
        UpdateTip();
    }

    /// <summary>The parsing behind this box (for models that apply the value to several elements).</summary>
    public NumericInput Input => input;

    public bool CheckIsRelative() => input.IsRelative;
    public int GetResult(int original) => input.GetResult(original);
    public int GetResult(int original, int step) => input.GetResult(original, step);
    public double GetResultFloat(double original) => input.GetResultFloat(original);
    public double GetResultFloat(double original, int step) => input.GetResultFloat(original, step);
    public void ResetIncrementStep() => input.ResetIncrementStep();
    public new void SelectAll() => box.SelectAll();
    public void UpdateButtonsTooltip() => UpdateTip();

    // direction: -1 = up (increase), +1 = down, as UDB's spinner reports it
    private void Step(int direction, KeyModifiers modifiers = KeyModifiers.None)
    {
        if (direction == 0 || input.IsRelative) return;
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control), shift = modifiers.HasFlag(KeyModifiers.Shift);

        if (StepValues != null && (!ButtonStepsUseModifierKeys || (!ctrl && !shift)))
        {
            int current = input.GetResult(0, 1);
            Text = (direction < 0 ? StepValues.GetNextHigherWrap(current, ButtonStepsWrapAround) : StepValues.GetNextLowerWrap(current, ButtonStepsWrapAround)).ToString();
        }
        else if (AllowDecimal)
        {
            double size = ButtonStepsUseModifierKeys ? (ctrl ? ButtonStepSmall : shift ? ButtonStepBig : ButtonStepFloat) : ButtonStepFloat;
            int decimals = General.Map != null ? General.Map.FormatInterface.VertexDecimals : 3;
            // direction -1 (up) adds, +1 (down) subtracts
            double value = Math.Round(input.GetResultFloat(0.0, 1) - direction * size, decimals);
            if (value < 0 && !AllowNegative) value = 0;
            Text = value.ToString(CultureInfo.CurrentCulture);
        }
        else
        {
            int size = ButtonStepsUseModifierKeys ? (ctrl ? (int)ButtonStepSmall : shift ? (int)ButtonStepBig : ButtonStep) : ButtonStep;
            int value = input.GetResult(0, 1) - direction * size;
            if (value < 0 && !AllowNegative) value = 0;
            Text = value.ToString(CultureInfo.CurrentCulture);
        }
        WhenTextChanged?.Invoke(this, EventArgs.Empty);
        WhenButtonsClicked?.Invoke(this, EventArgs.Empty);
    }

    // Invalid expressions show red, relative values blue, like UDB
    private void Restyle()
    {
        if (AllowExpressions && !input.IsValid) box.Foreground = Brushes.DarkRed;
        else if (AllowRelative && input.IsRelative) box.Foreground = Brushes.SteelBlue;
        else box.ClearValue(TextBox.ForegroundProperty);
    }

    private void UpdateTip()
    {
        string tip = "";
        if (AllowExpressions) tip += "You can use expressions. Example: (128+64)*2.5\n";
        if (AllowRelative) tip += "Use ++ or -- prefixes to change by given value.\nUse +++ or --- prefixes to incrementally change by given value.\nUse * or / prefixes to multiply or divide by given value.\n";
        if (ButtonStepsUseModifierKeys) tip += "Hold Ctrl to change value by " + ButtonStepSmall + ".\nHold Shift to change value by " + ButtonStepBig + ".";
        ToolTip.SetTip(box, tip.Length > 0 ? tip.TrimEnd() : null);
    }
}
