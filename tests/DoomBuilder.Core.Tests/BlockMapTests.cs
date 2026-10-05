using System.Drawing;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class BlockMapTests
{
    [Fact]
    public void An_empty_area_gives_a_single_block_instead_of_overflowing()
    {
        // MapSet.CreateArea of no vertices: left/top = float.MaxValue, right/bottom = float.MinValue
        var map = new BlockMap<BlockEntry>(new RectangleF(float.MaxValue, float.MaxValue, float.MinValue - float.MaxValue, float.MinValue - float.MaxValue));

        Assert.Equal(1, map.Size.Width);
        Assert.Equal(1, map.Size.Height);
    }

    [Fact]
    public void A_normal_area_is_covered_by_whole_blocks()
    {
        var map = new BlockMap<BlockEntry>(new RectangleF(-100, -100, 400, 300));

        Assert.True(map.Size.Width >= 4);
        Assert.True(map.Size.Height >= 3);
    }
}
