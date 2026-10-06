// "Color Configuration": the five colors of the sound modes. UDB's ColorConfiguration as a modal Avalonia dialog.
using System;
using System.Windows.Forms;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Rendering;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.SoundPropagationMode
{
	public class ColorConfiguration : IDisposable, IWin32Window
	{
		private readonly ColorField highlightcolor = new ColorField();
		private readonly ColorField level1color = new ColorField();
		private readonly ColorField level2color = new ColorField();
		private readonly ColorField nosoundcolor = new ColorField();
		private readonly ColorField blocksoundcolor = new ColorField();
		private readonly Avalonia.Controls.Button resetcolors = new Avalonia.Controls.Button { Content = "Reset colors" };
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal ColorField HighlightColor { get { return highlightcolor; } }
		internal ColorField Level1Color { get { return level1color; } }
		internal ColorField Level2Color { get { return level2color; } }
		internal ColorField NoSoundColor { get { return nosoundcolor; } }
		internal ColorField BlockSoundColor { get { return blocksoundcolor; } }
		internal Avalonia.Controls.Button ResetColors { get { return resetcolors; } }

		public ColorConfiguration()
		{
			highlightcolor.Color = BuilderPlug.Me.HighlightColor;
			level1color.Color = BuilderPlug.Me.Level1Color;
			level2color.Color = BuilderPlug.Me.Level2Color;
			nosoundcolor.Color = BuilderPlug.Me.NoSoundColor;
			blocksoundcolor.Color = BuilderPlug.Me.BlockSoundColor;
			resetcolors.Click += resetcolors_Click;
		}

		private bool Apply()
		{
			BuilderPlug.Me.HighlightColor = highlightcolor.Color;
			BuilderPlug.Me.Level1Color = level1color.Color;
			BuilderPlug.Me.Level2Color = level2color.Color;
			BuilderPlug.Me.NoSoundColor = nosoundcolor.Color;
			BuilderPlug.Me.BlockSoundColor = blocksoundcolor.Color;

			General.Settings.WritePluginSetting("highlightcolor", highlightcolor.Color.ToInt());
			General.Settings.WritePluginSetting("level1color", level1color.Color.ToInt());
			General.Settings.WritePluginSetting("level2color", level2color.Color.ToInt());
			General.Settings.WritePluginSetting("nosoundcolor", nosoundcolor.Color.ToInt());
			General.Settings.WritePluginSetting("blocksoundcolor", blocksoundcolor.Color.ToInt());
			return true;
		}

		private void resetcolors_Click(object sender, EventArgs e)
		{
			highlightcolor.Color = new PixelColor(255, 0, 192, 0);
			level1color.Color = new PixelColor(255, 0, 255, 0);
			level2color.Color = new PixelColor(255, 255, 255, 0);
			nosoundcolor.Color = new PixelColor(255, 160, 160, 160);
			blocksoundcolor.Color = new PixelColor(255, 255, 0, 0);
		}

		public DialogResult ShowDialog()
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowSpacing = 6, ColumnSpacing = 10 };
			var rows = new (string, ColorField)[]
			{
				("Highlight color:", highlightcolor), ("Level 1 color:", level1color), ("Level 2 color:", level2color),
				("No sound color:", nosoundcolor), ("Block sound color:", blocksoundcolor),
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
