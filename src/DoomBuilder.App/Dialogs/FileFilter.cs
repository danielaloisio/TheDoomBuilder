using System;
using System.Collections.Generic;
using Avalonia.Platform.Storage;

namespace DoomBuilder.App.Dialogs;

/// <summary>Converts WinForms file filters ("Doom WAD Files (*.wad)|*.wad;*.WAD|All files|*.*") to Avalonia picker file types.</summary>
public static class FileFilter
{
    /// <summary>
    /// Every file, by name pattern only. Avalonia's own FilePickerFileTypes.All also carries MIME types ("*/*"), which the Linux portal turns
    /// into a content-type filter that leaves executables (application/x-pie-executable) out of the list.
    /// </summary>
    public static FilePickerFileType AllFiles { get; } = new FilePickerFileType("All files") { Patterns = new[] { "*" } };

    public static List<FilePickerFileType> Parse(string filter)
    {
        var types = new List<FilePickerFileType>();
        if (string.IsNullOrWhiteSpace(filter)) return types;

        string[] parts = filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = new List<string>();
            foreach (string pattern in parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                patterns.Add(pattern);

            types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
        }
        return types;
    }

    /// <summary>
    /// The file extension patterns are case-insensitive on Windows but not everywhere: the UDB filter lists every case spelling of
    /// ".wad" for that reason. Collapses them back to one pattern per extension for pickers that match case-insensitively.
    /// </summary>
    public static List<FilePickerFileType> ParseCaseInsensitive(string filter)
    {
        var types = Parse(filter);
        foreach (var type in types)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distinct = new List<string>();
            foreach (string pattern in type.Patterns)
                if (seen.Add(pattern)) distinct.Add(pattern);
            type.Patterns = distinct;
        }
        return types;
    }
}
