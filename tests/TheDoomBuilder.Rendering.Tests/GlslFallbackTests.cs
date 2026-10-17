using System.Collections.Generic;
using TheDoomBuilder.Rendering;
using Xunit;

namespace TheDoomBuilder.Rendering.Tests;

/// <summary>A 3.2 core context (macOS) only has GLSL 1.50: the shaders must fall back to it (no GPU needed to test the text).</summary>
public class GlslFallbackTests
{
    [Theory]
    [InlineData("4.10", true)]
    [InlineData("4.60 NVIDIA", true)]
    [InlineData("3.30", true)]
    [InlineData("3.30 - Build 26.20", true)]
    [InlineData("1.50", false)]
    [InlineData("1.50 NVIDIA via Cg", false)]
    [InlineData("3.20", false)]
    [InlineData("", true)]
    public void The_glsl_version_decides_if_330_can_be_used(string version, bool expected)
    {
        Assert.Equal(expected, GlShader.SupportsGlsl330(version));
    }

    [Fact]
    public void Input_locations_are_removed_and_reported_for_binding_by_name()
    {
        string source = "layout(location = 0) in vec3 _SC_Bin_FPosition;\n#line 4\nlayout(location=2) in vec2 _SC_Bin_FTextureCoordinate;\nout vec4 x;\nvoid main(){}";
        var inputs = new List<KeyValuePair<uint, string>>();
        string result = GlShader.StripInputLocations(source, inputs);

        Assert.DoesNotContain("layout", result);
        Assert.Contains("in vec3 _SC_Bin_FPosition;", result);
        Assert.Contains("in vec2 _SC_Bin_FTextureCoordinate;", result);
        Assert.Equal(2, inputs.Count);
        Assert.Equal(new KeyValuePair<uint, string>(0, "_SC_Bin_FPosition"), inputs[0]);
        Assert.Equal(new KeyValuePair<uint, string>(2, "_SC_Bin_FTextureCoordinate"), inputs[1]);
    }
}
