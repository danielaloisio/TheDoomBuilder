using System.Linq;
using CodeImp.DoomBuilder.Rendering;
using DoomBuilder.Rendering;
using Xunit;

namespace DoomBuilder.Rendering.Tests;

public class VertexArenaTests
{
    private const int Flat = FlatVertex.Stride;

    [Fact]
    public void Allocate_appends_ranges_and_tracks_vertex_indices()
    {
        var arena = new VertexArena(VertexFormat.Flat, Flat * 100);
        var a = arena.Allocate(Flat * 10, "a");
        var b = arena.Allocate(Flat * 5, "b");

        Assert.Equal(0, a.Offset);
        Assert.Equal(0, a.StartIndex);
        Assert.Equal(Flat * 10, b.Offset);
        Assert.Equal(10, b.StartIndex);
        Assert.Equal(Flat * 15, arena.NextPos);
    }

    [Fact]
    public void HasRoom_is_false_when_full_and_Allocate_throws()
    {
        var arena = new VertexArena(VertexFormat.Flat, Flat * 10);
        arena.Allocate(Flat * 10, null);
        Assert.False(arena.HasRoom(1));
        Assert.Throws<System.InvalidOperationException>(() => arena.Allocate(1, null));
    }

    [Fact]
    public void Compact_packs_live_ranges_and_describes_the_gpu_copies()
    {
        var arena = new VertexArena(VertexFormat.Flat, Flat * 10);
        var a = arena.Allocate(Flat * 2, "a");
        var b = arena.Allocate(Flat * 3, "b");
        var c = arena.Allocate(Flat * 4, "c");
        arena.Free(b);   // hole in the middle

        var next = arena.Compact(Flat * 2, out var copies);

        // a stays, c moves down over the hole
        Assert.Equal(0, a.Offset);
        Assert.Equal(Flat * 2, c.Offset);
        Assert.Equal(2, c.StartIndex);
        Assert.Equal(Flat * 6, next.NextPos);
        Assert.Equal(2, copies.Count);
        Assert.Equal(new VertexArena.CopyRun(0, 0, Flat * 2), copies[0]);
        Assert.Equal(new VertexArena.CopyRun(Flat * 5, Flat * 2, Flat * 4), copies[1]);
        Assert.Equal(new[] { "a", "c" }, next.Ranges.Select(r => (string)r.Tag));
        Assert.Empty(arena.Ranges);   // ranges were handed over
    }

    [Fact]
    public void Compact_merges_adjacent_ranges_into_one_copy()
    {
        var arena = new VertexArena(VertexFormat.World, WorldVertex.Stride * 10);
        arena.Allocate(WorldVertex.Stride * 3, null);
        arena.Allocate(WorldVertex.Stride * 3, null);

        arena.Compact(1, out var copies);

        Assert.Single(copies);
        Assert.Equal(WorldVertex.Stride * 6, copies[0].Size);
    }

    [Theory]
    [InlineData(2, 1)]    // mostly empty after GC: keep the size
    [InlineData(100, 1)]  // too full: must at least double what is needed
    public void Compact_grows_the_buffer_only_when_it_was_more_than_half_full(int liveUnits, int requestedUnits)
    {
        var arena = new VertexArena(VertexFormat.Flat, Flat * 100);
        arena.Allocate(Flat * liveUnits, null);
        int total = Flat * (liveUnits + requestedUnits);

        var next = arena.Compact(Flat * requestedUnits, out _);

        Assert.True(next.Size >= total);
        if (liveUnits + requestedUnits <= 50) Assert.Equal(Flat * 100, next.Size);
        else Assert.True(next.Size >= total * 2 || next.Size >= Flat * 100 * 2);
    }
}
