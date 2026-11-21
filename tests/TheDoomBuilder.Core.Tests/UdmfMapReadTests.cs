using System;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class UdmfMapReadTests : MapIoTestBase
{
    private const string TextMap = @"
namespace = ""zdoom"";
vertex { x = 0.0; y = 0.0; }
vertex { x = 128.5; y = 0.0; }
vertex { x = 0.0; y = 128.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; id = 7; special = 80; arg0 = 3; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 0; sidefront = 2; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; offsetx = 4; user_note = ""hi""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 8; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; id = 5; }
thing { x = 32.25; y = 32.0; height = 16.0; type = 1; angle = 90; skill1 = true; skill2 = true; single = true; }
";

    [Fact]
    public void Reads_floating_point_coordinates_custom_fields_and_flags()
    {
        using (var wad = new WAD(WadPath))
        {
            AddLump(wad, "MAP01", Array.Empty<byte>());
            AddLump(wad, "TEXTMAP", TextMap);
            AddLump(wad, "ENDMAP", Array.Empty<byte>());
            wad.WriteHeaders();
        }

        MapSet map = ReadMap01(wad => new UniversalMapSetIO(wad, General.Map), "GZDoom_DoomUDMF.cfg");

        Assert.Equal(3, map.Vertices.Count);
        Assert.Contains(map.Vertices, v => v.Position.x == 128.5);
        Assert.Equal(3, map.Linedefs.Count);
        Assert.Equal(3, map.Sidedefs.Count);

        Sector sector = Assert.Single(map.Sectors);
        Assert.Equal(8, sector.FloorHeight);
        Assert.Equal(192, sector.Brightness);
        Assert.Equal(5, sector.Tag);

        Linedef special = Assert.Single(map.Linedefs, l => l.Action == 80);
        Assert.Equal(3, special.Args[0]);
        Assert.Equal(7, special.Tag);
        Assert.True(special.IsFlagSet("blocking"));

        Sidedef offset = Assert.Single(map.Sidedefs, s => s.OffsetX == 4);
        Assert.Equal("hi", offset.Fields["user_note"].Value);

        Thing thing = Assert.Single(map.Things);
        Assert.Equal(32.25, thing.Position.x);
        Assert.Equal(16, thing.Position.z);
        Assert.True(thing.IsFlagSet("skill1"));
        AssertNoErrors();
    }
}
