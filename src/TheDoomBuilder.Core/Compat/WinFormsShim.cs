// TEMPORARY SHIM. The UDB Core sources reference a handful of System.Windows.Forms types
// (key codes, mouse buttons, dialog enums). WinForms does not exist on Linux/macOS, so the
// types the Core really needs are declared here under the same namespace to keep the ported
// sources unchanged. Values match WinForms exactly (user key bindings are stored as ints).
// Retire this file by moving the call sites to neutral types as the UI layer gets ported.
using System;

namespace System.Windows.Forms
{
    [Flags]
    public enum Keys
    {
        KeyCode = 0xFFFF, Modifiers = unchecked((int)0xFFFF0000),
        None = 0, LButton = 1, RButton = 2, Cancel = 3, MButton = 4, XButton1 = 5, XButton2 = 6,
        Back = 8, Tab = 9, LineFeed = 10, Clear = 12, Return = 13, Enter = 13,
        ShiftKey = 16, ControlKey = 17, Menu = 18, Pause = 19, Capital = 20, CapsLock = 20,
        Escape = 27, Space = 32, Prior = 33, PageUp = 33, Next = 34, PageDown = 34,
        End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40,
        Select = 41, Print = 42, Execute = 43, Snapshot = 44, PrintScreen = 44,
        Insert = 45, Delete = 46, Help = 47,
        D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52, D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,
        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74, K = 75, L = 76,
        M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82, S = 83, T = 84, U = 85, V = 86, W = 87, X = 88,
        Y = 89, Z = 90, LWin = 91, RWin = 92, Apps = 93, Sleep = 95,
        NumPad0 = 96, NumPad1 = 97, NumPad2 = 98, NumPad3 = 99, NumPad4 = 100, NumPad5 = 101,
        NumPad6 = 102, NumPad7 = 103, NumPad8 = 104, NumPad9 = 105,
        Multiply = 106, Add = 107, Separator = 108, Subtract = 109, Decimal = 110, Divide = 111,
        F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117, F7 = 118, F8 = 119, F9 = 120,
        F10 = 121, F11 = 122, F12 = 123, F13 = 124, F14 = 125, F15 = 126, F16 = 127, F17 = 128,
        F18 = 129, F19 = 130, F20 = 131, F21 = 132, F22 = 133, F23 = 134, F24 = 135,
        NumLock = 144, Scroll = 145, LShiftKey = 160, RShiftKey = 161, LControlKey = 162,
        RControlKey = 163, LMenu = 164, RMenu = 165,
        Oem1 = 186, Oem2 = 191, Oem3 = 192, Oem4 = 219, Oem5 = 220, Oem6 = 221, Oem7 = 222,
        OemSemicolon = 186, Oemplus = 187, Oemcomma = 188, OemMinus = 189, OemPeriod = 190,
        OemQuestion = 191, Oemtilde = 192, OemOpenBrackets = 219, OemPipe = 220,
        OemCloseBrackets = 221, OemQuotes = 222, Oem8 = 223, OemBackslash = 226,
        Shift = 0x10000, Control = 0x20000, Alt = 0x40000,
    }

    [Flags]
    public enum MouseButtons
    {
        None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000, XButton1 = 0x800000, XButton2 = 0x1000000,
    }

    public class MouseEventArgs : EventArgs
    {
        public MouseButtons Button { get; }
        public int Clicks { get; }
        public int X { get; }
        public int Y { get; }
        public int Delta { get; }
        public System.Drawing.Point Location => new System.Drawing.Point(X, Y);
        public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
        { Button = button; Clicks = clicks; X = x; Y = y; Delta = delta; }
    }

    public class KeyEventArgs : EventArgs
    {
        public KeyEventArgs(Keys keyData) { KeyData = keyData; }
        public Keys KeyData { get; }
        public Keys KeyCode => KeyData & Keys.KeyCode;
        public Keys Modifiers => KeyData & Keys.Modifiers;
        public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;
        public bool Control => (KeyData & Keys.Control) == Keys.Control;
        public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;
        public virtual bool Handled { get; set; }
        public bool SuppressKeyPress { get; set; }
        public int KeyValue => (int)(KeyData & Keys.KeyCode);
    }

    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }

    public enum MessageBoxButtons { OK, OKCancel, AbortRetryIgnore, YesNoCancel, YesNo, RetryCancel }

    public enum MessageBoxIcon
    {
        None = 0, Error = 16, Hand = 16, Stop = 16, Question = 32, Exclamation = 48, Warning = 48,
        Asterisk = 64, Information = 64,
    }

    public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }

    public interface IWin32Window { IntPtr Handle { get; } }
}
