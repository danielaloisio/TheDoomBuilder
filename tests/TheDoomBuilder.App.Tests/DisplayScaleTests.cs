using System.Drawing;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>On a screen with a scale above 100% the display (drawn in device pixels) keeps its look: vertices, line normals and text labels grow with it.</summary>
public class DisplayScaleTests : EditorTestBase
{
    [AvaloniaFact]
    public void Vertices_and_text_grow_with_the_display_scale()
    {
        OpenEditor();
        SizeF original = MainForm.DPIScaler;
        try
        {
            var renderer = (Renderer2D)General.Map.Renderer2D;
            MainForm.DPIScaler = new SizeF(1f, 1f);
            renderer.ScaleView(8f);                                  // zoomed in: the vertices are at their biggest
            int normal = renderer.VertexSize;
            float labelsize = TextLabel.ScaledFontSize();

            MainForm.DPIScaler = new SizeF(2f, 2f);
            renderer.ScaleView(8f + 0.001f);                         // the transformations are redone
            Assert.True(renderer.VertexSize > normal, "vertices: " + normal + " -> " + renderer.VertexSize);
            Assert.Equal(labelsize * 2f, TextLabel.ScaledFontSize());
        }
        finally { MainForm.DPIScaler = original; }
    }

    [AvaloniaFact]
    public void A_change_of_the_display_scale_is_announced()
    {
        SizeF original = MainForm.DPIScaler;
        int raised = 0;
        void Count() { raised++; }
        MainForm.DPIScalerChanged += Count;
        try
        {
            MainForm.DPIScaler = new SizeF(original.Width + 1f, original.Height + 1f);
            MainForm.DPIScaler = MainForm.DPIScaler;                // the same value: nothing to announce
            Assert.Equal(1, raised);
        }
        finally { MainForm.DPIScalerChanged -= Count; MainForm.DPIScaler = original; }
    }

    [AvaloniaFact]
    public void Plotter_lines_and_the_fixed_thing_size_follow_the_display_scale()
    {
        OpenEditor();
        SizeF original = MainForm.DPIScaler;
        try
        {
            var renderer = (Renderer2D)General.Map.Renderer2D;
            MainForm.DPIScaler = new SizeF(1f, 1f);
            renderer.ScaleView(2f);
            float fixedsize = renderer.FixedThingSize;
            MainForm.DPIScaler = new SizeF(2f, 2f);
            renderer.ScaleView(2f + 0.001f);
            Assert.Equal(fixedsize * 2f, renderer.FixedThingSize);
        }
        finally { MainForm.DPIScaler = original; }

        // A line of the plotter is as many pixels wide as the thickness says
        var plotter = new Plotter(64, 64);
        var color = new PixelColor(255, 255, 255, 255);
        plotter.DrawLineSolid(10, 10, 40, 10, ref color);
        int thin = CountPainted(plotter);
        plotter.Clear();
        plotter.Thickness = 2;
        plotter.DrawLineSolid(10, 10, 40, 10, ref color);
        Assert.Equal(thin * 2, CountPainted(plotter));
        plotter.Dispose();
    }

    private static int CountPainted(Plotter plotter)
    {
        int count = 0;
        for(int y = 0; y < plotter.Height; y++)
            for(int x = 0; x < plotter.Width; x++)
            {
                PixelColor c = plotter.GetPixel(x, y);
                if(c.a != 0) count++;
            }
        return count;
    }
}
