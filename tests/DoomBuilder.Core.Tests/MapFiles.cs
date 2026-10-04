using System;
using System.IO;
using CodeImp.DoomBuilder.IO;

namespace DoomBuilder.Core.Tests;

/// <summary>Writes small Doom-format maps for tests that need a real map open.</summary>
internal static class MapFiles
{
    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, true)) write(w);
        return ms.ToArray();
    }

    private static void Name8(BinaryWriter w, string name)
    {
        var b = new byte[8];
        System.Text.Encoding.ASCII.GetBytes(name, 0, name.Length, b, 0);
        w.Write(b);
    }

    /// <summary>A 128x128 square room (MAP01) with a player start. Returns the wad path.</summary>
    public static string WriteSquareRoomWad(string dir)
    {
        string path = Path.Combine(dir, "room.wad");
        using var wad = new WAD(path);
        void Add(string name, byte[] data) { var l = wad.Insert(name, wad.Lumps.Count, data.Length); l.Stream.Write(data, 0, data.Length); }

        Add("MAP01", Array.Empty<byte>());
        Add("THINGS", Bytes(w => { w.Write((short)64); w.Write((short)64); w.Write((short)90); w.Write((short)1); w.Write((short)7); }));
        Add("LINEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 4; i++)
            {
                w.Write((short)i); w.Write((short)((i + 1) % 4)); w.Write((short)1);
                w.Write((short)0); w.Write((short)0); w.Write((short)i); w.Write((short)-1);
            }
        }));
        Add("SIDEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 4; i++)
            {
                w.Write((short)0); w.Write((short)0);
                Name8(w, "-"); Name8(w, "-"); Name8(w, "STARTAN1"); w.Write((short)0);
            }
        }));
        Add("VERTEXES", Bytes(w =>
        {
            foreach (var (x, y) in new[] { (0, 0), (128, 0), (128, 128), (0, 128) }) { w.Write((short)x); w.Write((short)y); }
        }));
        Add("SECTORS", Bytes(w =>
        {
            w.Write((short)0); w.Write((short)128);
            Name8(w, "FLOOR4_8"); Name8(w, "CEIL3_5"); w.Write((short)192); w.Write((short)0); w.Write((short)0);
        }));
        wad.WriteHeaders();
        return path;
    }
}
