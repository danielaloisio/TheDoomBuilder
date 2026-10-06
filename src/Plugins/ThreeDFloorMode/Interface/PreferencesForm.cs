// The plugin's tab ("3D Floor Plugin") of the program preferences: when the sector labels and the slope vertex labels show in the slope
// mode. Same settings and members as UDB's form; the controls are Avalonia, handed to the preferences window as the native control of a tab.
using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Windows;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class PreferencesForm : IDisposable
	{
		private static readonly string[] Options = { "Always", "Never", "On slope vertex highlight" };

		private readonly ComboBox sectorlabels = new ComboBox { ItemsSource = Options, MinWidth = 220 };
		private readonly ComboBox slopevertexlabels = new ComboBox { ItemsSource = Options, MinWidth = 220 };

		public PreferencesForm()
		{
			sectorlabels.SelectedIndex = (int)BuilderPlug.Me.SectorLabelDisplayOption;
			slopevertexlabels.SelectedIndex = (int)BuilderPlug.Me.SlopeVertexLabelDisplayOption;
		}

		// For the tests
		internal ComboBox SectorLabels { get { return sectorlabels; } }
		internal ComboBox SlopeVertexLabels { get { return slopevertexlabels; } }

		private Control Build()
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(6) };
			void Row(int row, string label, Control box)
			{
				var text = new TextBlock { Text = label, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
				Grid.SetRow(text, row);
				Grid.SetRow(box, row);
				Grid.SetColumn(box, 1);
				box.Margin = new Thickness(0, 4);
				grid.Children.Add(text);
				grid.Children.Add(box);
			}
			Row(0, "Show sector labels:", sectorlabels);
			Row(1, "Show slope vertex labels:", slopevertexlabels);

			var panel = new StackPanel { Margin = new Thickness(8), Spacing = 4 };
			panel.Children.Add(new TextBlock { Text = "Slope Mode", FontWeight = Avalonia.Media.FontWeight.SemiBold });
			panel.Children.Add(grid);
			panel.Children.Add(new TextBlock { Text = "Holding the Alt key will always show the labels", Opacity = 0.7 });
			return new ScrollViewer { Content = panel };
		}

		#region ================== Methods

		// When OK is pressed on the preferences dialog
		// Prevent inlining, otherwise there are unexpected interactions with Assembly.GetCallingAssembly
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void OnAccept(PreferencesController controller)
		{
			// Write preferred settings
			General.Settings.WritePluginSetting("sectorlabeldisplayoption", sectorlabels.SelectedIndex);
			General.Settings.WritePluginSetting("slopevertexlabeldisplayoption", slopevertexlabels.SelectedIndex);
		}

		// This sets up the form with the preferences controller
		public void Setup(PreferencesController controller)
		{
			controller.AddTab(new System.Windows.Forms.TabPage { Text = "3D Floor Plugin", NativeControl = Build() });
			controller.OnAccept += OnAccept;
		}

		public void Dispose() { }

		#endregion
	}
}
