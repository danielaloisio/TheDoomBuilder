// The window that shows an error of a script: its message and JavaScript stack trace, and the internal (C#) stack trace. UDB's UDBScriptErrorForm.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class UDBScriptErrorForm : Window
	{
		private readonly TextBox tbStackTrace = Box();
		private readonly TextBox tbInternalStackTrace = Box();
		private readonly TabControl tabControl1 = new TabControl();

		// For the tests
		internal TextBox StackTrace { get { return tbStackTrace; } }
		internal TextBox InternalStackTrace { get { return tbInternalStackTrace; } }
		internal TabControl Tabs { get { return tabControl1; } }

		private static TextBox Box() { return new TextBox { IsReadOnly = true, AcceptsReturn = true, MinWidth = 500, MinHeight = 150, FontFamily = new Avalonia.Media.FontFamily("monospace") }; }

		public UDBScriptErrorForm(string message, string stacktrace, string internalstacktrace)
		{
			Title = "Script Error";
			SizeToContent = SizeToContent.WidthAndHeight;
			CanResize = true;
			ShowInTaskbar = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			tbStackTrace.Text = message + "\r\n" + stacktrace;
			tbInternalStackTrace.Text = internalstacktrace;
			tabControl1.Items.Add(new TabItem { Header = "JavaScript stack trace", Content = tbStackTrace });
			tabControl1.Items.Add(new TabItem { Header = "Internal stack trace", Content = tbInternalStackTrace });
			if(string.IsNullOrWhiteSpace(stacktrace)) tabControl1.SelectedIndex = 1;

			var ok = new Avalonia.Controls.Button { Content = "OK", MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true, IsCancel = true, Margin = new Thickness(0, 8, 0, 0) };
			ok.Click += (s, e) => Close();
			var layout = new DockPanel { Margin = new Thickness(12) };
			DockPanel.SetDock(ok, Dock.Bottom);
			layout.Children.Add(ok);
			layout.Children.Add(new TextBlock { Text = "There was an error while executing the script:", Margin = new Thickness(0, 0, 0, 6) });
			DockPanel.SetDock(layout.Children[1], Dock.Top);
			layout.Children.Add(tabControl1);
			Content = layout;
		}

		public System.Windows.Forms.DialogResult ShowDialog()
		{
			DialogHost.ShowModal(this);
			return System.Windows.Forms.DialogResult.OK;
		}
	}
}
