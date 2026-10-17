using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class CommandPaletteMatchTests
{
    [Theory]
    [InlineData("Export Selection", "", true)]
    [InlineData("Export Selection", "select", true)]       // contained
    [InlineData("Export Selection", "ex sel", true)]       // words abbreviated
    [InlineData("Export Selection", "sel ex", false)]      // wrong order
    [InlineData("Open Command Palette", "pal", true)]
    [InlineData("Open Command Palette", "  OPEN   comm ", true)]
    [InlineData("Open Command Palette", "zzz", false)]
    [InlineData("Open Command Palette", "open pal x", false)]
    public void Text_matches_the_way_the_udb_palette_does(string title, string typed, bool expected)
        => Assert.Equal(expected, CommandPaletteModel.MatchText(title, typed));
}

[Collection("General static state")]
public class CommandPaletteModelTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-palette-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public CommandPaletteModelTests()
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

    [Fact]
    public void Everything_is_listed_when_nothing_is_typed_with_the_runnable_actions_before_the_rest()
    {
        var entries = new CommandPaletteModel().Search("");

        Assert.Equal(General.Actions.GetAllActions().Length, entries.Count);
        int firstunusable = entries.FindIndex(e => !e.Usable);
        if (firstunusable >= 0) Assert.All(entries.Skip(firstunusable), e => Assert.False(e.Usable));      // no runnable one after that
        var usable = entries.Where(e => e.Usable).Select(e => e.Title).ToList();
        Assert.Equal(usable.OrderBy(t => t, StringComparer.CurrentCulture).ToList(), usable.OrderBy(t => t).ToList());
    }

    [Fact]
    public void Typing_narrows_the_list_and_the_last_ones_run_come_first()
    {
        var palette = new CommandPaletteModel();
        var found = palette.Search("command palette");
        Assert.Contains(found, e => e.Title == "Open Command Palette");
        Assert.True(found.Count < General.Actions.GetAllActions().Length);

        var entry = found.First(e => e.Title == "Open Command Palette");
        palette.Run(entry);

        var again = palette.Search("");
        Assert.Equal(PaletteGroup.Recent, again[0].Group);
        Assert.Equal("Open Command Palette", again[0].Title);
        Assert.Equal(new[] { entry.ActionName }, palette.RecentNames);
        Assert.DoesNotContain(palette.Search("palette"), e => e.Group == PaletteGroup.Recent);   // recent only without a search
    }

    [Fact]
    public void Only_five_recent_actions_are_kept()
    {
        var palette = new CommandPaletteModel();
        foreach (var entry in palette.Search("").Take(8)) palette.Run(entry);

        Assert.Equal(CommandPaletteModel.MaxRecent, palette.RecentNames.Count);
    }
}
