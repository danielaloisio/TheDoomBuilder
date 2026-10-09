using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Compilers;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Data.Scripting;
using CodeImp.DoomBuilder.GZBuilder.Data;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;

using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// The script editor window (UDB's ScriptEditorForm + ScriptEditorPanel): one tab per script lump of the map (SCRIPTS, DIALOGUE...),
/// AvaloniaEdit as the text control, a compile button and the list of compiler errors. The Core talks to it through <see cref="IScriptEditorHost"/>.
/// </summary>
public sealed class ScriptEditorWindow : Window, IScriptEditorHost
{
    internal enum DocKind { Lump, File, Resource }

    /// <summary>One script being edited: a lump of the map (saved with the map), a file on disk, or a script resource inside a wad/pk3/folder of the map's resources.</summary>
    internal sealed class DocTab
    {
        public DocKind Kind;
        public string LumpName;          // Lump: as stored in the map's temporary file
        public bool IsMapHeader;
        public string FilePath = "";     // File: empty until saved
        public string Untitled = "";     // File: the name shown until saved
        public ScriptResource Resource;  // Resource
        public string Hash;              // Resource: of the data when loaded, to notice other programs changing it
        public bool ReadOnly;
        public ScriptConfiguration Config;
        public TextEditor Editor;
        public ScriptSyntaxColorizer Colorizer;
        public ScriptHighlightRenderer Highlight;
        public FoldingManager Folding;
        public TabItem Item;
        public string SavedText = "";
        public bool Changed => Editor.Text != SavedText;
        /// <summary>Lumps are saved into the map by themselves; files and resources only when asked.</summary>
        public bool ExplicitSave => Kind != DocKind.Lump;
        public string SettingsKey => Kind switch
        {
            DocKind.Lump => IsMapHeader ? MapManager.CONFIG_MAP_HEADER : LumpName,
            DocKind.File => FilePath,
            _ => Resource.FilePathName,
        };
        public string Title => Kind switch
        {
            DocKind.Lump => IsMapHeader ? General.Map.Options.CurrentName : LumpName.ToUpperInvariant(),
            DocKind.File => FilePath.Length == 0 ? Untitled : Path.GetFileName(FilePath),
            _ => Resource.ToString(),
        };
        /// <summary>The name compiler errors carry for this script.</summary>
        public bool OwnsError(CompilerError e) => Kind switch
        {
            DocKind.Lump => string.Equals(e.filename, "?" + LumpName, StringComparison.OrdinalIgnoreCase),
            DocKind.File => FilePath.Length > 0 && string.Equals(e.filename, FilePath, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(e.filename, Resource.Filename, StringComparison.OrdinalIgnoreCase),
        };
    }

    private readonly TabControl tabs = new TabControl();
    private readonly ListBox errorlist = new ListBox { MaxHeight = 150 };
    private readonly TextBlock status = new TextBlock { Margin = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly List<DocTab> docs = new List<DocTab>();
    private readonly List<CompilerError> currenterrors = new List<CompilerError>();
    private bool closed, closingconfirmed;
    private readonly TextBox findbox = new TextBox { PlaceholderText = Loc.T("Find"), MinWidth = 200 };
    private readonly TextBox replacebox = new TextBox { PlaceholderText = Loc.T("Replace with"), MinWidth = 200 };
    private readonly CheckBox matchcase = new CheckBox { Content = Loc.T("Match case") };
    private readonly CheckBox wholeword = new CheckBox { Content = Loc.T("Whole word") };
    private readonly StackPanel findbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(6, 0, 6, 6), IsVisible = false };
    private CompletionWindow completion;
    private OverloadInsightWindow calltip;
    private readonly ComboBox navigator = new ComboBox { MinWidth = 220, PlaceholderText = Loc.T("Scripts and functions"), IsEnabled = false };
    private readonly Avalonia.Threading.DispatcherTimer navigatortimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
    private bool updatingnavigator;
    private List<CompilerError> navigatorerrors = new List<CompilerError>();

    /// <summary>The open editor, if any (the Core owns the lifetime through ScriptEditorForm; tests look it up here).</summary>
    public static ScriptEditorWindow Instance { get; private set; }

    public bool IsClosed => closed;
    public bool IsShown => IsVisible;
    bool IScriptEditorHost.TopMost { set { Topmost = value; } }

