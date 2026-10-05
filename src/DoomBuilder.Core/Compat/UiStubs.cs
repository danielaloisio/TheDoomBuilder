// TEMPORARY UI STUBS. Placeholders for WinForms/UDB-UI types that the (otherwise UI-free) Core
// still references. They compile but do nothing; every one is a TODO to be replaced when the
// matching Avalonia piece exists (shell: Phase 4, dialogs/controls: Phase 5, rendering: Phase 2).
using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms
{
    public struct Padding { public int Left, Top, Right, Bottom; public Padding(int all) { Left = Top = Right = Bottom = all; } public Padding(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; } public int All => Left; public int Horizontal => Left + Right; public int Vertical => Top + Bottom; }
    public class Control : IWin32Window
    {
        public class ControlCollection : List<Control> { public void SetChildIndex(Control c, int index) { Remove(c); Insert(Math.Min(index, Count), c); } }
        public ControlCollection Controls { get; } = new ControlCollection();
        public int Width { get => ClientSize.Width; set => ClientSize = new Size(value, ClientSize.Height); }
        public int Height { get => ClientSize.Height; set => ClientSize = new Size(ClientSize.Width, value); }
        public int Left { get; set; }
        public int Top { get; set; }
        public Point Location { get; set; }
        public Padding Margin { get; set; }
        public bool Visible { get; set; } = true;
        public System.Drawing.Font Font { get; set; }
        public System.Drawing.Image BackgroundImage { get; set; }
        public ImageLayout BackgroundImageLayout { get; set; }
        public Rectangle RectangleToScreen(Rectangle r) => r;
        public string Text { get; set; }
        public bool Enabled { get; set; } = true;
        public IntPtr Handle => IntPtr.Zero;
        public Size ClientSize { get; set; }
        public Rectangle ClientRectangle => new Rectangle(Point.Empty, ClientSize);
        public Cursor Cursor { get; set; }
        public bool Focused => false;
        public bool InvokeRequired => false;
        public object Invoke(Delegate method, params object[] args) => method.DynamicInvoke(args);
        public void Focus() { }
        public Point PointToClient(Point p) => p;
        public Point PointToScreen(Point p) => p;
        /// <summary>The Avalonia control that really shows this one (typed object: the Core has no UI reference). Dockers use it.</summary>
        public object NativeControl { get; set; }
    }
    public class Panel : Control { }
    public class ComboBox : Control { public List<object> Items { get; } = new List<object>(); }
    public class ListView : Control { }
    public class DataGridView : Control { }
    public class Cursor { public static Point Position { get; set; } public static Cursor Current { get; set; } public static void Hide() { } public static void Show() { } }
    public static class Cursors { public static readonly Cursor Default = new Cursor(), AppStarting = new Cursor(), WaitCursor = new Cursor(), Cross = new Cursor(), SizeAll = new Cursor(), Hand = new Cursor(), IBeam = new Cursor(), No = new Cursor(), Arrow = new Cursor(), SizeNS = new Cursor(), SizeWE = new Cursor(), SizeNESW = new Cursor(), SizeNWSE = new Cursor(), HSplit = new Cursor(), VSplit = new Cursor(); }
}

namespace CodeImp.DoomBuilder.Controls
{
    /// <summary>Stub for the WinForms GL host control. The real one is an Avalonia MapViewport (Phase 2).</summary>
    public class RenderTargetControl : System.Windows.Forms.Control
    {
        public System.Drawing.Size Size => ClientSize;
        /// <summary>Raised when a mode wants a tooltip over the display (title, text, x, y in display pixels); the shell draws it.</summary>
        public event Action<string, string, int, int> ToolTipRequested;
        public event Action ToolTipHidden;
        public void ShowToolTip(string title, string text, int x, int y) { ToolTipRequested?.Invoke(title, text, x, y); }
        public void HideToolTip() { ToolTipHidden?.Invoke(); }
    }
}

namespace System.Windows.Forms
{
    /// <summary>Working replacement for the WinForms timer (fires Tick on a thread-pool thread).</summary>
    public class Timer : IDisposable
    {
        private readonly System.Timers.Timer timer = new System.Timers.Timer(100);
        public Timer() { timer.Elapsed += (s, e) => Tick?.Invoke(this, EventArgs.Empty); }
        public int Interval { get => (int)timer.Interval; set => timer.Interval = value; }
        public bool Enabled { get => timer.Enabled; set => timer.Enabled = value; }
        public event EventHandler Tick;
        public void Start() => timer.Start();
        public void Stop() => timer.Stop();
        public void Dispose() => timer.Dispose();
    }
}

