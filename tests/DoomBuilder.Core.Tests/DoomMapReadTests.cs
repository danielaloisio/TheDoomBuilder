using System;
using System.IO;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>Phase 1 milestone: read a Doom-format map from a WAD without any UI.</summary>
public class DoomMapReadTests : MapIoTestBase
{
    /// <summary>A triangular room: 3 vertices, 3 linedefs, 3 sidedefs, 1 sector, 2 things.</summary>
    private void WriteTriangleMap()
    {
        using var wad = new WAD(WadPath);
        AddLump(wad, "MAP01", Array.Empty<byte>());
        AddLump(wad, "THINGS", Bytes(w =>
        {
            w.Write((short)32); w.Write((short)32); w.Write((short)90); w.Write((short)1); w.Write((short)7);     // player 1 start
            w.Write((short)64); w.Write((short)16); w.Write((short)0); w.Write((short)3004); w.Write((short)7);   // zombieman
        }));
        AddLump(wad, "LINEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 3; i++)
            {
                w.Write((short)i); w.Write((short)((i + 1) % 3)); w.Write((short)1); // flags: impassable
                w.Write((short)0); w.Write((short)0);                                 // action, tag
                w.Write((short)i); w.Write((short)-1);                                // right sidedef, no left
            }
        }));
        AddLump(wad, "SIDEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 3; i++)
            {
                w.Write((short)0); w.Write((short)0);
                Name8(w, "-"); Name8(w, "-"); Name8(w, "STARTAN1");
                w.Write((short)0);
            }
        }));
        AddLump(wad, "VERTEXES", Bytes(w =>
        {
            w.Write((short)0); w.Write((short)0);
            w.Write((short)128); w.Write((short)0);
            w.Write((short)0); w.Write((short)128);
        }));
        AddLump(wad, "SECTORS", Bytes(w =>
        {
            w.Write((short)0); w.Write((short)128);
            Name8(w, "FLOOR4_8"); Name8(w, "CEIL3_5");
            w.Write((short)192); w.Write((short)0); w.Write((short)5);
        }));
        wad.WriteHeaders();
    }

    [Fact]
    public void Reads_vertices_linedefs_sidedefs_sectors_and_things()
    {
        WriteTriangleMap();
        MapSet map = ReadMap01(wad => new DoomMapSetIO(wad, General.Map), "Doom_DoomDoom.cfg");

        Assert.Equal(3, map.Vertices.Count);
        Assert.Equal(3, map.Linedefs.Count);
        Assert.Equal(3, map.Sidedefs.Count);
        Assert.Single(map.Sectors);
        Assert.Equal(2, map.Things.Count);

        Sector sector = Assert.Single(map.Sectors);
        Assert.Equal(0, sector.FloorHeight);
        Assert.Equal(128, sector.CeilHeight);
        Assert.Equal("FLOOR4_8", sector.FloorTexture);
        Assert.Equal("CEIL3_5", sector.CeilTexture);
        Assert.Equal(192, sector.Brightness);
        Assert.Equal(5, sector.Tag);

        Assert.Contains(map.Things, t => t.Type == 1 && t.Position.x == 32 && t.Position.y == 32);
        Assert.Contains(map.Things, t => t.Type == 3004);
        Assert.All(map.Linedefs, l => Assert.NotNull(l.Front));
        Assert.All(map.Linedefs, l => Assert.Null(l.Back));
        AssertNoErrors();
    }
}
