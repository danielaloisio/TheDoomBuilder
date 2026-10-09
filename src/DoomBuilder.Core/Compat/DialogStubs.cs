// TEMPORARY DIALOG STUBS. In UDB the Core opens WinForms dialogs directly (Types handlers, MapManager,
// General, edit modes). The dialogs do not exist yet in Avalonia, so these stand-ins keep the Core
// compiling and behave as "user cancelled". Each one becomes a call to an IDialogService method backed
// by an Avalonia window in Phase 4/5; the list below is that TODO list.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;

namespace System.Windows.Forms
{
    public enum FormWindowState { Normal, Minimized, Maximized }

    public class Form : Control, IDisposable
    {
        public FormWindowState WindowState { get; set; }
        public bool TopMost { get; set; }
        public bool IsDisposed => false;
        public void Close() { }
        public void Show() { }
        public void Activate() { }
        public static Form ActiveForm => null;
        public DialogResult DialogResult { get; set; }
        public virtual DialogResult ShowDialog() => DialogResult.Cancel;
        public virtual DialogResult ShowDialog(IWin32Window owner) => DialogResult.Cancel;
        public void Dispose() { }
    }

    public enum ImageLayout { None, Tile, Center, Stretch, Zoom }
    public enum HelpNavigator { TableOfContents, Index, Find, Topic }
    public static class Help { public static void ShowHelp(IWin32Window parent, string url) { } public static void ShowHelp(IWin32Window parent, string url, HelpNavigator nav, object p) { } }

    public static class Application
    {
        public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;
        public static string StartupPath => AppContext.BaseDirectory;
        public static string ProductName => "TheDoomBuilder";
        public static string ProductVersion => "0.0";
        public static event Action<object, System.Threading.ThreadExceptionEventArgs> ThreadException { add { } remove { } }
        public static void DoEvents() { }
        public static void EnableVisualStyles() { }
        public static void SetCompatibleTextRenderingDefault(bool v) { }
        public static void Exit() { }
        public static void Run(object form) { }
    }

