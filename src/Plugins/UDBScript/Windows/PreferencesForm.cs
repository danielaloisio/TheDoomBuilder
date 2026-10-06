// The "UDBScript" tab of the program preferences: the external editor that opens the scripts. UDB's PreferencesForm; the controls are Avalonia,
// handed to the preferences window as the native control of a tab.
using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Windows;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class PreferencesForm : IDisposable
	{
		private readonly TextBox exepath = new TextBox { MinWidth = 320 };
		private readonly AvButton btnSelectExe = new AvButton { Content = "..." };

		// For the tests
		internal TextBox ExePath { get { return exepath; } }
		internal AvButton SelectExeButton { get { return btnSelectExe; } }

		public PreferencesForm()
		{
			exepath.Text = BuilderPlug.Me.EditorExePath;
			btnSelectExe.Click += btnSelectExe_Click;
		}

		private Control Build()
		{
			var row = new DockPanel();
			DockPanel.SetDock(btnSelectExe, Dock.Right);
			btnSelectExe.Margin = new Thickness(4, 0, 0, 0);
			row.Children.Add(btnSelectExe);
			row.Children.Add(exepath);
			var panel = new StackPanel { Margin = new Thickness(8), Spacing = 6 };
			panel.Children.Add(new TextBlock { Text = "External script editor:" });
			panel.Children.Add(row);
			return panel;
		}

		// Prevent inlining, otherwise there are unexpected interactions with Assembly.GetCallingAssembly
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void OnAccept(PreferencesController controller)
		{
			BuilderPlug.Me.SetEditor(exepath.Text);
		}

		public void Setup(PreferencesController controller)
		{
			controller.AddTab(new System.Windows.Forms.TabPage { Text = "UDBScript", NativeControl = Build() });
			controller.OnAccept += OnAccept;
		}

		private void btnSelectExe_Click(object sender, EventArgs e)
		{
			var dialog = new System.Windows.Forms.OpenFileDialog { Filter = "Executable files|*.exe;*|All files|*", Title = "Select external script editor" };
			if(General.Dialogs.ShowFileDialog(dialog) == System.Windows.Forms.DialogResult.OK) exepath.Text = dialog.FileName;
		}

		public void Dispose() { }
	}
}
