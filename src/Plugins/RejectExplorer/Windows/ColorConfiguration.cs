// "Color Configuration": the five colors of the reject explorer mode. UDB's ColorConfiguration as a modal Avalonia dialog.
using System;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Rendering;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.RejectExplorer
{
	public class ColorConfiguration : IDisposable, IWin32Window
	{
		private readonly ColorField defaultcolor = new ColorField();
		private readonly ColorField highlightcolor = new ColorField();
		private readonly ColorField bidirectionalcolor = new ColorField();
		private readonly ColorField unidirectionalfromcolor = new ColorField();
		private readonly ColorField unidirectionaltocolor = new ColorField();
		private readonly Avalonia.Controls.Button resetcolors = new Avalonia.Controls.Button { Content = "Reset colors" };
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal ColorField DefaultColor { get { return defaultcolor; } }
		internal ColorField HighlightColor { get { return highlightcolor; } }
		internal ColorField BidirectionalColor { get { return bidirectionalcolor; } }
		internal ColorField UnidirectionalFromColor { get { return unidirectionalfromcolor; } }
		internal ColorField UnidirectionalToColor { get { return unidirectionaltocolor; } }
		internal Avalonia.Controls.Button ResetColors { get { return resetcolors; } }
		internal SimpleDialog Dialog { get { return dialog; } }

		public ColorConfiguration()
		{
			defaultcolor.Color = PixelColor.FromInt(BuilderPlug.Me.ColorSettings.Default);
			highlightcolor.Color = PixelColor.FromInt(BuilderPlug.Me.ColorSettings.Highlight);
			bidirectionalcolor.Color = PixelColor.FromInt(BuilderPlug.Me.ColorSettings.Bidirectional);
			unidirectionalfromcolor.Color = PixelColor.FromInt(BuilderPlug.Me.ColorSettings.UnidirectionalFrom);
			unidirectionaltocolor.Color = PixelColor.FromInt(BuilderPlug.Me.ColorSettings.UnidirectionalTo);
			resetcolors.Click += resetcolors_Click;
		}

		private bool Apply()
		{
			BuilderPlug.Me.ColorSettings = new ColorSettings
			{
				Default = defaultcolor.Color.ToInt(),
				Highlight = highlightcolor.Color.ToInt(),
				Bidirectional = bidirectionalcolor.Color.ToInt(),
				UnidirectionalFrom = unidirectionalfromcolor.Color.ToInt(),
				UnidirectionalTo = unidirectionaltocolor.Color.ToInt()
			};

			General.Settings.WritePluginSetting("colors.default", defaultcolor.Color.ToInt());
			General.Settings.WritePluginSetting("colors.highlight", highlightcolor.Color.ToInt());
			General.Settings.WritePluginSetting("colors.bidirectional", bidirectionalcolor.Color.ToInt());
			General.Settings.WritePluginSetting("colors.unidirectionalfrom", unidirectionalfromcolor.Color.ToInt());
			General.Settings.WritePluginSetting("colors.unidirectionalto", unidirectionaltocolor.Color.ToInt());
			return true;
		}

		private void resetcolors_Click(object sender, EventArgs e)
		{
			defaultcolor.Color = PixelColor.FromInt(BuilderPlug.Me.DefaultColorSettings.Default);
			highlightcolor.Color = PixelColor.FromInt(BuilderPlug.Me.DefaultColorSettings.Highlight);
			bidirectionalcolor.Color = PixelColor.FromInt(BuilderPlug.Me.DefaultColorSettings.Bidirectional);
			unidirectionalfromcolor.Color = PixelColor.FromInt(BuilderPlug.Me.DefaultColorSettings.UnidirectionalFrom);
			unidirectionaltocolor.Color = PixelColor.FromInt(BuilderPlug.Me.DefaultColorSettings.UnidirectionalTo);
		}

		public DialogResult ShowDialog()
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowSpacing = 6, ColumnSpacing = 10 };
			var rows = new (string, ColorField)[]
			{
				("Default color:", defaultcolor), ("Highlight color:", highlightcolor), ("Bidirectional color:", bidirectionalcolor),
				("Unidirectional from color:", unidirectionalfromcolor), ("Unidirectional to color:", unidirectionaltocolor),
			};
			for(int i = 0; i < rows.Length; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				var label = new TextBlock { Text = rows[i].Item1, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
				Grid.SetRow(label, i);
				Grid.SetRow(rows[i].Item2, i);
				Grid.SetColumn(rows[i].Item2, 1);
				grid.Children.Add(label);
				grid.Children.Add(rows[i].Item2);
			}

			dialog = new SimpleDialog("Color Configuration", grid);
			dialog.Validate = Apply;
			dialog.ExtraButtons.Children.Add(resetcolors);
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
