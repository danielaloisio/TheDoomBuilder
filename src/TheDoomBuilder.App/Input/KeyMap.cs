using System;
using System.Windows.Forms;
using Avalonia.Input;

namespace DoomBuilder.App.Input;

/// <summary>
/// Translates Avalonia keys to the WinForms <see cref="Keys"/> values the editor stores shortcuts as (they are the Windows
/// virtual-key codes plus the Shift/Control/Alt bits), so a user's settings and the shipped default bindings keep working.
/// </summary>
public static class KeyMap
{
    /// <summary>
    /// Key code only (no modifier bits). Returns <see cref="Keys.None"/> for keys the editor has no value for.
    /// </summary>
    public static Keys ToKeyCode(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return Keys.A + (key - Key.A);
        if (key >= Key.D0 && key <= Key.D9) return Keys.D0 + (key - Key.D0);
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return Keys.NumPad0 + (key - Key.NumPad0);
        if (key >= Key.F1 && key <= Key.F24) return Keys.F1 + (key - Key.F1);

        switch (key)
        {
            case Key.Back: return Keys.Back;
            case Key.Tab: return Keys.Tab;
            case Key.Return: return Keys.Return;
            case Key.Escape: return Keys.Escape;
            case Key.Space: return Keys.Space;
            case Key.PageUp: return Keys.PageUp;
            case Key.PageDown: return Keys.PageDown;
            case Key.End: return Keys.End;
            case Key.Home: return Keys.Home;
            case Key.Left: return Keys.Left;
            case Key.Up: return Keys.Up;
            case Key.Right: return Keys.Right;
            case Key.Down: return Keys.Down;
            case Key.Insert: return Keys.Insert;
            case Key.Delete: return Keys.Delete;
            case Key.Pause: return Keys.Pause;
            case Key.CapsLock: return Keys.CapsLock;
            case Key.PrintScreen: return Keys.PrintScreen;
            case Key.NumLock: return Keys.NumLock;
            case Key.Scroll: return Keys.Scroll;

            case Key.Multiply: return Keys.Multiply;
            case Key.Add: return Keys.Add;
            case Key.Subtract: return Keys.Subtract;
            case Key.Decimal: return Keys.Decimal;
            case Key.Divide: return Keys.Divide;

            case Key.OemSemicolon: return Keys.Oem1;
            case Key.OemPlus: return Keys.Oemplus;
            case Key.OemComma: return Keys.Oemcomma;
            case Key.OemMinus: return Keys.OemMinus;
            case Key.OemPeriod: return Keys.OemPeriod;
            case Key.OemQuestion: return Keys.Oem2;
            case Key.OemTilde: return Keys.Oem3;
            case Key.OemOpenBrackets: return Keys.Oem4;
            case Key.OemPipe: return Keys.Oem5;
            case Key.OemCloseBrackets: return Keys.Oem6;
            case Key.OemQuotes: return Keys.Oem7;
            case Key.OemBackslash: return Keys.OemBackslash;

            // WinForms reports the modifier keys by their generic code
            case Key.LeftShift: case Key.RightShift: return Keys.ShiftKey;
            case Key.LeftCtrl: case Key.RightCtrl: return Keys.ControlKey;
            case Key.LeftAlt: case Key.RightAlt: return Keys.Menu;
            case Key.LWin: return Keys.LWin;
            case Key.RWin: return Keys.RWin;

            default: return Keys.None;
        }
    }

    /// <summary>The Shift/Control/Alt bits for the pressed modifiers.</summary>
    /// <param name="macos">
    /// On macOS the Command key plays Ctrl's role (Cmd+Z, Cmd+C...), because the editor's default shortcuts are Ctrl-based.
    /// The physical Control key keeps working as Ctrl too.
    /// </param>
    public static Keys ToModifierBits(KeyModifiers modifiers, bool macos)
    {
        Keys result = Keys.None;
        if ((modifiers & KeyModifiers.Shift) != 0) result |= Keys.Shift;
        if ((modifiers & KeyModifiers.Control) != 0) result |= Keys.Control;
        if ((modifiers & KeyModifiers.Alt) != 0) result |= Keys.Alt;
        if (macos && (modifiers & KeyModifiers.Meta) != 0) result |= Keys.Control;
        return result;
    }

    public static Keys ToKeyData(Key key, KeyModifiers modifiers) => ToKeyData(key, modifiers, OperatingSystem.IsMacOS());

    /// <summary>Key code plus modifier bits; <see cref="Keys.None"/> when the key has no editor value.</summary>
    public static Keys ToKeyData(Key key, KeyModifiers modifiers, bool macos)
    {
        Keys code = ToKeyCode(key);
        if (code == Keys.None) return Keys.None;

        Keys bits = ToModifierBits(modifiers, macos);

        // Pressing a modifier itself: its own bit is part of the data (Shift down = ShiftKey | Shift). On macOS the Command keys
        // arrive as the Windows keys and count as Control, for the same reason they do in ToModifierBits.
        if (macos && (key == Key.LWin || key == Key.RWin)) { code = Keys.ControlKey; bits |= Keys.Control; }
        return code | bits;
    }
}
