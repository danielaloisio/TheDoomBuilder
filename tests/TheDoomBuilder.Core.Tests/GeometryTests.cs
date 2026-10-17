using System;
using CodeImp.DoomBuilder.Geometry;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void Line2D_length_is_euclidean()
    {
        var line = new Line2D(new Vector2D(0, 0), new Vector2D(3, 4));
        Assert.Equal(5.0, line.GetLength(), 9);
        Assert.Equal(25.0, line.GetLengthSq(), 9);
    }

    [Fact]
    public void Crossing_lines_intersect_and_parallel_lines_do_not()
    {
        Assert.True(Line2D.GetIntersection(new Line2D(0, 0, new Vector2D(10, 10)), new Line2D(0, 10, new Vector2D(10, 0))));
        Assert.False(Line2D.GetIntersection(new Line2D(0, 0, new Vector2D(10, 0)), new Line2D(0, 5, new Vector2D(10, 5))));
    }

    [Fact]
    public void Distance_to_line_respects_bounds()
    {
        var line = new Line2D(new Vector2D(0, 0), new Vector2D(10, 0));
        Assert.Equal(3.0, line.GetDistanceToLine(new Vector2D(5, 3), true), 9);
        Assert.Equal(5.0, line.GetDistanceToLine(new Vector2D(15, 0), true), 9);   // clamped to the end point
        Assert.Equal(0.0, line.GetDistanceToLine(new Vector2D(15, 0), false), 9);  // infinite line
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(359)]
    public void Doom_angle_round_trips_through_real_angle(int doomangle)
        => Assert.Equal(doomangle, Angle2D.RealToDoom(Angle2D.DoomToReal(doomangle)));
}