    public static class MessageBox
    {
        public static DialogResult Show(string text) => Show(text, "", MessageBoxButtons.OK);
        public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) => Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
            => CodeImp.DoomBuilder.General.Dialogs.ShowMessage(CodeImp.DoomBuilder.Localization.Localizer.T(text), CodeImp.DoomBuilder.Localization.Localizer.T(caption), buttons, icon, def);
        public static DialogResult Show(IWin32Window owner, string text) => Show(text);
        public static DialogResult Show(IWin32Window owner, string text, string caption) => Show(text, caption);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) => Show(text, caption, buttons, icon);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton def)
            => Show(text, caption, buttons, icon, def);
    }

    public class FileDialog : Form
    {
        public string FileName { get; set; } = "";
        public string[] FileNames { get { return _filenames ?? new[] { FileName }; } set { _filenames = value; } }
        private string[] _filenames;
        public override DialogResult ShowDialog() => CodeImp.DoomBuilder.General.Dialogs.ShowFileDialog(this);
        public override DialogResult ShowDialog(IWin32Window owner) => CodeImp.DoomBuilder.General.Dialogs.ShowFileDialog(this);
        public string Filter { get; set; }
        public int FilterIndex { get; set; }
        public string InitialDirectory { get; set; }
        public string Title { get; set; }
        public bool AddExtension { get; set; }
        public bool CheckFileExists { get; set; }
        public bool CheckPathExists { get; set; }
        public bool RestoreDirectory { get; set; }
        public string DefaultExt { get; set; }
        public bool ValidateNames { get; set; }
        public bool ShowHelp { get; set; }
    }
    public class ColorDialog : Form
    {
        public bool AllowFullOpen { get; set; }
        public bool AnyColor { get; set; }
        public bool FullOpen { get; set; }
        public Color Color { get; set; }
    }
    public class OpenFileDialog : FileDialog { public bool Multiselect { get; set; } }
    public class SaveFileDialog : FileDialog { public bool OverwritePrompt { get; set; } }

    public static class DataFormats { public const string Text = "Text"; public const string UnicodeText = "UnicodeText"; }
    public interface IDataObject { object GetData(string format); bool GetDataPresent(string format); }
    public class DataObject : IDataObject
    {
        private readonly Dictionary<string, object> data = new Dictionary<string, object>();
        public void SetData(string format, object value) => data[format] = value;
        public object GetData(string format) => data.TryGetValue(format, out var v) ? v : null;
        public bool GetDataPresent(string format) => data.ContainsKey(format);
    }
    /// <summary>The system clipboard, for text. The shell provides it (Core has no UI types); without one the clipboard stays in this process.</summary>
    public interface IClipboardProvider
    {
        /// <summary>The text on the clipboard, or null when there is none.</summary>
        string GetText();
        void SetText(string text);
    }

    /// <summary>
    /// The clipboard code of the editor asks for. Text goes to the system clipboard when the shell provided one (so maps can be
    /// copied between instances and from other programs); if that fails, or there is none, the copy still works inside this process.
    /// </summary>
    public static class Clipboard
    {
        private static DataObject current = new DataObject();

        /// <summary>The system clipboard (set by the shell).</summary>
        public static IClipboardProvider Provider { get; set; }

        public static void SetDataObject(object d, bool copy) { current = d as DataObject ?? new DataObject(); }
        public static void SetDataObject(object d, bool copy, int retries, int delay) => SetDataObject(d, copy);

        public static void SetText(string text)
        {
            current = new DataObject();
            current.SetData(DataFormats.Text, text);
            try { Provider?.SetText(text); }
            catch(Exception e) { Console.Error.WriteLine("[Clipboard] Unable to use the system clipboard: " + e.Message); }
        }

        public static string GetText()
        {
            if(Provider != null)
            {
                try { return Provider.GetText() ?? string.Empty; }
                catch(Exception e) { Console.Error.WriteLine("[Clipboard] Unable to use the system clipboard: " + e.Message); }
            }
            return current.GetData(DataFormats.Text) as string ?? string.Empty;
        }

        public static bool ContainsData(string format) => format == DataFormats.Text ? ContainsText() : current.GetDataPresent(format);
        public static bool ContainsText() => GetText().Length > 0;
        public static object GetData(string format) => format == DataFormats.Text ? GetText() : current.GetData(format);
        public static IDataObject GetDataObject() => current;
    }

    public static class TextRenderer { public static Size MeasureText(string text, Font font) => new Size((int)Math.Ceiling(font.SizeInPixels * 0.6f * (text?.Length ?? 0)), font.Height); }

    public class KeysConverter
    {
        /// <summary>The name of a key, with its modifiers in front ("Ctrl+Shift+E"), as WinForms' converter writes it. Takes a Keys or its int value.</summary>
        public string ConvertToString(object value)
        {
            if(value == null) return string.Empty;
            int v = value is Keys keys ? (int)keys : Convert.ToInt32(value);

            string prefix = "";
            if((v & (int)Keys.Control) != 0) prefix += "Ctrl+";
            if((v & (int)Keys.Alt) != 0) prefix += "Alt+";
            if((v & (int)Keys.Shift) != 0) prefix += "Shift+";

            int code = v & 0xFFFF;
            if(code == 0) return prefix.TrimEnd('+');
            if(code >= (int)Keys.D0 && code <= (int)Keys.D9) return prefix + (char)('0' + (code - (int)Keys.D0));
            if(code == (int)Keys.Prior) return prefix + "PageUp";
            if(code == (int)Keys.Next) return prefix + "PageDown";
            if(code == (int)Keys.ControlKey) return prefix + "Ctrl";
            if(code == (int)Keys.ShiftKey) return prefix + "Shift";
            if(code == (int)Keys.Menu) return prefix + "Alt";
            if(code == (int)Keys.Return) return prefix + "Return";
            return prefix + ((Keys)code).ToString();
        }
        public object ConvertFromString(string text) => Enum.TryParse(text.Replace("+", ","), true, out Keys k) ? k : Keys.None;
    }
}

namespace CodeImp.DoomBuilder
{
    public enum DebugMessageType { LOG = 1, INFO = 2, WARNING = 4, ERROR = 8, SPECIAL = 16 }

    /// <summary>Stub of the debug console control: messages go to stderr.</summary>
    public static class DebugConsole
    {
        public static void Write(DebugMessageType type, string message) => Console.Error.Write(message);
        public static void WriteLine(string message) => Console.Error.WriteLine(message);
        public static void WriteLine(DebugMessageType type, string message) => Console.Error.WriteLine(message);
        public static void Clear() { }
    }

}

namespace CodeImp.DoomBuilder.Controls
{
    public static class ImageSelectorPanel { public static void ClearCachedPreviews() { } }
}

