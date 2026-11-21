using System;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class HexenMapReadTests : MapIoTestBase
{
    [Fact]
    public void Reads_action_specials_arguments_and_thing_heights()
    {
        using (var wad = new WAD(WadPath))
        {
            AddLump(wad, "MAP01", Array.Empty<byte>());
            AddLump(wad, "THINGS", Bytes(w =>
            {
                w.Write((short)5);                       // tid
                w.Write((short)96); w.Write((short)-32); // x, y
                w.Write((short)24);                      // z (height above floor)
                w.Write((short)180);                     // angle
                w.Write((short)1);                       // type: player 1 start
                w.Write((short)0x0007);                  // flags
                w.Write((byte)80);                       // action special
                w.Write(new byte[] { 1, 2, 3, 4, 5 });   // args
            }));
            AddLump(wad, "LINEDEFS", Bytes(w =>
            {
                for (int i = 0; i < 3; i++)
                {
                    w.Write((short)i); w.Write((short)((i + 1) % 3)); w.Write((short)1);
                    w.Write((byte)(i == 0 ? 11 : 0));              // action special on the first line
                    w.Write(new byte[] { (byte)(i == 0 ? 9 : 0), 0, 0, 0, 0 });
                    w.Write((short)i); w.Write((short)-1);
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
            AddLump(wad, "BEHAVIOR", Array.Empty<byte>());
            wad.WriteHeaders();
        }

        MapSet map = ReadMap01(wad => new HexenMapSetIO(wad, General.Map), "GZDoom_HexenHexen.cfg");

        Assert.Equal(3, map.Linedefs.Count);
        Linedef special = Assert.Single(map.Linedefs, l => l.Action == 11);
        Assert.Equal(9, special.Args[0]);

        Thing thing = Assert.Single(map.Things);
        Assert.Equal(1, thing.Type);
        Assert.Equal(5, thing.Tag);
        Assert.Equal(80, thing.Action);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, thing.Args);
        Assert.Equal(-32, thing.Position.y);
        Assert.Equal(24, thing.Position.z);
        AssertNoErrors();
    }
}