    public ScriptEditorWindow()
    {
        Title = Loc.T("Script Editor");
        Width = 900;
        Height = 650;
        MinWidth = 400;
        MinHeight = 300;

        var newfile = new Button { Content = Loc.T("New") };
        var open = new Button { Content = Loc.T("Open...") };
        var resources = new Button { Content = Loc.T("Resources...") };
        var save = new Button { Content = Loc.T("Save") };
        var saveas = new Button { Content = Loc.T("Save as...") };
        var closetab = new Button { Content = Loc.T("Close tab") };
        var compile = new Button { Content = Loc.T("Compile") };
        var wrap = new CheckBox { Content = Loc.T("Wrap long lines") };
        var whitespace = new CheckBox { Content = Loc.T("Show whitespace") };
        var newmenu = new MenuFlyout();
        foreach(ScriptConfiguration config in ScriptTypes())
        {
            var item = new MenuItem { Header = config.Description };
            item.Click += (s, e) => NewFile(config);
            newmenu.Items.Add(item);
        }
        newfile.Flyout = newmenu;
        open.Click += (s, e) => OpenFileDialog();
        resources.Click += (s, e) => BrowseResources();
        save.Click += (s, e) => SaveCurrent();
        saveas.Click += (s, e) => SaveAs(Current);
        closetab.Click += (s, e) => CloseTab(Current);
        compile.Click += (s, e) => CompileCurrent();
        wrap.IsCheckedChanged += (s, e) => { foreach(DocTab t in docs) t.Editor.WordWrap = wrap.IsChecked == true; };
        whitespace.IsCheckedChanged += (s, e) => { foreach(DocTab t in docs) { t.Editor.Options.ShowSpaces = t.Editor.Options.ShowTabs = whitespace.IsChecked == true; } };
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        foreach(Control c in new Control[] { newfile, open, resources, save, saveas, closetab, compile, wrap, whitespace })
        {
            c.Margin = new Thickness(0, 0, 8, 4);
            toolbar.Children.Add(c);
        }
        navigator.Margin = new Thickness(0, 0, 8, 4);
        toolbar.Children.Add(navigator);
        navigator.SelectionChanged += (s, e) => OnNavigatorSelected();
        navigatortimer.Tick += (s, e) => { navigatortimer.Stop(); UpdateNavigator(Current); if(Current != null) UpdateFolding(Current); };

        var next = new Button { Content = Loc.T("Next") };
        var previous = new Button { Content = Loc.T("Previous") };
        var replace = new Button { Content = Loc.T("Replace") };
        var replaceall = new Button { Content = Loc.T("Replace all") };
        var closefind = new Button { Content = Loc.T("x") };
        next.Click += (s, e) => FindNext(false);
        previous.Click += (s, e) => FindNext(true);
        replace.Click += (s, e) => ReplaceCurrent();
        replaceall.Click += (s, e) => ReplaceAll();
        closefind.Click += (s, e) => { findbar.IsVisible = false; Current?.Editor.Focus(); };
        findbox.KeyDown += (s, e) => { if(e.Key == Key.Enter) { FindNext(e.KeyModifiers == KeyModifiers.Shift); e.Handled = true; } else if(e.Key == Key.Escape) { findbar.IsVisible = false; Current?.Editor.Focus(); e.Handled = true; } };
        foreach(Control c in new Control[] { findbox, replacebox, matchcase, wholeword, next, previous, replace, replaceall, closefind }) findbar.Children.Add(c);

        errorlist.IsVisible = false;
        errorlist.DoubleTapped += (s, e) => GoToSelectedError();
        errorlist.KeyDown += (s, e) => { if(e.Key == Key.Enter) GoToSelectedError(); };

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(findbar, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        DockPanel.SetDock(errorlist, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(findbar);
        root.Children.Add(status);
        root.Children.Add(errorlist);
        root.Children.Add(tabs);
        Content = root;

        tabs.SelectionChanged += (s, e) => { UpdateStatus(); UpdateNavigator(Current); };
        KeyDown += OnWindowKeyDown;
        Closing += OnClosing;
        Closed += (s, e) => { closed = true; if(Instance == this) Instance = null; };

        Instance = this;
                LoadLumps();
    }

    // ---------------------------------------------------------------- loading

    private void LoadLumps()
    {
        if(General.Map == null) return;
        foreach(MapLumpInfo info in General.Map.Config.MapLumps.Values)
        {
            ScriptConfiguration config = null;
            if(info.ScriptBuild)
            {
                config = General.GetScriptConfiguration(ScriptType.ACS);
                if(config == null)
                {
                    General.ErrorLogger.Add(ErrorType.Warning, "Unable to find script configuration for \"" + ScriptType.ACS + "\" script type. Using plain text configuration.");
                    config = new ScriptConfiguration();
                }
            }
            else if(info.Script != null)
            {
                config = info.Script;
            }
            if(config == null) continue;
            AddLump(info.Name, config);
        }

        // Files and resources that were open when the editor was closed
        foreach(ScriptDocumentSettings saved in General.Map.Options.ScriptDocumentSettings.Values.ToList())
        {
            if(saved.TabType == ScriptDocumentTabType.FILE && File.Exists(saved.Filename)) OpenFile(saved.Filename);
            else if(saved.TabType == ScriptDocumentTabType.RESOURCE)
            {
                ScriptResource resource = FindResource(saved.ScriptType, saved.Filename);
                if(resource != null) OpenResource(resource);
            }
        }

        // Back to where it was left: the active tab, the caret and the scroll position of each script
        DocTab active = null;
        foreach(DocTab t in docs)
        {
            if(!General.Map.Options.ScriptDocumentSettings.TryGetValue(t.SettingsKey, out ScriptDocumentSettings saved)) continue;
            Dispatcher.UIThread.Post(() => RestoreView(t, saved), DispatcherPriority.Loaded);
            if(saved.IsActiveTab) active = t;
        }
        // Otherwise the "Scripts" tab is what people want 99% of the time
        active ??= docs.FirstOrDefault(t => t.Kind == DocKind.Lump && t.Title == "SCRIPTS");
        if(active != null) tabs.SelectedItem = active.Item;
        UpdateStatus();
        UpdateNavigator(Current);
    }

    private static ScriptResource FindResource(ScriptType type, string filepathname)
    {
        if(General.Map?.Data == null || !General.Map.Data.ScriptResources.TryGetValue(type, out var set)) return null;
        return set.FirstOrDefault(r => string.Equals(r.FilePathName, filepathname, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The script types to offer for a new file (and the plain text one).</summary>
    private static List<ScriptConfiguration> ScriptTypes()
    {
        var list = new List<ScriptConfiguration>(General.ScriptConfigs.Values) { new ScriptConfiguration() };
        list.Sort();
        return list;
    }

    private DocTab CreateTab(DocKind kind, ScriptConfiguration config)
    {
        var tab = new DocTab { Kind = kind, Config = config };
        tab.Editor = new TextEditor
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        tab.Colorizer = new ScriptSyntaxColorizer(config);
        tab.Editor.TextArea.TextView.LineTransformers.Add(tab.Colorizer);
        tab.Highlight = new ScriptHighlightRenderer(config, tab.Editor.TextArea);
        tab.Editor.TextArea.TextView.BackgroundRenderers.Add(tab.Highlight);
        tab.Editor.TextArea.SelectionChanged += (s, e) => UpdateHighlight(tab);
        tab.Editor.TextChanged += (s, e) => { tab.Colorizer.Invalidate(); tab.Editor.TextArea.TextView.Redraw(); UpdateTitle(tab); UpdateStatus(); navigatortimer.Stop(); navigatortimer.Start(); };
        tab.Editor.TextArea.Caret.PositionChanged += (s, e) => { UpdateStatus(); UpdateHighlight(tab); };
        tab.Editor.TextArea.TextEntered += (s, e) => OnTextEntered(tab, e.Text);
        ApplySettings(tab);
        return tab;
    }

    private void Register(DocTab tab)
    {
        tab.SavedText = tab.Editor.Text;
        tab.Item = new TabItem { Header = tab.Title, Content = tab.Editor, Tag = tab };
        if(tab.Kind == DocKind.File && tab.FilePath.Length > 0) ToolTip.SetTip(tab.Item, tab.FilePath);
        else if(tab.Kind == DocKind.Resource) ToolTip.SetTip(tab.Item, tab.Resource.FilePathName);
        tabs.Items.Add(tab.Item);
        docs.Add(tab);
    }

    private void AddLump(string name, ScriptConfiguration config)
    {
        bool isheader = name == MapManager.CONFIG_MAP_HEADER;
        DocTab tab = CreateTab(DocKind.Lump, config);
        tab.LumpName = isheader ? MapManager.TEMP_MAP_HEADER : name;
        tab.IsMapHeader = isheader;
        MemoryStream stream = General.Map.GetLumpData(tab.LumpName);
        if(stream != null) tab.Editor.Text = ScriptEditorControl.Encoding.GetString(stream.ToArray());
        Register(tab);
    }

    // ---------------------------------------------------------------- files

    /// <summary>The script type of a file by its extension (plain text when none matches).</summary>
    private static ScriptConfiguration ConfigForFile(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.');
        foreach(ScriptConfiguration config in General.ScriptConfigs.Values)
            if(config.Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase))) return config;
        return new ScriptConfiguration();
    }

    internal bool OpenFile(string path)
    {
        DocTab existing = docs.FirstOrDefault(t => t.Kind == DocKind.File && string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if(existing != null) { tabs.SelectedItem = existing.Item; return true; }

        string text;
        try { text = File.ReadAllText(path); }
        catch(Exception e)
        {
            General.ErrorLogger.Add(ErrorType.Error, "Cannot open file \"" + path + "\" for reading. Make sure the path exists and that the file is not in use by another application.");
            General.WriteLogLine(e.GetType().Name + ": " + e.Message);
            General.ShowErrorMessage("Unable to open file \"" + path + "\" for reading. Make sure the path exists and that the file is not in use by another application.", System.Windows.Forms.MessageBoxButtons.OK);
            return false;
        }

        DocTab tab = CreateTab(DocKind.File, ConfigForFile(path));
        tab.FilePath = path;
        tab.Editor.Text = text;
        Register(tab);
        tabs.SelectedItem = tab.Item;
        UpdateNavigator(tab);
        ShowErrors(navigatorerrors, true);
        return true;
    }

    internal DocTab NewFile(ScriptConfiguration config)
    {
        DocTab tab = CreateTab(DocKind.File, config);
        tab.Untitled = "Untitled" + (config.Extensions.Length > 0 ? "." + config.Extensions[0] : "");
        Register(tab);
        tabs.SelectedItem = tab.Item;
        tab.Editor.Focus();
        return tab;
    }

    private void OpenFileDialog()
    {
        var dialog = new System.Windows.Forms.OpenFileDialog { Title = Loc.T("Open Script File"), Filter = "All files|*", CheckFileExists = true };
        if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dialog.FileName)) OpenFile(dialog.FileName);
    }

    // ---------------------------------------------------------------- resources

    /// <summary>Opens (or selects) a script resource found in the map's resources.</summary>
    internal DocTab OpenResource(ScriptResource resource)
    {
        DocTab existing = docs.FirstOrDefault(t => t.Kind == DocKind.Resource && t.Resource.LumpIndex == resource.LumpIndex && t.Resource.FilePathName == resource.FilePathName);
        if(existing != null) { tabs.SelectedItem = existing.Item; return existing; }

        ScriptConfiguration config = General.GetScriptConfiguration(resource.ScriptType) ?? new ScriptConfiguration();
        DocTab tab = CreateTab(DocKind.Resource, config);
        tab.Resource = resource;
        tab.ReadOnly = resource.IsReadOnly;
        tab.Editor.IsReadOnly = resource.IsReadOnly;

        MemoryStream stream = resource.Resource?.LoadFile(resource.Filename, resource.LumpIndex);
        if(stream != null)
        {
            tab.Hash = MD5Hash.Get(stream);
            tab.Editor.Text = ScriptEditorControl.Encoding.GetString(stream.ToArray());
        }
        else
        {
            General.ErrorLogger.Add(ErrorType.Warning, "Failed to load " + resource.ScriptType + " resource \"" + resource.Filename + "\".");
        }

        Register(tab);
        tabs.SelectedItem = tab.Item;
        UpdateNavigator(tab);
        ShowErrors(navigatorerrors, true);
        return tab;
    }

    private void BrowseResources()
    {
        if(General.Map?.Data == null) return;
        var all = new List<(string Text, ScriptResource Resource)>();
        foreach(var group in General.Map.Data.ScriptResources.OrderBy(g => g.Key.ToString()))
            foreach(ScriptResource r in group.Value.OrderBy(r => r.FilePathName, StringComparer.OrdinalIgnoreCase))
                all.Add(((r.IsReadOnly ? "[read only] " : "") + group.Key + ": " + r + "   (" + r.FilePathName + ")", r));

        var filter = new TextBox { PlaceholderText = Loc.T("Filter") };
        var list = new ListBox { ItemsSource = all.Select(a => a.Text).ToList() };
        var open = new Button { Content = Loc.T("Open"), HorizontalAlignment = HorizontalAlignment.Right };
        var dialog = new Window { Title = Loc.T("Script Resources"), Width = 640, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(filter, Dock.Top);
        DockPanel.SetDock(open, Dock.Bottom);
        root.Children.Add(filter);
        root.Children.Add(open);
        root.Children.Add(list);
        dialog.Content = root;

        void Open()
        {
            if(list.SelectedItem is string text && all.FirstOrDefault(a => a.Text == text).Resource is ScriptResource r) { OpenResource(r); dialog.Close(); }
        }
        filter.PropertyChanged += (s, e) =>
        {
            if(e.Property != TextBox.TextProperty) return;
            string f = filter.Text ?? "";
            list.ItemsSource = all.Where(a => a.Text.Contains(f, StringComparison.OrdinalIgnoreCase)).Select(a => a.Text).ToList();
        };
        list.DoubleTapped += (s, e) => Open();
        open.Click += (s, e) => Open();
        dialog.Show(this);
    }

    /// <summary>The script editor preferences (font, line numbers, tabs, indentation) on one editor.</summary>
    private void ApplySettings(DocTab tab)
    {
        var settings = General.Settings;
        string font = string.IsNullOrWhiteSpace(settings.ScriptFontName) ? "" : settings.ScriptFontName + ", ";
        tab.Editor.FontFamily = new FontFamily(font + "Cascadia Mono, Consolas, DejaVu Sans Mono, Menlo, monospace");
        tab.Editor.FontSize = settings.ScriptFontSize > 0 ? settings.ScriptFontSize : 12;
        tab.Editor.FontWeight = settings.ScriptFontBold ? FontWeight.Bold : FontWeight.Normal;
        tab.Editor.ShowLineNumbers = settings.ScriptShowLineNumbers;
        tab.Editor.Options.IndentationSize = settings.ScriptTabWidth > 0 ? settings.ScriptTabWidth : 4;
        tab.Editor.Options.ConvertTabsToSpaces = !settings.ScriptUseTabs;
        tab.Editor.TextArea.IndentationStrategy = settings.ScriptAutoIndent ? new ScriptIndentationStrategy(tab.Config, tab.Editor.TextArea) : null;

        // The colors of the program (Preferences > Script editor)
        ColorCollection colors = General.Colors;
        IBrush Brush(PixelColor c) => new SolidColorBrush(Color.FromArgb(255, c.r, c.g, c.b));
        tab.Editor.Background = Brush(colors.ScriptBackground);
        tab.Editor.Foreground = Brush(colors.PlainText);
        tab.Editor.LineNumbersForeground = Brush(colors.LineNumbers);
        tab.Editor.TextArea.SelectionBrush = Brush(colors.ScriptSelectionBackColor);
        tab.Editor.TextArea.SelectionForeground = Brush(colors.ScriptSelectionForeColor);
        tab.Editor.TextArea.TextView.NonPrintableCharacterBrush = Brush(colors.ScriptWhitespace);
        tab.Colorizer.RefreshColors();
        tab.Colorizer.Invalidate();
        tab.Editor.TextArea.TextView.Redraw();

        // Folding of code blocks, for the C-like script types
        bool fold = settings.ScriptShowFolding && ((int)tab.Config.Lexer == 3 || (int)tab.Config.Lexer == 35);
        if(fold && tab.Folding == null) { tab.Folding = FoldingManager.Install(tab.Editor.TextArea); UpdateFolding(tab); }
        else if(!fold && tab.Folding != null) { FoldingManager.Uninstall(tab.Folding); tab.Folding = null; }
    }

    private void UpdateFolding(DocTab tab)
    {
        if(tab.Folding == null) return;
        tab.Folding.UpdateFoldings(ScriptFolding.Find(tab.Editor.Text, tab.Config), -1);
    }

    private static void UpdateHighlight(DocTab tab)
    {
        tab.Highlight.Update();
        tab.Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    // ---------------------------------------------------------------- state

    private DocTab Current => (tabs.SelectedItem as TabItem)?.Tag as DocTab;

    private void UpdateTitle(DocTab tab) { if(tab.Item == null) return; tab.Item.Header = tab.Title + (tab.Changed ? "*" : ""); }

    private void UpdateStatus()
    {
        DocTab tab = Current;
        if(tab == null) { status.Text = ""; return; }
        var caret = tab.Editor.TextArea.Caret;
        status.Text = $"{tab.Config.Description}    Ln {caret.Line}, Col {caret.Column}";
    }

    // ---------------------------------------------------------------- saving

    // Lumps are saved into the map by themselves (implicit); files and resources only when asked (explicit)
    public bool CheckImplicitChanges() => docs.Any(t => t.Kind == DocKind.Lump && t.Changed);

    public void ImplicitSave()
    {
        foreach(DocTab tab in docs)
        {
            if(tab.Kind != DocKind.Lump || !tab.Changed) continue;
            TrimTrailingWhitespace(tab);
            General.Map.SetLumpData(tab.LumpName, new MemoryStream(ScriptEditorControl.Encoding.GetBytes(tab.Editor.Text)));
            tab.SavedText = tab.Editor.Text;
            UpdateTitle(tab);
        }
    }

    private static void TrimTrailingWhitespace(DocTab tab)
    {
        var doc = tab.Editor.Document;
        for(int number = doc.LineCount; number >= 1; number--)
        {
            DocumentLine line = doc.GetLineByNumber(number);
            string text = doc.GetText(line.Offset, line.Length);
            int keep = text.TrimEnd(' ', '\t').Length;
            if(keep < text.Length) doc.Remove(line.Offset + keep, text.Length - keep);
        }
    }

    /// <summary>Saves one script. Returns false when it could not be saved or the user cancelled.</summary>
    private bool Save(DocTab tab)
    {
        if(tab == null) return false;
        switch(tab.Kind)
        {
            case DocKind.Lump:
                ImplicitSave();
                return true;

            case DocKind.File:
                if(tab.FilePath.Length == 0) return SaveAs(tab);
                return WriteFile(tab, tab.FilePath);

            default:
                return SaveResource(tab);
        }
    }

    private bool WriteFile(DocTab tab, string path)
    {
        TrimTrailingWhitespace(tab);
        try { File.WriteAllText(path, tab.Editor.Text); }
        catch(Exception e)
        {
            General.ErrorLogger.Add(ErrorType.Error, "Cannot open file \"" + path + "\" for writing. Make sure the path exists and that the file is not in use by another application.");
            General.WriteLogLine(e.GetType().Name + ": " + e.Message);
            General.ShowErrorMessage("Unable to open file \"" + path + "\" for writing. Make sure the path exists and that the file is not in use by another application.", System.Windows.Forms.MessageBoxButtons.OK);
            return false;
        }
        tab.FilePath = path;
        tab.SavedText = tab.Editor.Text;
        UpdateTitle(tab);
        ToolTip.SetTip(tab.Item, path);
        return true;
    }

    /// <summary>Saves a file script under a name asked from the user (files only; lumps and resources keep their place).</summary>
    private bool SaveAs(DocTab tab)
    {
        if(tab == null || tab.Kind != DocKind.File) return false;
        var dialog = new System.Windows.Forms.SaveFileDialog { Title = Loc.T("Save Script As"), Filter = "All files|*", FileName = tab.FilePath.Length > 0 ? tab.FilePath : tab.Untitled, OverwritePrompt = true };
        if(dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrEmpty(dialog.FileName)) return false;
        return WriteFile(tab, dialog.FileName);
    }

    private bool SaveResource(DocTab tab)
    {
        if(tab.ReadOnly || !tab.Changed) return false;
        DataReader reader = tab.Resource.Resource;
        if(reader == null) { SaveLostResourceAsFile(tab); return false; }

        TrimTrailingWhitespace(tab);

        // Somebody else changed the lump since it was loaded?
        if(reader.FileExists(tab.Resource.Filename, tab.Resource.LumpIndex))
        {
            using(MemoryStream current = reader.LoadFile(tab.Resource.Filename, tab.Resource.LumpIndex))
            {
                if(current != null && MD5Hash.Get(current) != tab.Hash
                   && System.Windows.Forms.MessageBox.Show("Target lump was modified by another application. Do you still want to replace it?", "Warning", System.Windows.Forms.MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.Cancel)
                    return false;
            }
        }

        using(var stream = new MemoryStream(ScriptEditorControl.Encoding.GetBytes(tab.Editor.Text)))
        {
            if(!reader.SaveFile(stream, tab.Resource.Filename, tab.Resource.LumpIndex)) return false;
            stream.Position = 0;
            tab.Hash = MD5Hash.Get(stream);
        }
        tab.SavedText = tab.Editor.Text;
        UpdateTitle(tab);
        return true;
    }

    // The resource is gone (resources were reloaded without it): what was typed becomes an unsaved file
    private void SaveLostResourceAsFile(DocTab tab)
    {
        General.ShowErrorMessage("The resource of \"" + tab.Title + "\" is no longer available. Save the text to a file instead.", System.Windows.Forms.MessageBoxButtons.OK);
        tab.Kind = DocKind.File;
        tab.Untitled = Path.GetFileName(tab.Resource.Filename);
        tab.Editor.IsReadOnly = false;
        SaveAs(tab);
    }

    internal bool SaveCurrent() { return Save(Current); }
    internal bool SaveCurrentAs() { return SaveAs(Current); }
    internal bool CloseCurrentTab() { return CloseTab(Current); }
    internal IReadOnlyList<string> TabTitles() { return docs.Select(t => (string)t.Item.Header).ToList(); }

    /// <summary>Closes a tab of a file or resource, asking to save its changes first. Map lumps stay.</summary>
    internal bool CloseTab(DocTab tab)
    {
        if(tab == null || tab.Kind == DocKind.Lump) return false;
        if(!AskSave(tab)) return false;
        int index = tabs.Items.IndexOf(tab.Item);
        docs.Remove(tab);
        tabs.Items.Remove(tab.Item);
        if(tabs.SelectedItem == null && tabs.ItemCount > 0) tabs.SelectedIndex = Math.Max(0, index - 1);
        return true;
    }

    private bool AskSave(DocTab tab)
    {
        if(!tab.ExplicitSave || tab.ReadOnly || !tab.Changed) return true;
        switch(System.Windows.Forms.MessageBox.Show(Loc.T("Do you want to save changes to {0}?", tab.Title), "Close File", System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question))
        {
            case System.Windows.Forms.DialogResult.Yes: return Save(tab);
            case System.Windows.Forms.DialogResult.Cancel: return false;
            default: return true;
        }
    }

    public bool AskSaveAll()
    {
        ImplicitSave();
        foreach(DocTab tab in docs.ToList())
            if(!AskSave(tab)) return false;
        return true;
    }

    private static void RestoreView(DocTab tab, ScriptDocumentSettings saved)
    {
        int length = tab.Editor.Document.TextLength;
        tab.Editor.TextArea.Caret.Offset = Math.Max(0, Math.Min(saved.CaretPosition, length));
        if(saved.FirstVisibleLine > 0) tab.Editor.ScrollToLine(Math.Min(saved.FirstVisibleLine + 1, tab.Editor.Document.LineCount));
    }

    /// <summary>Remembers the open scripts and their view in the map's options (they are saved with the map).</summary>
    public void WriteOpenFilesToConfiguration()
    {
        if(General.Map == null) return;
        General.Map.Options.ScriptDocumentSettings.Clear();
        foreach(DocTab t in docs)
        {
            if(t.Kind == DocKind.File && t.FilePath.Length == 0) continue;   // never saved: nothing to come back to
            General.Map.Options.ScriptDocumentSettings[t.SettingsKey] = new ScriptDocumentSettings
            {
                FoldLevels = new Dictionary<int, HashSet<int>>(),
                CaretPosition = t.Editor.CaretOffset,
                FirstVisibleLine = Math.Max(0, (int)(t.Editor.TextArea.TextView.VerticalOffset / Math.Max(1, t.Editor.TextArea.TextView.DefaultLineHeight))),
                Filename = t.SettingsKey,
                TabType = t.Kind switch { DocKind.Lump => ScriptDocumentTabType.LUMP, DocKind.File => ScriptDocumentTabType.FILE, _ => ScriptDocumentTabType.RESOURCE },
                ScriptType = t.Kind == DocKind.Resource ? t.Resource.ScriptType : ScriptType.UNKNOWN,
                ResourceLocation = t.Kind == DocKind.Resource ? t.Resource.Resource?.Location.location : null,
                IsActiveTab = ReferenceEquals(Current, t),
            };
        }
    }

    // ---------------------------------------------------------------- compile and errors

    private void CompileCurrent()
    {
        DocTab tab = Current;
        if(tab == null) return;
        if(tab.Config.Compiler == null && tab.Kind != DocKind.Lump)
        {
            General.ShowErrorMessage("There is no compiler for " + tab.Config.Description + " scripts.", System.Windows.Forms.MessageBoxButtons.OK);
            return;
        }

        var errors = new List<CompilerError>();
        bool compiled;
        switch(tab.Kind)
        {
            case DocKind.Lump:
                ImplicitSave();
                string configname = tab.IsMapHeader ? MapManager.CONFIG_MAP_HEADER : tab.LumpName;
                if(!General.Map.Config.MapLumps.ContainsKey(configname))
                {
                    General.ShowErrorMessage("Unable to compile lump \"" + tab.LumpName + "\". This lump is not defined in the current game configuration.", System.Windows.Forms.MessageBoxButtons.OK);
                    return;
                }
                compiled = General.Map.TemporaryMapFile.CompileLump(configname, tab.Config, errors);
                break;

            case DocKind.File:
                if(!Save(tab)) return;    // the compiler reads the file
                compiled = DirectoryReader.CompileScriptLump(tab.FilePath, tab.Config, errors);
                break;

            default:
                if(tab.Changed && !Save(tab)) return;
                DataReader reader = tab.Resource.Resource;
                compiled = reader != null && reader.CompileLump(tab.Resource.Filename, tab.Resource.LumpIndex, tab.Config, errors);
                break;
        }

        if(compiled)
        {
            UpdateNavigator(tab);
            errors.AddRange(navigatorerrors);
        }
        ShowErrors(errors, false);
    }

    public void ShowErrors(IList<CompilerError> errors, bool clear)
    {
        if(clear) currenterrors.Clear();
        currenterrors.AddRange(errors);
        errorlist.ItemsSource = currenterrors.Select(e => e.linenumber != CompilerError.NO_LINE_NUMBER
            ? $"{e.filename.TrimStart('?')} line {e.linenumber + 1}: {e.description}"
            : $"{e.filename.TrimStart('?')}: {e.description}").ToList();
        errorlist.IsVisible = currenterrors.Count > 0;
    }

    private void GoToSelectedError()
    {
        int index = errorlist.SelectedIndex;
        if(index < 0 || index >= currenterrors.Count) return;
        CompilerError error = currenterrors[index];
        DocTab tab = docs.FirstOrDefault(t => t.OwnsError(error)) ?? Current;
        if(tab == null) return;
        tabs.SelectedItem = tab.Item;
        if(error.linenumber != CompilerError.NO_LINE_NUMBER) MoveToLine(tab, error.linenumber + 1);
    }

    private static void MoveToLine(DocTab tab, int line)
    {
        line = Math.Max(1, Math.Min(line, tab.Editor.Document.LineCount));
        var docline = tab.Editor.Document.GetLineByNumber(line);
        tab.Editor.Select(docline.Offset, docline.Length);
        tab.Editor.ScrollToLine(line);
        tab.Editor.Focus();
    }

    public void DisplayError(ErrorItem error)
    {
        if(!(error is TextResourceErrorItem item) || General.Map?.Data == null) return;

        // The resource that holds the script (embedded wads of a pk3 live in a temporary folder)
        DataReader reader = null;
        if(item.ResourceLocation.StartsWith(General.Map.TempPath))
        {
            foreach(DataReader container in General.Map.Data.Containers)
                if(container is PK3Reader pk3)
                    foreach(WADReader wad in pk3.Wads)
                        if(wad.Location.location == item.ResourceLocation) reader = wad;
        }
        else
        {
            reader = General.Map.Data.Containers.FirstOrDefault(c => c.Location.location == item.ResourceLocation);
        }
        if(reader == null || !reader.FileExists(item.LumpName, item.LumpIndex)) return;

        using(MemoryStream stream = reader.LoadFile(item.LumpName, item.LumpIndex))
        {
            var data = new TextResourceData(reader, stream, item.LumpName, item.LumpIndex, false);
            DocTab tab = OpenResource(new ScriptResource(data, item.ScriptType));
            if(tab != null && item.LineNumber != CompilerError.NO_LINE_NUMBER) MoveToLine(tab, item.LineNumber + 1);
        }
    }

    // ---------------------------------------------------------------- find / replace / go to line

    private void ShowFind(bool withreplace)
    {
        DocTab tab = Current;
        if(tab == null) return;
        string selected = tab.Editor.SelectedText;
        if(!string.IsNullOrEmpty(selected) && !selected.Contains('\n')) findbox.Text = selected;
        replacebox.IsVisible = withreplace;
        findbar.IsVisible = true;
        findbox.Focus();
        findbox.SelectAll();
    }

    private bool IsWholeWord(string text, int start, int length)
    {
        string extra = Current?.Config.ExtraWordCharacters;
        bool Word(char c) => char.IsLetterOrDigit(c) || c == '_' || (extra != null && extra.IndexOf(c) >= 0);
        if(start > 0 && Word(text[start - 1])) return false;
        if(start + length < text.Length && Word(text[start + length])) return false;
        return true;
    }

    /// <summary>Finds the next (or previous) match from the selection and selects it. Wraps around.</summary>
    private bool FindNext(bool backwards)
    {
        DocTab tab = Current;
        string what = findbox.Text;
        if(tab == null || string.IsNullOrEmpty(what)) return false;
        string text = tab.Editor.Text;
        var comparison = matchcase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        bool whole = wholeword.IsChecked == true;
        int selstart = tab.Editor.SelectionStart, selend = selstart + tab.Editor.SelectionLength;

        int Search(int from, bool back, int limit)
        {
            int pos = from;
            while(true)
            {
                if(back)
                {
                    if(pos < 0) return -1;
                    pos = text.LastIndexOf(what, Math.Min(pos + what.Length - 1, text.Length - 1), comparison);
                    if(pos < 0 || pos < limit) return -1;
                }
                else
                {
                    if(pos > text.Length) return -1;
                    pos = text.IndexOf(what, pos, comparison);
                    if(pos < 0 || pos > limit) return -1;
                }
                if(!whole || IsWholeWord(text, pos, what.Length)) return pos;
                pos += back ? -1 : 1;
            }
        }

        int found = backwards ? Search(selstart - 1, true, 0) : Search(selend, false, text.Length);
        if(found < 0) found = backwards ? Search(text.Length - 1, true, 0) : Search(0, false, text.Length); // wrap around
        if(found < 0) { status.Text = "Not found: " + what; return false; }
        tab.Editor.Select(found, what.Length);
        tab.Editor.TextArea.Caret.BringCaretToView();
        return true;
    }

    private void ReplaceCurrent()
    {
        DocTab tab = Current;
        if(tab == null || string.IsNullOrEmpty(findbox.Text)) return;
        var comparison = matchcase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if(string.Equals(tab.Editor.SelectedText, findbox.Text, comparison))
            tab.Editor.Document.Replace(tab.Editor.SelectionStart, tab.Editor.SelectionLength, replacebox.Text ?? "");
        FindNext(false);
    }

    private int ReplaceAll()
    {
        DocTab tab = Current;
        string what = findbox.Text;
        if(tab == null || string.IsNullOrEmpty(what)) return 0;
        var comparison = matchcase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        string text = tab.Editor.Text, with = replacebox.Text ?? "";
        var result = new System.Text.StringBuilder();
        int count = 0, pos = 0;
        while(true)
        {
            int found = text.IndexOf(what, pos, comparison);
            if(found < 0) break;
            if(wholeword.IsChecked == true && !IsWholeWord(text, found, what.Length)) { result.Append(text, pos, found - pos + 1); pos = found + 1; continue; }
            result.Append(text, pos, found - pos).Append(with);
            pos = found + what.Length;
            count++;
        }
        if(count == 0) { status.Text = "Not found: " + what; return 0; }
        result.Append(text, pos, text.Length - pos);
        tab.Editor.Document.BeginUpdate();
        tab.Editor.Document.Replace(0, text.Length, result.ToString());
        tab.Editor.Document.EndUpdate();
        status.Text = count + " replaced";
        return count;
    }

    private async void GoToLineDialog()
    {
        DocTab tab = Current;
        if(tab == null) return;
        var box = new TextBox { Text = tab.Editor.TextArea.Caret.Line.ToString(), MinWidth = 120 };
        var ok = new Button { Content = Loc.T("Go") };
        var dialog = new Window { Title = Loc.T("Go to line"), SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Line number (1 - " + tab.Editor.Document.LineCount + "):" });
        panel.Children.Add(box);
        panel.Children.Add(ok);
        dialog.Content = panel;
        ok.Click += (s, e) => dialog.Close(true);
        box.KeyDown += (s, e) => { if(e.Key == Key.Enter) dialog.Close(true); };
        dialog.Opened += (s, e) => { box.Focus(); box.SelectAll(); };
        if(await dialog.ShowDialog<bool?>(this) == true && int.TryParse(box.Text, out int line)) MoveToLine(tab, line);
    }

    // ---------------------------------------------------------------- auto-completion

    private sealed class ScriptCompletionData : ICompletionData
    {
        private readonly Action<TextArea, ISegment> complete;
        public ScriptCompletionData(string text, string description, Action<TextArea, ISegment> complete) { Text = text; Description = description; this.complete = complete; }
        public Avalonia.Media.IImage Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment segment, EventArgs insertionRequestEventArgs) { complete(textArea, segment); }
    }

    private ScriptCompletionData CompletionFor(DocTab tab, string word)
    {
        return new ScriptCompletionData(word, tab.Config.GetFunctionDefinition(word), (area, segment) =>
        {
            string[] snippet = tab.Config.GetSnippet(word);
            if(snippet != null) { InsertSnippet(tab, snippet, segment.Offset, segment.Length); return; }

            // A definition with an entry marker (or a "$" comment template) replaces the word with its expanded form
            string definition = tab.Config.GetFunctionDefinition(word);
            int entry = string.IsNullOrEmpty(definition) ? -1 : definition.IndexOf(EntryMarker, StringComparison.OrdinalIgnoreCase);
            if(!string.IsNullOrEmpty(definition) && (word.StartsWith("$") || entry != -1))
            {
                if(entry != -1) definition = definition.Remove(entry, EntryMarker.Length);
                area.Document.Replace(segment.Offset, segment.Length, definition);
                if(entry != -1) area.Caret.Offset = segment.Offset + entry;
                return;
            }
            area.Document.Replace(segment.Offset, segment.Length, word);
        });
    }

    private const string EntryMarker = "[EP]", LineBreakMarker = "[LB]";

    /// <summary>Puts a snippet in place of the given text: indented like the current line, [LB] a line break in Allman style, [EP] where the caret ends up.</summary>
    private void InsertSnippet(DocTab tab, string[] lines, int offset, int length)
    {
        var doc = tab.Editor.Document;
        DocumentLine line = doc.GetLineByOffset(offset);
        string linetext = doc.GetText(line.Offset, line.Length);
        string indent = new string(linetext.TakeWhile(c => c == ' ' || c == '\t').ToArray());
        string spaces = new string(' ', Math.Max(1, General.Settings.ScriptTabWidth));

        var processed = new List<string>();
        foreach(string l in lines)
        {
            if(l.Contains(LineBreakMarker, StringComparison.Ordinal))
            {
                if(General.Settings.ScriptAllmanStyle) processed.AddRange(l.Split(new[] { LineBreakMarker }, StringSplitOptions.RemoveEmptyEntries));
                else processed.Add(l.Replace(LineBreakMarker, " "));
            }
            else processed.Add(l);
        }

        int caret = -1, running = 0;
        for(int i = 0; i < processed.Count; i++)
        {
            if(!General.Settings.ScriptUseTabs) processed[i] = processed[i].Replace("\t", spaces);
            int marker = processed[i].IndexOf(EntryMarker, StringComparison.OrdinalIgnoreCase);
            if(marker != -1 && caret == -1)
            {
                processed[i] = processed[i].Remove(marker, EntryMarker.Length);
                caret = running + marker;
            }
            running += processed[i].Length + 1 + indent.Length; // the line break and the indentation after it
        }

        doc.Replace(offset, length, string.Join("\n" + indent, processed));
        tab.Editor.TextArea.Caret.Offset = caret != -1 ? offset + caret : offset + string.Join("\n" + indent, processed).Length;
    }

    /// <summary>Inserts the named snippet of the current script type at the caret (what picking it in the completion list does).</summary>
    internal bool InsertSnippetAtCaret(string name)
    {
        DocTab tab = Current;
        string[] lines = tab?.Config.GetSnippet(name);
        if(lines == null) return false;
        InsertSnippet(tab, lines, tab.Editor.CaretOffset, 0);
        return true;
    }

    // ---------------------------------------------------------------- function navigator

    /// <summary>The names in the function navigator (scripts and functions of the current tab), brought up to date first.</summary>
    internal IReadOnlyList<string> NavigatorNames()
    {
        UpdateNavigator(Current);
        return (navigator.ItemsSource as IEnumerable<ScriptItem> ?? Enumerable.Empty<ScriptItem>()).Select(i => i.Name).ToList();
    }

    internal void Navigate(string name)
    {
        navigator.SelectedItem = (navigator.ItemsSource as IEnumerable<ScriptItem>)?.FirstOrDefault(i => i.Name == name);
    }

    private void UpdateNavigator(DocTab tab)
    {
        if(tab == null || General.Map == null) return;
        string text = tab.Editor.Text;
        updatingnavigator = true;
        try
        {
            string previous = (navigator.SelectedItem as ScriptItem)?.Name;
            navigatorerrors = new List<CompilerError>();
            if(text.Length == 0) { navigator.ItemsSource = null; navigator.IsEnabled = false; return; }

            var combo = new System.Windows.Forms.ComboBox();
            ScriptHandler handler = General.Types.GetScriptHandler(tab.Config.ScriptType);
            ScriptDocumentTab fake = tab.Kind == DocKind.Lump ? new ScriptLumpDocumentTab { Title = tab.Title, Filename = tab.LumpName }
                : new ScriptDocumentTab { Title = tab.Title, Filename = tab.Kind == DocKind.File ? tab.FilePath : tab.Resource.Filename };
            navigatorerrors = handler.UpdateFunctionBarItems(fake, new MemoryStream(ScriptEditorControl.Encoding.GetBytes(text)), combo);

            var items = combo.Items.OfType<ScriptItem>().ToList();
            navigator.ItemsSource = items;
            navigator.IsEnabled = items.Count > 0;
            navigator.SelectedItem = items.FirstOrDefault(i => i.Name == previous) ?? items.FirstOrDefault();
        }
        finally { updatingnavigator = false; }
    }

    private void OnNavigatorSelected()
    {
        DocTab tab = Current;
        if(updatingnavigator || tab == null || !(navigator.SelectedItem is ScriptItem item)) return;
        int offset = Math.Max(0, Math.Min(item.CursorPosition, tab.Editor.Document.TextLength));
        tab.Editor.TextArea.Caret.Offset = offset;
        tab.Editor.ScrollToLine(tab.Editor.Document.GetLineByOffset(offset).LineNumber);
        tab.Editor.Focus();
    }

    // ---------------------------------------------------------------- function tips

    /// <summary>After "(" of a known function: its definition (arguments) next to the caret, until ")" or ";" is typed.</summary>
    private void ShowCallTip(DocTab tab)
    {
        var area = tab.Editor.TextArea;
        var doc = area.Document;
        int end = area.Caret.Offset - 1; // the "("
        int start = end;
        while(start > 0 && IsWordChar(doc.GetCharAt(start - 1), tab.Config)) start--;
        if(start == end) return;
        string definition = tab.Config.GetFunctionDefinition(doc.GetText(start, end - start));
        if(string.IsNullOrEmpty(definition)) return;

        CloseCallTip();
        calltip = new OverloadInsightWindow(area) { Provider = new SingleTip(definition.Replace(EntryMarker, "")) };
        calltip.Closed += (s, e) => calltip = null;
        calltip.Show();
    }

    private void CloseCallTip() { calltip?.Close(); calltip = null; }

    private sealed class SingleTip : IOverloadProvider
    {
        private readonly string text;
        public SingleTip(string text) { this.text = text; }
        public int SelectedIndex { get => 0; set { } }
        public int Count => 1;
        public string CurrentIndexText => null;
        public object CurrentHeader => text;
        public object CurrentContent => null;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged { add { } remove { } }
    }

    private static bool IsWordChar(char c, ScriptConfiguration config) => char.IsLetterOrDigit(c) || c == '_' || (config.ExtraWordCharacters != null && config.ExtraWordCharacters.IndexOf(c) >= 0);

    private void OnTextEntered(DocTab tab, string text)
    {
        if(string.IsNullOrEmpty(text)) return;
        AutoCloseBracket(tab, text[0]);
        if(text[0] == '(') ShowCallTip(tab);
        else if(text[0] == ')' || text[0] == ';') CloseCallTip();
        else if(IsWordChar(text[0], tab.Config) && General.Settings.ScriptAutoShowAutocompletion) ShowCompletion(tab, false);
    }

    /// <summary>With "auto close brackets": typing an opening block/function/array character adds the closing one after the caret.</summary>
    private void AutoCloseBracket(DocTab tab, char typed)
    {
        if(!General.Settings.ScriptAutoCloseBrackets || tab.Editor.IsReadOnly) return;
        var area = tab.Editor.TextArea;
        var doc = area.Document;
        int caret = area.Caret.Offset;
        foreach((string open, string close) in new[] { (tab.Config.CodeBlockOpen, tab.Config.CodeBlockClose), (tab.Config.FunctionOpen, tab.Config.FunctionClose), (tab.Config.ArrayOpen, tab.Config.ArrayClose) })
        {
            if(string.IsNullOrEmpty(open) || string.IsNullOrEmpty(close) || typed != open[0]) continue;
            bool atend = caret >= doc.TextLength;
            if(atend || doc.GetCharAt(caret) != close[0])
            {
                doc.Insert(caret, close);
                area.Caret.Offset = caret;
            }
            return;
        }
    }

    /// <summary>F1: the help page of the word under the caret, when the script type has a keyword help site.</summary>
    internal bool LaunchKeywordHelp()
    {
        DocTab tab = Current;
        string site = tab?.Config.KeywordHelp;
        if(string.IsNullOrEmpty(site)) return false;
        string word = WordAtCaret(tab);
        if(!string.IsNullOrEmpty(word) && word.Length > 1)
        {
            General.OpenWebsite(site.Replace("%K", tab.Config.GetKeywordCase(word)));
            return true;
        }
        return true;
    }

    private static string WordAtCaret(DocTab tab)
    {
        var doc = tab.Editor.Document;
        int caret = tab.Editor.CaretOffset, start = caret, end = caret;
        while(start > 0 && IsWordChar(doc.GetCharAt(start - 1), tab.Config)) start--;
        while(end < doc.TextLength && IsWordChar(doc.GetCharAt(end), tab.Config)) end++;
        return doc.GetText(start, end - start);
    }

    /// <summary>The word being typed; UDB offers every keyword/constant/property/snippet that contains it.</summary>
    private void ShowCompletion(DocTab tab, bool force)
    {
        if(tab == null) return;
        var area = tab.Editor.TextArea;
        int caret = area.Caret.Offset;
        var doc = area.Document;
        int start = caret;
        while(start > 0 && IsWordChar(doc.GetCharAt(start - 1), tab.Config)) start--;
        string typed = doc.GetText(start, caret - start);
        if(typed.Length == 0 && !force) return;

        // Not inside comments or strings: look at the line text before the caret
        string before = doc.GetText(doc.GetLineByOffset(caret).Offset, caret - doc.GetLineByOffset(caret).Offset);
        if(before.Contains("//") || before.Count(c => c == '"') % 2 == 1) return;

        var comparison = tab.Config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var words = tab.Config.Keywords.Concat(tab.Config.Constants).Concat(tab.Config.Properties).Concat(tab.Config.Snippets)
            .Where(w => typed.Length == 0 || w.IndexOf(typed, comparison) >= 0).Distinct().OrderBy(w => w, StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        if(words.Count == 0 || (words.Count == 1 && string.Equals(words[0], typed, comparison))) { completion?.Close(); return; }

        if(completion == null)
        {
            completion = new CompletionWindow(area) { CloseWhenCaretAtBeginning = true };
            completion.Closed += (s, e) => completion = null;
            completion.StartOffset = start;
            foreach(string w in words) completion.CompletionList.CompletionData.Add(CompletionFor(tab, w));
            completion.Show();
        }
        else
        {
            completion.StartOffset = start;
            var data = completion.CompletionList.CompletionData;
            data.Clear();
            foreach(string w in words) data.Add(CompletionFor(tab, w));
            completion.CompletionList.SelectItem(typed);
        }
    }

    // ---------------------------------------------------------------- window

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if(e.Key == Key.F10) { e.Handled = true; return; }
        if(e.Key == Key.F1) { if(!LaunchKeywordHelp()) General.ShowHelp("w_scripteditor.html"); e.Handled = true; return; }
        if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S) { SaveCurrent(); e.Handled = true; }
        else if(e.Key == Key.F9) { CompileCurrent(); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F) { ShowFind(false); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.H) { ShowFind(true); e.Handled = true; }
        else if(e.Key == Key.F3) { FindNext(e.KeyModifiers == KeyModifiers.Shift); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.W) { CloseTab(Current); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O) { OpenFileDialog(); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N) { NewFile(ConfigForFile("x.txt")); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.G) { GoToLineDialog(); e.Handled = true; }
        else if(e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Space) { ShowCompletion(Current, true); e.Handled = true; }
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if(closingconfirmed) return;
        General.Map?.ApplyScriptChanged();
        if(!AskSaveAll()) { e.Cancel = true; return; }
        WriteOpenFilesToConfiguration();
        closingconfirmed = true;
        General.Map?.CloseScriptEditor(true);
    }

    public void ShowWindow()
    {
        if(IsVisible) return;
        if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            Show(desktop.MainWindow);
        else
            Show();
    }

    public void Restore() { if(WindowState == Avalonia.Controls.WindowState.Minimized) WindowState = Avalonia.Controls.WindowState.Normal; }

    public void ActivateWindow()
    {
        foreach(DocTab t in docs) ApplySettings(t);   // the preferences may have changed since
        Topmost = General.Settings.ScriptOnTop;
        Restore();
        Activate();
        Current?.Editor.Focus();
    }

    public void CloseWindow()
    {
        ImplicitSave();            // closed by the program (map closed, another map opened): the scripts go into the map first
        closingconfirmed = true;
        Close();
    }
}