// Script editor (Phase 8: AvaloniaEdit replaces ScintillaNET). Members listed are only those the Core calls.
namespace ScintillaNET
{
    public enum Lexer { Container = 0, Null = 1 }
    public class ScintillaLine { public int Position { get; set; } public int WrapCount { get; set; } public int Length { get; set; } }
    public class ScintillaLines { public ScintillaLine this[int index] => new ScintillaLine(); public int Count => 0; }
    public class Scintilla
    {
        public event System.Windows.Forms.KeyEventHandler KeyUp;
        public string Text { get; set; } = "";
        public int CurrentPosition { get; set; }
        public int CurrentLine => 0;
        public ScintillaLines Lines { get; } = new ScintillaLines();
        public bool CallTipActive => false;
        public bool AutoCActive => false;
        public void AutoCCancel() { }
        public void AutoCShow(int lenEntered, string list) { }
        public void SetKeywords(int set, string keywords) { }
        public int GetStyleAt(int position) => 0;
        public int GetCharAt(int position) => 0;
        public int WordStartPosition(int position, bool onlyWordCharacters) => position;
        public int WordEndPosition(int position, bool onlyWordCharacters) => position;
        public int LineFromPosition(int position) => 0;
        public int PointXFromPosition(int position) => 0;
        public int PointYFromPosition(int position) => 0;
        public int CharPositionFromPointClose(int x, int y) => -1;
        public string GetTextRange(int position, int length) => "";
        public void CallTipCancel() { }
        public void CallTipShow(int position, string definition) { }
        public void CallTipSetHlt(int start, int end) { }
    }
}

namespace System.Windows.Forms { public delegate void KeyEventHandler(object sender, KeyEventArgs e); }

namespace CodeImp.DoomBuilder.Controls
{
    public class ScriptEditorControl : System.Windows.Forms.Control
    {
        public enum ImageIndex { None = -1, ScriptConstant = 0, ScriptKeyword = 1, ScriptError = 2, ScriptProperty = 3, ScriptSnippet = 4 }
        public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding("iso-8859-1");
        public ScintillaNET.Scintilla Scintilla { get; } = new ScintillaNET.Scintilla();
        public bool CheckImplicitChanges() => false;
        public void ImplicitSave() { }
        public void ShowErrors(System.Collections.Generic.IList<CodeImp.DoomBuilder.Compilers.CompilerError> errors, bool clear) { }
        public void WriteOpenFilesToConfiguration() { }
        internal ScriptStyleType GetScriptStyle(int style) => ScriptStyleType.PlainText;
    }
    public class ScriptDocumentTab : System.Windows.Forms.Control { public string Filename { get; set; } public string Title { get; set; } }
}

namespace CodeImp.DoomBuilder.Windows
{
    public class ScriptEditorForm : System.Windows.Forms.Form
    {
        public CodeImp.DoomBuilder.Controls.ScriptEditorControl Editor { get; } = new CodeImp.DoomBuilder.Controls.ScriptEditorControl();
        public bool AskSaveAll() => true;
        public void DisplayError(CodeImp.DoomBuilder.ErrorItem error) { }
    }
}

namespace System.Windows.Forms { public class TabPage : Control { } }

namespace CodeImp.DoomBuilder.Controls
{
    public class ToastControl : System.Windows.Forms.Control
    {
        public ToastControl(CodeImp.DoomBuilder.ToastType type, string title, string message, long duration) { }
        public int Bottom => Top + Height;
        public bool IsAlive() => false;
        public void CheckDecay() { }
        public void Dispose() { }
    }
}

namespace CodeImp.DoomBuilder.Windows
{
    public class PreferencesForm : System.Windows.Forms.Form
    {
        private readonly System.Collections.Generic.List<System.Windows.Forms.TabPage> pages = new System.Collections.Generic.List<System.Windows.Forms.TabPage>();
        public System.Collections.Generic.IReadOnlyList<System.Windows.Forms.TabPage> Pages => pages;
        public void AddTabPage(System.Windows.Forms.TabPage tab) { pages.Add(tab); }
    }
}

namespace CodeImp.DoomBuilder.Windows
{
    /// <summary>What the plugins read from UDB's MainForm without having the form: the display scale (HiDPI). The shell sets it.</summary>
    public static class MainForm
    {
        public static System.Drawing.SizeF DPIScaler = new System.Drawing.SizeF(1f, 1f);
    }
}
