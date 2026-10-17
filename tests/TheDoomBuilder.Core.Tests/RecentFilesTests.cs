using System;
using System.IO;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

[Collection("General static state")]
public class RecentFilesTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-recent-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public RecentFilesTests()
    {
        Directory.CreateDirectory(dir);
        Assert.True(General.Startup(new[] { "-nosettings" }, () => new HeadlessMainWindow(), appdir, dir));
    }

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    private string File(string name) { string p = Path.Combine(dir, name); System.IO.File.WriteAllText(p, ""); return p; }

    [Fact]
    public void The_most_recent_file_comes_first_and_a_repeated_file_moves_up()
    {
        var recent = new RecentFiles();
        string a = File("a.wad"), b = File("b.wad"), c = File("c.wad");

        recent.Add(a); recent.Add(b); recent.Add(c);
        Assert.Equal(new[] { c, b, a }, recent.Files);

        recent.Add(a);
        Assert.Equal(new[] { a, c, b }, recent.Files);
    }

    [Fact]
    public void Paths_that_differ_only_in_case_are_the_same_file()
    {
        var recent = new RecentFiles();
        string a = File("Map.wad");

        recent.Add(a);
        recent.Add(a.ToUpperInvariant());

        Assert.Single(recent.Files);
    }

    [Fact]
    public void The_list_never_grows_past_the_maximum()
    {
        var recent = new RecentFiles();
        for (int i = 0; i < recent.Maximum + 5; i++) recent.Add(File("m" + i + ".wad"));

        Assert.Equal(recent.Maximum, recent.Files.Count);
        Assert.EndsWith("m" + (recent.Maximum + 4) + ".wad", recent.Files[0]);
    }

    [Fact]
    public void The_list_is_saved_in_the_settings_and_loaded_again_without_files_that_are_gone()
    {
        var recent = new RecentFiles();
        string a = File("a.wad"), b = File("b.wad");
        recent.Add(a); recent.Add(b);
        System.IO.File.Delete(a);

        var again = new RecentFiles();
        again.Load();

        Assert.Equal(new[] { b }, again.Files);
    }

    [Fact]
    public void Long_paths_are_shortened_in_the_middle()
    {
        string path = "/home/someone/projects/very/long/folder/structure/with/many/levels/map.wad";
        string text = RecentFiles.MenuText(path, 40);

        Assert.True(text.Length <= 40);
        Assert.StartsWith("/ho", text);
        Assert.Contains("...", text);
        Assert.EndsWith("map.wad", text);
        Assert.Equal("short.wad", RecentFiles.MenuText("short.wad"));
    }

    [Fact]
    public void A_file_renamed_by_case_is_found_under_its_new_spelling()
    {
        string real = File("Real.wad");
        string asked = Path.Combine(dir, "real.wad");

        string found = RecentFiles.FindExistingFile(asked);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()) Assert.NotNull(found);   // case-insensitive file systems
        else Assert.Null(found);                                                                // there "real.wad" simply does not exist
        Assert.Equal(real, RecentFiles.FindExistingFile(real));
        Assert.Null(RecentFiles.FindExistingFile(Path.Combine(dir, "nothing", "x.wad")));
    }
}
