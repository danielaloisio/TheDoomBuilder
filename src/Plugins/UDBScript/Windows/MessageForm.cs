// The message of showMessage() / showMessageYesNo(): the text, one or two buttons and a way to abort the script.
// UDB's MessageForm as a modal Avalonia window. OK is the answer of the first option, Cancel the second; Abort ends the script.
using System;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class MessageForm : Window
	{
		private readonly AvButton btnButton1 = new AvButton { MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly AvButton btnButton2 = new AvButton { MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly AvButton btnAbortScript = new AvButton { Content = "Abort script", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly TextBox tbMessage = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinWidth = 380, MinHeight = 120, MaxHeight = 400 };
		private DialogResult result = DialogResult.Cancel;

		/// <summary>The answer: OK for the first button (or the only one), Cancel for the other, Abort for "Abort script".</summary>
		public DialogResult DialogResult { get { return result; } private set { result = value; } }

		// For the tests
		internal AvButton Button1 { get { return btnButton1; } }
		internal AvButton Button2 { get { return btnButton2; } }
		internal AvButton AbortButton { get { return btnAbortScript; } }
		internal TextBox MessageBox { get { return tbMessage; } }

		public MessageForm(string option1text, string option2text, string message)
		{
			Title = "Script Message";
			SizeToContent = SizeToContent.WidthAndHeight;
			CanResize = false;
			ShowInTaskbar = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;

			DialogResult first = DialogResult.OK, second = DialogResult.Cancel;
			if(option2text == null)
			{
				btnButton1.Content = option1text;
				btnButton2.IsVisible = false;
			}
			else
			{
				// Two options: the first one goes to the right, as OK
				btnButton1.Content = option2text;
				first = DialogResult.Cancel;
				btnButton2.Content = option1text;
				second = DialogResult.OK;
			}
			tbMessage.Text = message.Replace("\n", Environment.NewLine);

			btnButton1.Click += (s, e) => Answer(first);
			btnButton2.Click += (s, e) => Answer(second);
			btnAbortScript.Click += btnAbortScript_Click;
			btnButton1.IsDefault = true;
			KeyDown += (s, e) => { if(e.Key == Avalonia.Input.Key.Escape) { Answer(DialogResult.Cancel); e.Handled = true; } };

			var buttons = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
			DockPanel.SetDock(btnAbortScript, Dock.Left);
			buttons.Children.Add(btnAbortScript);
			var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
			right.Children.Add(btnButton1);
			right.Children.Add(btnButton2);
			buttons.Children.Add(right);
			var layout = new DockPanel { Margin = new Thickness(12) };
			DockPanel.SetDock(buttons, Dock.Bottom);
			layout.Children.Add(buttons);
			layout.Children.Add(tbMessage);
			Content = layout;
		}

		private void Answer(DialogResult answer)
		{
			DialogResult = answer;
			Close();
		}

		private void btnAbortScript_Click(object sender, EventArgs e)
		{
			if(System.Windows.Forms.MessageBox.Show("Are you sure you want to abort the script?", "Abort script", MessageBoxButtons.YesNo) == DialogResult.Yes)
				Answer(DialogResult.Abort);
		}

		/// <summary>Shows the message and waits for the answer.</summary>
		public DialogResult ShowDialog()
		{
			DialogHost.ShowModal(this);
			return DialogResult;
		}
	}
}
