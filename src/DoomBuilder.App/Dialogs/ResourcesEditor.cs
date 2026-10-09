using DoomBuilder.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CodeImp.DoomBuilder.Data;

using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// The resource list of the map options: WADs, PK3/PK7 archives and folders to load textures, flats and sprites from, in load
/// order. The game configuration's own resources (the IWAD) are shown above, read-only.
/// </summary>
public sealed class ResourcesEditor : UserControl
{
    private readonly ListBox fixedlist = new ListBox { MaxHeight = 90, IsHitTestVisible = false, Opacity = 0.7 };
    private readonly ListBox list = new ListBox { MinHeight = 90, MaxHeight = 160 };
    private readonly Button remove = new Button { Content = Loc.T("Remove") };
    private readonly Button up = new Button { Content = Loc.T("Up") };
    private readonly Button down = new Button { Content = Loc.T("Down") };
    private readonly Button addfile = new Button { Content = Loc.T("Add file...") };
    private readonly Button addfolder = new Button { Content = Loc.T("Add folder...") };

    /// <summary>The user added, removed or moved a resource.</summary>
    public event Action Changed;

    private DataLocationList resources = new DataLocationList();
    private string startpath;

    public ResourcesEditor()
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        foreach (Button b in new[] { addfile, addfolder, remove, up, down }) buttons.Children.Add(b);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Resources", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        panel.Children.Add(fixedlist);
        panel.Children.Add(list);
        panel.Children.Add(buttons);
        Content = panel;

        addfile.Click += (s, e) => AddFiles();
        addfolder.Click += (s, e) => AddFolder();
        remove.Click += (s, e) => Remove();
        up.Click += (s, e) => Move(-1);
        down.Click += (s, e) => Move(1);
        list.SelectionChanged += (s, e) => UpdateButtons();
        UpdateButtons();
    }

    /// <summary>The resources being edited (the same list object the model owns).</summary>
    public DataLocationList Locations
    {
        get { return resources; }
        set { resources = value ?? new DataLocationList(); Refresh(); }
    }

    /// <summary>Resources that come with the game configuration.</summary>
    public void SetFixed(IEnumerable<DataLocation> items)
    {
        fixedlist.ItemsSource = items.Select(Describe).ToList();
        fixedlist.IsVisible = fixedlist.ItemCount > 0;
    }

    /// <summary>The folder the file picker opens in (the map's folder).</summary>
    public string StartPath { set { startpath = value; } }

    public bool HasMissingResources { get { return !resources.IsValid(); } }

    private static string Describe(DataLocation dl)
    {
        string kind = dl.type == DataLocation.RESOURCE_DIRECTORY ? "folder" : dl.type == DataLocation.RESOURCE_PK3 ? "archive" : "wad";
        string missing = dl.IsValid() ? "" : "  (not found)";
        return dl.GetDisplayName() + "  [" + kind + "]  " + dl.location + missing;
    }

    private void Refresh()
    {
        int selected = list.SelectedIndex;
        list.ItemsSource = resources.Select(Describe).ToList();
        if (selected >= 0 && selected < resources.Count) list.SelectedIndex = selected;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int i = list.SelectedIndex;
        remove.IsEnabled = i >= 0;
        up.IsEnabled = i > 0;
        down.IsEnabled = i >= 0 && i < resources.Count - 1;
    }

    /// <summary>Adds a resource, deciding the kind from the path (folder, WAD or archive).</summary>
    public void Add(string path)
    {
        int type = Directory.Exists(path) ? DataLocation.RESOURCE_DIRECTORY
                 : path.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ? DataLocation.RESOURCE_WAD : DataLocation.RESOURCE_PK3;

        var location = new DataLocation(type, path, false, false, false, new List<string>());
        if (!resources.Contains(location)) resources.Add(location);
        Refresh();
        list.SelectedIndex = resources.Count - 1;
        Changed?.Invoke();
    }

    private void Remove()
    {
        int i = list.SelectedIndex;
        if (i < 0) return;
        resources.RemoveAt(i);
        Refresh();
        Changed?.Invoke();
    }

    private void Move(int delta)
    {
        int i = list.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= resources.Count) return;
        (resources[i], resources[j]) = (resources[j], resources[i]);
        Refresh();
        list.SelectedIndex = j;
        Changed?.Invoke();
    }

    private void AddFiles()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = DialogPump.Run(() => storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Add resource"),
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Doom resources") { Patterns = new[] { "*.wad", "*.pk3", "*.pk7", "*.zip", "*.ipk3", "*.ipk7" } },
                FilePickerFileTypes.All,
            },
            SuggestedStartLocation = StartFolder(storage),
        }));

        foreach (IStorageFile file in files ?? Array.Empty<IStorageFile>())
            if (file.TryGetLocalPath() is string path) Add(path);
    }

    private void AddFolder()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var folders = DialogPump.Run(() => storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.T("Add resource folder"), SuggestedStartLocation = StartFolder(storage) }));
        foreach (IStorageFolder folder in folders ?? Array.Empty<IStorageFolder>())
            if (folder.TryGetLocalPath() is string path) Add(path);
    }

    private IStorageFolder StartFolder(IStorageProvider storage)
    {
        string dir = string.IsNullOrEmpty(startpath) ? null : (Directory.Exists(startpath) ? startpath : Path.GetDirectoryName(startpath));
        return dir != null && Directory.Exists(dir) ? DialogPump.Run(() => storage.TryGetFolderFromPathAsync(dir)) : null;
    }
}
