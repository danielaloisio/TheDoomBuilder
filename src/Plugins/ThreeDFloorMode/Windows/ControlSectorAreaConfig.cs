// "Control Sector Area Configuration": the tag range the 3D floor tool uses for its control sectors. UDB's ControlSectorAreaConfig as a
// modal Avalonia dialog with the same constructor and ShowDialog.
using System;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;
using AvCheckBox = Avalonia.Controls.CheckBox;
using Control = Avalonia.Controls.Control;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class ControlSectorAreaConfig : IDisposable, IWin32Window
	{
		private readonly ControlSectorArea csa;
		private readonly AvCheckBox useTagRange = new AvCheckBox { Content = "Use tag range" };
		private readonly NumberBox firstTag = new NumberBox { MinWidth = 100 };
		private readonly NumberBox lastTag = new NumberBox { MinWidth = 100 };
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal AvCheckBox UseTagRangeBox { get { return useTagRange; } }
		internal NumberBox FirstTagBox { get { return firstTag; } }
		internal NumberBox LastTagBox { get { return lastTag; } }

		public ControlSectorAreaConfig(ControlSectorArea csa)
		{
			this.csa = csa;
			useTagRange.IsChecked = csa.UseCustomTagRnage;
			firstTag.Text = csa.FirstTag.ToString();
			lastTag.Text = csa.LastTag.ToString();
			UpdateEnabled();
			useTagRange.IsCheckedChanged += (s, e) => UpdateEnabled();
		}

		private void UpdateEnabled()
		{
			firstTag.IsEnabled = lastTag.IsEnabled = useTagRange.IsChecked == true;
		}

		private bool Accept()
		{
			bool use = useTagRange.IsChecked == true;
			int first = firstTag.GetResult(csa.FirstTag);
			int last = lastTag.GetResult(csa.LastTag);

			if(use && last < first)
			{
				MessageBox.Show("Last tag of range must be bigger than first tag of range", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			csa.UseCustomTagRnage = use;
			if(use)
			{
				csa.FirstTag = first;
				csa.LastTag = last;
			}
			return true;
		}

		public DialogResult ShowDialog()
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0, 6, 0, 0) };
			void Row(int row, string label, Control box)
			{
				var text = new TextBlock { Text = label, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
				Grid.SetRow(text, row);
				Grid.SetRow(box, row);
				Grid.SetColumn(box, 1);
				box.Margin = new Thickness(0, 3);
				grid.Children.Add(text);
				grid.Children.Add(box);
			}
			Row(0, "First", firstTag);
			Row(1, "Last", lastTag);

			var panel = new StackPanel { MinWidth = 260 };
			panel.Children.Add(useTagRange);
			panel.Children.Add(grid);

			dialog = new SimpleDialog("Control Sector Area Configuration", panel) { };
			dialog.Validate = Accept;
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
