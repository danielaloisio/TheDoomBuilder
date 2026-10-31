using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CodeImp.DoomBuilder.Data;
using TheDoomBuilder.UI;

using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace TheDoomBuilder.Application.Dialogs;

/// <summary>
/// Add/edit a single resource location (UDB's ResourceOptionsForm): WAD, PK3/PK7/zip archive or folder, plus the
/// "exclude this resource from testing parameters" flag (<see cref="DataLocation.notfortesting"/>) used by
/// <see cref="CodeImp.DoomBuilder.General.Launcher"/> to skip resources the test engine already loads on its own
/// (e.g. the engine's own base resource pack).
/// </summary>
public sealed class ResourceOptionsWindow : Window
{
    private readonly ComboBox typebox = new ComboBox { MinWidth = 160 };
    private readonly TextBox locationbox = new TextBox { MinWidth = 320 };
    private readonly Button browse = new Button { Content = Loc.T("Browse...") };
    private readonly CheckBox strictpatches = new CheckBox { Content = Loc.T("Strict patches") };
    private readonly CheckBox dirtextures = new CheckBox { Content = Loc.T("Use as textures") };
    private readonly CheckBox dirflats = new CheckBox { Content = Loc.T("Use as flats") };
    private readonly CheckBox notfortesting = new CheckBox { Content = Loc.T("Exclude this resource from testing parameters") };
    private readonly string startpath;

    public DataLocation ResourceLocation { get; private set; }

    public ResourceOptionsWindow(DataLocation settings, string caption, string startPath)
    {
        startpath = startPath;
        Title = caption;
        Width = 480;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        typebox.ItemsSource = new[] { Loc.T("WAD file"), Loc.T("Directory"), Loc.T("PK3/PK7/ZIP file") };
        typebox.SelectedIndex = settings.type;
        typebox.SelectionChanged += (s, e) => UpdateOptionsVisibility();

        locationbox.Text = settings.location;
        strictpatches.IsChecked = settings.option1;
        dirtextures.IsChecked = settings.option1;
        dirflats.IsChecked = settings.option2;
        notfortesting.IsChecked = settings.notfortesting;

        browse.Click += (s, e) => Browse();

        var locationrow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        locationrow.Children.Add(locationbox);
        locationrow.Children.Add(browse);

        var panel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(14) };
        panel.Children.Add(new TextBlock { Text = Loc.T("Resource type:") });
        panel.Children.Add(typebox);
        panel.Children.Add(new TextBlock { Text = Loc.T("Location:") });
        panel.Children.Add(locationrow);
        panel.Children.Add(strictpatches);
        panel.Children.Add(dirtextures);
        panel.Children.Add(dirflats);
        panel.Children.Add(notfortesting);

        var ok = new Button { Content = Loc.T("OK"), MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = Loc.T("Cancel"), MinWidth = 90, IsCancel = true };
        ok.Click += (s, e) => Accept();
        cancel.Click += (s, e) => Close(false);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;
        UpdateOptionsVisibility();
    }

    private void UpdateOptionsVisibility()
    {
        int type = typebox.SelectedIndex;
        strictpatches.IsVisible = type == DataLocation.RESOURCE_WAD;
        dirtextures.IsVisible = type == DataLocation.RESOURCE_DIRECTORY;
        dirflats.IsVisible = type == DataLocation.RESOURCE_DIRECTORY;
    }

    private void Browse()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        int type = typebox.SelectedIndex;
        if (type == DataLocation.RESOURCE_DIRECTORY)
        {
            var folders = DialogPump.Run(() => storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.T("Select Resource Folder"), SuggestedStartLocation = StartFolder(storage) }));
            foreach (IStorageFolder folder in folders ?? Array.Empty<IStorageFolder>())
                if (folder.TryGetLocalPath() is string path) { locationbox.Text = path; break; }
        }
        else
        {
            var filter = type == DataLocation.RESOURCE_WAD
                ? new FilePickerFileType(Loc.T("Doom WAD files")) { Patterns = new[] { "*.wad" } }
                : new FilePickerFileType(Loc.T("Doom PK3/PKE/PK7 files")) { Patterns = new[] { "*.pk3", "*.pke", "*.pk7", "*.ipk3", "*.ipk7", "*.zip" } };

            var files = DialogPump.Run(() => storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Loc.T("Select Resource File"),
                AllowMultiple = false,
                FileTypeFilter = new[] { filter, FileFilter.AllFiles },
                SuggestedStartLocation = StartFolder(storage),
            }));
            foreach (IStorageFile file in files ?? Array.Empty<IStorageFile>())
                if (file.TryGetLocalPath() is string path) { locationbox.Text = path; break; }
        }
    }

    private IStorageFolder StartFolder(IStorageProvider storage)
    {
        string dir = string.IsNullOrEmpty(startpath) ? null : (Directory.Exists(startpath) ? startpath : Path.GetDirectoryName(startpath));
        return dir != null && Directory.Exists(dir) ? DialogPump.Run(() => storage.TryGetFolderFromPathAsync(dir)) : null;
    }

    private void Accept()
    {
        int type = typebox.SelectedIndex;
        string location = locationbox.Text ?? "";
        bool valid = type == DataLocation.RESOURCE_DIRECTORY ? Directory.Exists(location) : File.Exists(location);
        if (!valid)
        {
            CodeImp.DoomBuilder.General.ShowErrorMessage(type == DataLocation.RESOURCE_DIRECTORY
                ? Loc.T("Please select a valid directory resource.")
                : Loc.T("Please select a valid WAD, PK3, PKE, or PK7 File resource."), System.Windows.Forms.MessageBoxButtons.OK);
            return;
        }

        ResourceLocation = new DataLocation(type, location,
            type == DataLocation.RESOURCE_WAD ? (strictpatches.IsChecked ?? false) : type == DataLocation.RESOURCE_DIRECTORY ? (dirtextures.IsChecked ?? false) : false,
            type == DataLocation.RESOURCE_DIRECTORY ? (dirflats.IsChecked ?? false) : false,
            notfortesting.IsChecked ?? false,
            new List<string>());
        Close(true);
    }
}
