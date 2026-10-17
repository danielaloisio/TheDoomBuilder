using System;
using System.IO;
using CodeImp.DoomBuilder.Plugins.VisplaneExplorer.Vpo;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The C# port of the Visplane Overflow library (what UDB's native BuilderNative did): Doom's renderer, counting.</summary>
public class VpoTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-vpo-" + Guid.NewGuid().ToString("N"));

    public VpoTests() { Directory.CreateDirectory(dir); }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    private VpoContext Open(RowMap map, bool open = false, string name = "MAP01")
    {
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".wad");
        map.Build().Write(path, name);
        var context = new VpoContext();
        Assert.True(context.LoadWad(path), context.Error);
        bool hexen = true;
        Assert.True(context.OpenMap(name, ref hexen), context.Error);
        Assert.False(hexen);
        if (open) context.OpenDoorSectors(1);
        return context;
    }

    private static (int result, int visplanes, int drawsegs, int openings, int solidsegs) Spot(VpoContext c, int x, int y, int dz, int angle)
    {
        int vp = 0, ds = 0, op = 0, ss = 0;
        int result = c.TestSpot(x, y, dz, angle, ref vp, ref ds, ref op, ref ss);
        return (result, vp, ds, op, ss);
    }

    [Fact]
    public void A_plain_room_needs_a_floor_and_a_ceiling_and_a_few_walls()
    {
        var c = Open(new RowMap(new RowMap.Room { Width = 256 }));
        var r = Spot(c, 128, 64, 41, 0);
        Assert.Equal(VpoContext.RESULT_OK, r.result);
        Assert.Equal(2, r.visplanes);                                    // the floor and the ceiling
        Assert.InRange(r.drawsegs, 1, 4);
        Assert.Equal(0, r.openings);                                     // no two-sided lines
        Assert.True(r.solidsegs >= 2);

        // Every direction sees at least one wall, and only the two planes
        foreach (int angle in new[] { 0, 90, 180, 270, 45, 135, 225, 315 })
        {
            var a = Spot(c, 128, 64, 41, angle);
            Assert.Equal(VpoContext.RESULT_OK, a.result);
            Assert.Equal(2, a.visplanes);
            Assert.True(a.drawsegs >= 1);
        }
    }

    [Fact]
    public void Spots_outside_of_the_map_or_not_inside_the_sector_are_refused()
    {
        var c = Open(new RowMap(new RowMap.Room { Width = 256 }));
        Assert.Equal(VpoContext.RESULT_IN_VOID, Spot(c, 1000, 64, 41, 0).result);       // outside of the map
        Assert.Equal(VpoContext.RESULT_IN_VOID, Spot(c, -50, 64, 41, 0).result);
        Assert.Equal(VpoContext.RESULT_BAD_Z, Spot(c, 128, 64, 200, 0).result);         // above the ceiling
        Assert.Equal(VpoContext.RESULT_BAD_Z, Spot(c, 128, 64, 0, 0).result);           // on the floor
        Assert.Equal(VpoContext.RESULT_OK, Spot(c, 128, 64, -20, 0).result);            // a negative height is measured from the ceiling
        Assert.Equal(VpoContext.RESULT_OK, Spot(c, 128, 64, 41, 360).result);           // 360 is 0
    }

    [Fact]
    public void The_counters_only_grow()
    {
        var c = Open(new RowMap(new RowMap.Room { Width = 256 }));
        int vp = 50, ds = 60, op = 70, ss = 80;
        Assert.Equal(VpoContext.RESULT_OK, c.TestSpot(128, 64, 41, 0, ref vp, ref ds, ref op, ref ss));
        Assert.Equal((50, 60, 70, 80), (vp, ds, op, ss));              // what the view needs is less than what was there already
    }

    [Fact]
    public void A_step_in_the_floor_or_the_ceiling_makes_more_planes_and_a_two_sided_line_makes_openings()
    {
        // Three rooms in a row; the middle one is lower: two more planes (its floor and its ceiling are at other heights)
        var flat = Open(new RowMap(new RowMap.Room(), new RowMap.Room(), new RowMap.Room()));
        var step = Open(new RowMap(new RowMap.Room(), new RowMap.Room { Floor = -32, Ceiling = 96 }, new RowMap.Room()));

        var rf = Spot(flat, 64, 64, 41, 0);
        var rs = Spot(step, 64, 64, 41, 0);
        Assert.Equal(VpoContext.RESULT_OK, rf.result);
        Assert.Equal(2, rf.visplanes);                                   // all the floors are at the same height: one plane. The same for the ceilings
        Assert.Equal(VpoContext.RESULT_OK, rs.result);
        Assert.True(rs.visplanes > rf.visplanes, "the steps need more planes: " + rs.visplanes);
        Assert.True(rs.drawsegs > rf.drawsegs, "and more wall pieces");
    }

    [Fact]
    public void A_closed_door_hides_the_room_behind_it_and_an_open_one_does_not()
    {
        // A, the door (as high as its floor: closed) and B; the lines on the sides of the door are manual doors
        RowMap Make() => new RowMap(new RowMap.Room(), new RowMap.Room { Width = 32, Floor = 0, Ceiling = 0 }, new RowMap.Room { Width = 128 })
        {
            BorderSpecials = { [1] = 1, [2] = 1 },
        };

        var closed = Open(Make(), open: false);
        var opened = Open(Make(), open: true);
        var rc = Spot(closed, 64, 64, 41, 0);
        var ro = Spot(opened, 64, 64, 41, 0);
        Assert.Equal(VpoContext.RESULT_OK, rc.result);
        Assert.Equal(VpoContext.RESULT_OK, ro.result);
        Assert.True(ro.drawsegs > rc.drawsegs, "through the open door the walls of room B show: " + rc.drawsegs + " / " + ro.drawsegs);
        Assert.True(ro.visplanes >= rc.visplanes);

        // A spot in the door sector itself is only possible when it is open
        Assert.Equal(VpoContext.RESULT_BAD_Z, Spot(closed, 144, 64, 41, 0).result);
        Assert.Equal(VpoContext.RESULT_OK, Spot(opened, 144, 64, 41, 0).result);
    }

    [Fact]
    public void Many_planes_overflow_the_limit_of_the_library()
    {
        // 600 narrow rooms in a row, each with another ceiling height: planes at the same height are merged, so it takes that many different
        // heights to pass the limit of 512 planes (4 times Doom's own)
        var rooms = new RowMap.Room[600];
        for (int i = 0; i < rooms.Length; i++) rooms[i] = new RowMap.Room { Width = 4, Floor = 0, Ceiling = 129 + i % 16000, Light = 192 };
        var c = Open(new RowMap(rooms));
        var r = Spot(c, 2, 64, 41, 0);
        Assert.Equal(VpoContext.RESULT_OVERFLOW, r.result);
    }

    [Fact]
    public void Files_that_are_not_wads_and_maps_that_are_not_there_are_reported()
    {
        var c = new VpoContext();
        string text = Path.Combine(dir, "text.wad");
        File.WriteAllText(text, "this is not a wad file at all");
        Assert.False(c.LoadWad(text));
        Assert.Contains("invalid wad", c.Error);
        Assert.False(c.LoadWad(Path.Combine(dir, "missing.wad")));

        string path = Path.Combine(dir, "room.wad");
        new RowMap(new RowMap.Room()).Build().Write(path);
        Assert.True(c.LoadWad(path));
        bool hexen = false;
        Assert.False(c.OpenMap("MAP02", ref hexen));
        Assert.Contains("No such map", c.Error);

        // A map without nodes cannot be rendered
        string nonodes = Path.Combine(dir, "nonodes.wad");
        new RowMap(new RowMap.Room()).Build().Write(nonodes, "MAP01", withnodes: false);
        Assert.True(c.LoadWad(nonodes));
        Assert.False(c.OpenMap("MAP01", ref hexen));
        Assert.Contains("Missing nodes", c.Error);
    }
}
