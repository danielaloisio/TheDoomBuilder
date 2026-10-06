using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DoomBuilder.App.Tests;

/// <summary>
/// A Doom map made of rooms in a row (each 128 high, as wide as asked), with the BSP, the segs and the subsectors that a nodebuilder would make for it:
/// small enough to write by hand, and with exactly the right node lumps, so that the code that reads them can be checked.
/// Sector i spans x from the sum of the widths before it; every wall is a line of its own, the lines between rooms are two-sided.
/// </summary>
public sealed class RowMap
{
    public sealed class Room
    {
        public int Width = 128;
        public int Floor = 0, Ceiling = 128;
        public string CeilingFlat = "CEIL3_5";
        public int Light = 192;
        public int Tag;
    }

    public const int Height = 128;

    public List<Room> Rooms { get; } = new List<Room>();
    /// <summary>The line between room i and i+1 gets this special (a manual door for 1, "DR door").</summary>
    public Dictionary<int, int> BorderSpecials { get; } = new Dictionary<int, int>();

    public RowMap(params Room[] rooms) { Rooms.AddRange(rooms); }

    public int Left(int room) => Rooms.Take(room).Sum(r => r.Width);

    // ---- the lumps
    private readonly MemoryStream vertexes = new MemoryStream(), linedefs = new MemoryStream(), sidedefs = new MemoryStream(), segs = new MemoryStream();
    private readonly MemoryStream ssectors = new MemoryStream(), nodes = new MemoryStream(), sectors = new MemoryStream(), things = new MemoryStream();
    private int numlines, numsides;
    private readonly Dictionary<int, int> borderline = new Dictionary<int, int>();       // border k (between k-1 and k) -> linedef number
    private readonly Dictionary<int, int> borderfront = new Dictionary<int, int>();      // k -> the sidedef of room k-1 (front)

    private static void Name(BinaryWriter w, string name)
    {
        byte[] b = new byte[8];
        Encoding.ASCII.GetBytes(name, 0, Math.Min(8, name.Length), b, 0);
        w.Write(b);
    }

    private int V(int boundary, int top) => boundary * 2 + top;

    private int AddSide(int sector, string upper, string lower, string middle)
    {
        var w = new BinaryWriter(sidedefs);
        w.Write((short)0); w.Write((short)0);
        Name(w, upper); Name(w, lower); Name(w, middle);
        w.Write((short)sector);
        return numsides++;
    }

    private int AddLine(int v1, int v2, int flags, int special, int front, int back)
    {
        var w = new BinaryWriter(linedefs);
        w.Write((short)v1); w.Write((short)v2); w.Write((short)flags); w.Write((short)special); w.Write((short)0); w.Write((short)front); w.Write((short)back);
        return numlines++;
    }

    private void AddSeg(int v1, int v2, int bam, int line, int side)
    {
        var w = new BinaryWriter(segs);
        w.Write((short)v1); w.Write((short)v2); w.Write((short)bam); w.Write((short)line); w.Write((short)side); w.Write((short)0);
    }

    private int BuildNode(int lo, int hi, List<(int x, int right, int left, int[] rbox, int[] lbox)> list)
    {
        if (hi - lo == 1) return 0x8000 | lo;
        int mid = (lo + hi) / 2;
        int right = BuildNode(mid, hi, list);     // the east part is on the right (front) of an upwards line
        int left = BuildNode(lo, mid, list);
        list.Add((Left(mid), right, left, new[] { Height, 0, Left(mid), Left(hi) }, new[] { Height, 0, Left(lo), Left(mid) }));
        return list.Count - 1;
    }