namespace CodeImp.DoomBuilder.Windows
{
    public class MapOptionsForm : Form
    {
        /// <summary>The options being edited; after OK, the result.</summary>
        public MapOptions Options { get; set; }
        /// <summary>True when creating a new map, false when editing the open one.</summary>
        public bool NewMap { get; }
        public MapOptionsForm(MapOptions options, bool newmap) { Options = options; NewMap = newmap; }
        public override DialogResult ShowDialog(IWin32Window owner) => CodeImp.DoomBuilder.General.Dialogs.ShowMapOptions(this);
    }
    public class OpenMapOptionsForm : Form
    {
        public string FileName { get; }
        public MapOptions Options { get; set; }
        public OpenMapOptionsForm(string filename) { FileName = filename; }
        public OpenMapOptionsForm(string filename, MapOptions options) { FileName = filename; Options = options; }
        public override DialogResult ShowDialog(IWin32Window owner) => CodeImp.DoomBuilder.General.Dialogs.ShowOpenMapOptions(this);
    }
    public class ChangeMapForm : Form
    {
        public string FileName { get; }
        public MapOptions Options { get; set; }
        public ChangeMapForm(string filename, MapOptions options) { FileName = filename; Options = options; }
        public override DialogResult ShowDialog(IWin32Window owner) => CodeImp.DoomBuilder.General.Dialogs.ShowChangeMap(this);
    }
    public class CenterOnCoordinatesForm : Form { public Vector2D Coordinates { get; } }
    public class PasteOptionsForm : Form
    {
        /// <summary>The options being edited (they start as the defaults); after OK, the result.</summary>
        public PasteOptions Options { get; set; } = CodeImp.DoomBuilder.General.Settings.PasteOptions.Copy();
        public override DialogResult ShowDialog(IWin32Window owner) => CodeImp.DoomBuilder.General.Dialogs.ShowPasteOptions(this);
    }
    public class GridSetupForm : Form { }
    public class ThingsFiltersForm : Form { }
    public class LinedefColorPresetsForm : Form { }
    public class ExceptionDialog : Form
    {
        public ExceptionDialog(Exception e) { }
        public ExceptionDialog(System.Threading.ThreadExceptionEventArgs e) { }
        public ExceptionDialog(UnhandledExceptionEventArgs e) { }
        public void Setup() { }
    }
    public class RunExternalCommandForm : Form
    {
        /// <summary>The command being run (the window shows its output).</summary>
        public ExternalCommandRunner Runner { get; }
        public ExternalCommandSettings Settings { get; }
        public RunExternalCommandForm(ProcessStartInfo info, ExternalCommandSettings settings)
        {
            Settings = settings;
            Runner = new ExternalCommandRunner(info, settings);
        }
        public override DialogResult ShowDialog() { DialogResult = CodeImp.DoomBuilder.General.Dialogs.ShowRunExternalCommand(this); Runner.Dispose(); return DialogResult; }
        public override DialogResult ShowDialog(IWin32Window owner) => ShowDialog();
    }
    public class ThingBrowserForm : Form
    {
        public int SelectedType { get; }
        public ThingBrowserForm(int type) { }
        public static int BrowseThing(IWin32Window parent, int value) => CodeImp.DoomBuilder.General.Dialogs.BrowseThing(value);
    }
    public static class AngleForm { public static int ShowDialog(IWin32Window parent, int value) => value; }
    public static class TextureBrowserForm { public static string Browse(IWin32Window parent, string value, bool flats) => CodeImp.DoomBuilder.General.Dialogs.BrowseImage(value, flats); }
    public static class TextEditForm { public static string ShowDialog(IWin32Window parent, string value) => value; }
    public static class EffectBrowserForm { public static int BrowseEffect(IWin32Window parent, int value) => value; }
    public static class ActionBrowserForm { public static int BrowseAction(IWin32Window parent, int value) => value; }
    public static class BitFlagsForm { public static int ShowDialog(IWin32Window parent, object list, int value) => value; }
    public static class BitFlagsAndOptionsForm { public static int ShowDialog(IWin32Window parent, object list, object flags, int value) => value; }
}

namespace CodeImp.DoomBuilder.Controls
{
    internal enum ScriptStyleType { PlainText = 0, Keyword = 1, Constant = 2, Comment = 3, Literal = 4, LineNumber = 5, String = 6, Include = 7, Property = 8 }
    public class ScriptLumpDocumentTab : ScriptDocumentTab { }
}
