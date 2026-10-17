using System.Linq;
using CodeImp.DoomBuilder.ZDoom;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class ZDoomParserTests : MapIoTestBase
{
    // Some parsers derive a virtual path from the lump name, so give them a realistic one.
    private static CodeImp.DoomBuilder.Data.TextResourceData Text(string content, string lumpname = "TEXTURES")
        => new(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
               new CodeImp.DoomBuilder.Data.DataLocation(CodeImp.DoomBuilder.Data.DataLocation.RESOURCE_DIRECTORY, string.Empty, false, false, false, []),
               lumpname);

    [Fact]
    public void Textures_parser_reads_composite_textures_and_patches()
    {
        UseGameConfig("GZDoom_DoomUDMF.cfg");   // texture name limits come from the game configuration
        var parser = new TexturesParser();
        bool ok = parser.Parse(Text(@"
            // composite wall texture
            Texture ""MYWALL"", 128, 64
            {
                XScale 2.0
                Patch ""WALL00_1"", 0, 0
                Patch ""TRIM01"", 16, 8 { FlipX }
            }
            Flat ""MYFLAT"", 64, 64 { Patch ""FLOOR0_1"", 0, 0 }
        "), true);

        Assert.True(ok, parser.ErrorDescription);

        var wall = Assert.Single(parser.Textures);   // "Texture" goes to the generic list; "WallTexture" is separate
        Assert.Equal("MYWALL", wall.Name);
        Assert.Equal(128, wall.Width);
        Assert.Equal(64, wall.Height);
        Assert.Equal(2.0f, wall.XScale);
        Assert.Equal(new[] { "WALL00_1", "TRIM01" }, wall.Patches.Select(p => p.Name));
        var trim = wall.Patches.Last();
        Assert.Equal(16, trim.OffsetX);
        Assert.Equal(8, trim.OffsetY);
        Assert.True(trim.FlipX);

        Assert.Equal("MYFLAT", Assert.Single(parser.Flats).Name);
    }

    [Fact]
    public void Textures_parser_reports_syntax_errors_instead_of_throwing()
    {
        UseGameConfig("GZDoom_DoomUDMF.cfg");
        var parser = new TexturesParser();
        bool ok = parser.Parse(Text(@"Texture ""BROKEN"", 64 { "), true);

        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(parser.ErrorDescription));
    }

    [Fact]
    public void Sndinfo_parser_reads_sound_definitions_and_ambients()
    {
        UseGameConfig("Doom_DoomDoom.cfg");   // ambient sound limits depend on the map format
        var parser = new SndInfoParser();
        bool ok = parser.Parse(Text(@"
            dsplasma weapons/plasma
            $ambient 1 amb/wind point continuous 1.0
            $random monsters/idle { dsposit1 dsposit2 }
        ", "SNDINFO"), true);

        Assert.True(ok, parser.ErrorDescription);
        Assert.Contains(parser.AmbientSounds, a => a.Key == 1);
        Assert.Contains(parser.Sounds.Keys, k => k.Equals("monsters/idle", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mapinfo_parser_reads_map_title_and_sky()
    {
        var parser = new MapinfoParser();
        bool ok = parser.Parse(Text(@"
            map MAP01 ""Entryway""
            {
                sky1 = ""SKY1"", 0.5
                next = ""MAP02""
            }
        ", "MAPINFO"), "MAP01", true);

        Assert.True(ok, parser.ErrorDescription);
        Assert.Equal("Entryway", parser.MapInfo.Title);
        Assert.Equal("SKY1", parser.MapInfo.Sky1);
    }
}