    /// <summary>Builds the lumps. After this the properties below hold them.</summary>
    public RowMap Build()
    {
        int n = Rooms.Count;
        var w = new BinaryWriter(vertexes);
        for (int k = 0; k <= n; k++) { w.Write((short)Left(k)); w.Write((short)0); w.Write((short)Left(k)); w.Write((short)Height); }

        // Sidedefs and lines of every room: north (going east), south (going west), the west wall and the east wall of the ends
        var north = new int[n]; var south = new int[n]; int west = -1, east = -1;
        for (int i = 0; i < n; i++)
        {
            north[i] = AddLine(V(i, 1), V(i + 1, 1), 1, 0, AddSide(i, "-", "-", "STARTAN1"), -1);
            south[i] = AddLine(V(i + 1, 0), V(i, 0), 1, 0, AddSide(i, "-", "-", "STARTAN1"), -1);
        }
        west = AddLine(V(0, 0), V(0, 1), 1, 0, AddSide(0, "-", "-", "STARTAN1"), -1);
        east = AddLine(V(n, 1), V(n, 0), 1, 0, AddSide(n - 1, "-", "-", "STARTAN1"), -1);
        for (int k = 1; k < n; k++)
        {
            int front = AddSide(k - 1, "-", "-", "-"), back = AddSide(k, "-", "-", "-");
            BorderSpecials.TryGetValue(k, out int special);
            borderline[k] = AddLine(V(k, 1), V(k, 0), 4, special, front, back);
        }

        // The segs and subsectors
        var ss = new BinaryWriter(ssectors);
        for (int i = 0; i < n; i++)
        {
            int first = (int)segs.Length / 12;
            if (i == 0) AddSeg(V(0, 0), V(0, 1), 0x4000, west, 0);
            else AddSeg(V(i, 0), V(i, 1), 0x4000, borderline[i], 1);
            AddSeg(V(i, 1), V(i + 1, 1), 0, north[i], 0);
            if (i == n - 1) AddSeg(V(n, 1), V(n, 0), -0x4000, east, 0);
            else AddSeg(V(i + 1, 1), V(i + 1, 0), -0x4000, borderline[i + 1], 0);
            AddSeg(V(i + 1, 0), V(i, 0), -0x8000, south[i], 0);
            ss.Write((short)4); ss.Write((short)first);
        }

        // The tree
        var list = new List<(int x, int right, int left, int[] rbox, int[] lbox)>();
        if (n > 1) BuildNode(0, n, list);
        var nw = new BinaryWriter(nodes);
        foreach (var node in list)
        {
            nw.Write((short)node.x); nw.Write((short)0); nw.Write((short)0); nw.Write((short)Height);
            foreach (int b in node.rbox) nw.Write((short)b);
            foreach (int b in node.lbox) nw.Write((short)b);
            nw.Write((ushort)node.right); nw.Write((ushort)node.left);
        }

        var sw = new BinaryWriter(sectors);
        foreach (Room r in Rooms)
        {
            sw.Write((short)r.Floor); sw.Write((short)r.Ceiling); Name(sw, "FLOOR4_8"); Name(sw, r.CeilingFlat); sw.Write((short)r.Light); sw.Write((short)0); sw.Write((short)r.Tag);
        }

        var tw = new BinaryWriter(things);
        tw.Write((short)64); tw.Write((short)64); tw.Write((short)0); tw.Write((short)1); tw.Write((short)7);
        return this;
    }

    public byte[] Vertexes => vertexes.ToArray();
    public byte[] Linedefs => linedefs.ToArray();
    public byte[] Sidedefs => sidedefs.ToArray();
    public byte[] Segs => segs.ToArray();
    public byte[] Ssectors => ssectors.ToArray();
    public byte[] Nodes => nodes.ToArray();
    public byte[] Sectors => sectors.ToArray();
    public byte[] Things => things.ToArray();

    /// <summary>The lumps of the map in the order of a map, with the header.</summary>
    public (string Name, byte[] Data)[] Lumps(string mapname = "MAP01", bool withnodes = true) => new (string, byte[])[]
    {
        (mapname, Array.Empty<byte>()), ("THINGS", Things), ("LINEDEFS", Linedefs), ("SIDEDEFS", Sidedefs), ("VERTEXES", Vertexes),
        ("SEGS", withnodes ? Segs : Array.Empty<byte>()), ("SSECTORS", withnodes ? Ssectors : Array.Empty<byte>()), ("NODES", withnodes ? Nodes : Array.Empty<byte>()),
        ("SECTORS", Sectors), ("REJECT", Array.Empty<byte>()), ("BLOCKMAP", Array.Empty<byte>()),
    };

    /// <summary>Writes the map as a WAD file.</summary>
    public string Write(string path, string mapname = "MAP01", bool withnodes = true)
    {
        using (var wad = new CodeImp.DoomBuilder.IO.WAD(path))
        {
            int index = 0;
            foreach (var (name, data) in Lumps(mapname, withnodes))
            {
                var lump = wad.Insert(name, index++, data.Length);
                lump.Stream.Write(data, 0, data.Length);
            }
            wad.WriteHeaders();
        }
        return path;
    }
}
