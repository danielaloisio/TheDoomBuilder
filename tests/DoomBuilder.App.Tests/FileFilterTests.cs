using System.Linq;
using DoomBuilder.App.Dialogs;
using Xunit;

namespace DoomBuilder.App.Tests;

public class FileFilterTests
{
    [Fact]
    public void The_all_files_filter_matches_by_name_only_so_the_linux_portal_lists_executables()
    {
        var all = FileFilter.AllFiles;
        Assert.Equal(new[] { "*" }, all.Patterns.ToArray());
        Assert.Null(all.MimeTypes);                         // "*/*" becomes a content-type filter there, and a program (x-pie-executable) is not listed
        Assert.Null(all.AppleUniformTypeIdentifiers);
    }

    [Fact]
    public void A_dialog_filter_string_becomes_one_type_per_pair()
    {
        var types = FileFilter.Parse("Doom WAD Files (*.wad)|*.wad;*.WAD|All files|*.*");
        Assert.Equal(2, types.Count);
        Assert.Equal(new[] { "*.wad", "*.WAD" }, types[0].Patterns.ToArray());
    }
}
