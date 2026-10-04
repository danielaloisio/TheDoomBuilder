using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Control = Avalonia.Controls.Control;
using Button = Avalonia.Controls.Button;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;
using StackPanel = Avalonia.Controls.StackPanel;
using TextBlock = Avalonia.Controls.TextBlock;using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using DialogResult = System.Windows.Forms.DialogResult;

namespace DoomBuilder.App.Dialogs;

/// <summary>The real dialogs of the application, shown as Avalonia windows over the main window.</summary>
internal sealed class AvaloniaDialogs : NoDialogs
{
    private readonly Func<Window> owner;

    public AvaloniaDialogs(Func<Window> owner)
    {
        this.owner = owner;
    }

    private Window Owner { get { return owner(); } }

    public override DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultbutton)
    {
        Console.Error.WriteLine("[MessageBox] " + caption + ": " + text);
        if (Owner == null) return base.ShowMessage(text, caption, buttons, icon, defaultbutton);

        var box = new MessageBoxWindow(text, caption, buttons, icon, defaultbutton);
        DialogPump.Run(() => box.ShowDialog<DialogResult>(Owner));
        return box.Result;
    }

    public override DialogResult ShowFileDialog(FileDialog dialog)
    {
        var storage = Owner?.StorageProvider;
        if (storage == null) return DialogResult.Cancel;

        var types = FileFilter.ParseCaseInsensitive(dialog.Filter);
        IStorageFolder start = null;
        if (!string.IsNullOrEmpty(dialog.InitialDirectory) && Directory.Exists(dialog.InitialDirectory))
            start = DialogPump.Run(() => storage.TryGetFolderFromPathAsync(dialog.InitialDirectory));

        if (dialog is SaveFileDialog)
        {
            var file = DialogPump.Run(() => storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = dialog.Title,
                FileTypeChoices = types,
                SuggestedStartLocation = start,
                SuggestedFileName = dialog.FileName,
                DefaultExtension = dialog.AddExtension ? DefaultExtensionOf(types, dialog.DefaultExt) : null,
                ShowOverwritePrompt = ((SaveFileDialog)dialog).OverwritePrompt,
            }));
            if (file == null) return DialogResult.Cancel;
            dialog.FileName = file.TryGetLocalPath() ?? file.Path.LocalPath;
            return DialogResult.OK;
        }

        bool multiple = (dialog as OpenFileDialog)?.Multiselect ?? false;
        IReadOnlyList<IStorageFile> files = DialogPump.Run(() => storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = dialog.Title,
            FileTypeFilter = types,
            SuggestedStartLocation = start,
            AllowMultiple = multiple,
        }));
        if (files == null || files.Count == 0) return DialogResult.Cancel;

        string[] paths = files.Select(f => f.TryGetLocalPath() ?? f.Path.LocalPath).ToArray();
        dialog.FileName = paths[0];
        dialog.FileNames = paths;
        return DialogResult.OK;
    }

    // "wad" from the first filter pattern "*.wad" when the dialog gave none
    private static string DefaultExtensionOf(List<FilePickerFileType> types, string explicitext)
    {
        if (!string.IsNullOrEmpty(explicitext)) return explicitext.TrimStart('.');
        string pattern = types.FirstOrDefault()?.Patterns?.FirstOrDefault();
        return pattern != null && pattern.StartsWith("*.") ? pattern.Substring(2) : null;
    }

    // ---- map options

    private static List<string> None() => new List<string>();

    public override DialogResult ShowOpenMapOptions(OpenMapOptionsForm form)
    {
        if (Owner == null) return DialogResult.Cancel;

        var model = new OpenMapOptionsModel(form.FileName, form.Options);
        try
        {
            if (model.LoadError != null)
            {
                ShowMessage(model.LoadError, "TheDoomBuilder", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return DialogResult.Cancel;
            }
            if (model.NothingToOpen)
            {
                ShowMessage("Unable to find maps using any game configuration.\nDoes this wad contain any maps at all?..", "TheDoomBuilder", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                return DialogResult.Cancel;
            }

            MapOptionsWindow window = null;
            var maps = new ListBox { Height = 140, ItemsSource = model.Maps.ToList(), SelectedItem = model.SelectedMap };
            maps.SelectionChanged += (s, e) => { if (maps.SelectedItem is string name) model.SelectMap(name); window?.LoadFromChoices(); };
            maps.DoubleTapped += (s, e) => window?.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            window = new MapOptionsWindow("Open Map from " + Path.GetFileName(form.FileName), model, Labeled("Maps", maps), form.FileName,
                validate: () => { string error = model.Validate(out string confirmation); return (error, confirmation == null ? None() : new List<string> { confirmation }); },
                apply: () => form.Options = model.BuildOptions(),
                onconfigchanged: () => { maps.ItemsSource = model.Maps.ToList(); maps.SelectedItem = model.SelectedMap; });

            return ShowWindow(window);
        }
        finally { model.Dispose(); }
    }

    public override DialogResult ShowChangeMap(ChangeMapForm form)
    {
        if (Owner == null) return DialogResult.Cancel;

        var model = new OpenMapOptionsModel(form.FileName, form.Options);
        try
        {
            if (model.LoadError != null)
            {
                ShowMessage(model.LoadError, "TheDoomBuilder", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return DialogResult.Cancel;
            }

            var maps = new ListBox { Height = 160, ItemsSource = model.Maps.ToList(), SelectedItem = form.Options?.CurrentName ?? model.SelectedMap };
            maps.SelectionChanged += (s, e) => { if (maps.SelectedItem is string name) model.SelectMap(name); };
            if (maps.SelectedItem is string initial) model.SelectMap(initial);

            var window = new MapOptionsWindow("Change Map", model, Labeled("Maps in " + Path.GetFileName(form.FileName), maps), form.FileName,
                validate: () => (model.SelectedMap == null ? "Please select a map to load for editing." : null, None()),
                apply: () => form.Options = model.BuildOptions(),
                onconfigchanged: () => { },
                configchangeable: false, showresources: false);
            maps.DoubleTapped += (s, e) => window.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            return ShowWindow(window);
        }
        finally { model.Dispose(); }
    }

    public override DialogResult ShowMapOptions(MapOptionsForm form)
    {
        if (Owner == null) return DialogResult.Cancel;

        var model = new MapOptionsModel(form.Options, form.NewMap);
        if (model.Configs.Count == 0)
        {
            ShowMessage("There is no game configuration enabled. Enable one in the game configurations first.", "TheDoomBuilder", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            return DialogResult.Cancel;
        }

        // Map names are upper case letters, digits and underscore: other keys are simply not typed
        var name = new TextBox { Text = model.LevelName, MaxLength = 8 };
        name.AddHandler(InputElement.TextInputEvent, (s, e) =>
        {
            if (e.Text != null && !e.Text.All(MapOptionsModel.IsAllowedNameChar)) e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var example = new TextBlock { Text = "Example: " + model.ExampleName, Opacity = 0.6 };
        // Watch the property itself so programmatic changes count too, not only typing
        name.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            string upper = (name.Text ?? "").ToUpperInvariant();
            if (upper != name.Text) { name.Text = upper; return; }   // names are upper case
            model.LevelName = upper;
        };

        var page = new StackPanel { Spacing = 3 };
        page.Children.Add(new TextBlock { Text = "Level name" });
        page.Children.Add(name);
        page.Children.Add(example);

        var window = new MapOptionsWindow(form.NewMap ? "New Map" : "Map Options", model, page, General.Map?.FilePathName,
            validate: () => { string error = model.Validate(out List<string> warnings); return (error, warnings); },
            apply: () => form.Options = model.Apply(),
            onconfigchanged: () => { name.Text = model.LevelName; example.Text = "Example: " + model.ExampleName; });

        return ShowWindow(window);
    }

    public override void ShowErrors()
    {
        if (Owner == null) return;
        var window = new ErrorsWindow();
        DialogPump.Run(() => window.ShowDialog<object>(Owner));
    }

    public override void ShowAbout()
    {
        if (Owner == null) return;
        var window = new AboutWindow();
        DialogPump.Run(() => window.ShowDialog<object>(Owner));
    }

    private DialogResult ShowWindow(MapOptionsWindow window)
    {
        bool ok = DialogPump.Run(() => window.ShowDialog<bool>(Owner));
        return ok ? DialogResult.OK : DialogResult.Cancel;
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(control);
        return panel;
    }
}
