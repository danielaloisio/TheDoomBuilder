// The controls of the window (what UDB's designer made).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public partial class ObjImportSettingsForm
	{
		private readonly FxText tbImportPath = new FxText();
		private readonly FxButton browse = new FxButton("...");
		private readonly FxNumber nudScale = new FxNumber(-2048, 2048, 1, 4, 0.1m);
		private readonly FxRadio axisx = new FxRadio("X", "upaxis");
		private readonly FxRadio axisy = new FxRadio("Y", "upaxis");
		private readonly FxRadio axisz = new FxRadio("Z", "upaxis");
		private readonly FxCheck cbusevertexheight = new FxCheck("Sloped Terrain");
		private readonly FxButton import = new FxButton("Import");
		private readonly FxButton cancel = new FxButton("Cancel");
		private readonly System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog { Filter = "Wavefront obj files|*.obj", Title = "Choose .obj file to import:" };

		// For the tests
		internal Avalonia.Controls.TextBox PathBox { get { return tbImportPath.View; } }
		internal FxNumber ScaleBox { get { return nudScale; } }
		internal FxRadio AxisX { get { return axisx; } }
		internal FxRadio AxisY { get { return axisy; } }
		internal FxRadio AxisZ { get { return axisz; } }
		internal FxCheck SlopedTerrain { get { return cbusevertexheight; } }
		internal FxButton BrowseButton { get { return browse; } }
		internal FxButton ImportButton { get { return import; } }
		internal FxButton CancelButton { get { return cancel; } }

		private static StackPanel Row(params Control[] controls)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			foreach(Control c in controls) row.Children.Add(c);
			return row;
		}

		private void BuildView()
		{
			Title = "Import Wavefront .obj as terrain";
			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
			layout.Children.Add(Row(new TextBlock { Text = "Path:", VerticalAlignment = VerticalAlignment.Center }, tbImportPath.View, browse.View));
			layout.Children.Add(Row(new TextBlock { Text = "Scale:", VerticalAlignment = VerticalAlignment.Center }, nudScale.View));
			layout.Children.Add(Row(new TextBlock { Text = "Up axis:", VerticalAlignment = VerticalAlignment.Center }, axisx.View, axisy.View, axisz.View));
			layout.Children.Add(cbusevertexheight.View);
			layout.Children.Add(ButtonRow(import, cancel));
			Content = layout;

			browse.Click += browse_Click;
			import.Click += import_Click;
			cancel.Click += cancel_Click;
		}
	}
}
