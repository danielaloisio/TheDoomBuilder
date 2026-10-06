// "Visplane Explorer - Set custom height": asks for the height above the floor the views are taken from. UDB's SetCustomHeightDialog as a
// modal Avalonia dialog. A blank or too large value means "the default".
using System;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.Plugins.VisplaneExplorer.Windows
{
	public class SetCustomHeightDialog : IDisposable, IWin32Window
	{
		#region ================== Variables

		private int customheight;
		private NumberBox input = new NumberBox { AllowNegative = false, MinWidth = 100 };
		private SimpleDialog dialog;

		#endregion

		#region ================== Properties

		public IntPtr Handle { get { return IntPtr.Zero; } }
		public int CustomHeight { get { return customheight; } set { customheight = value; } }

		// For the tests
		internal NumberBox Input { get { return input; } }
		internal SimpleDialog Dialog { get { return dialog; } }

		#endregion

		// Redraw the display using a user-entered view height. Blank input resets to the default.
		private bool Apply()
		{
			customheight = input.GetResult(0);
			if(customheight > 32767) customheight = 0;
			return true;
		}

		public DialogResult ShowDialog()
		{
			// (the dialog can be shown many times: its controls are made again, a control cannot be in two windows)
			input = new NumberBox { AllowNegative = false, MinWidth = 100 };
			input.Text = customheight.ToString();

			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, MinWidth = 220 };
			row.Children.Add(new TextBlock { Text = "View height:", VerticalAlignment = VerticalAlignment.Center });
			row.Children.Add(input);

			dialog = new SimpleDialog("Visplane Explorer - Set custom height", row);
			dialog.Validate = Apply;

			// Auto-focus on the view height input when the dialog pops up
			dialog.Opened += (s, e) => { input.Focus(); input.SelectAll(); };
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
