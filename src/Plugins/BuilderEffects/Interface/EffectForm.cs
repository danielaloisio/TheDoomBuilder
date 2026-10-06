// The base of the windows of Builder Effects (UDB's DelayedForm): a window that changes the map while it is open and answers OK or Cancel.
// DialogResult is Cancel until a button says otherwise (closing the window with its close button is a Cancel); OnFormClosing runs once, as the
// window closes, and is where the forms keep their settings (OK) or withdraw what they did (Cancel).
using System;
using System.Windows.Forms;
using Avalonia.Controls;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public class EffectForm : Window
	{
		private bool closing;

		public new DialogResult DialogResult { get; set; } = DialogResult.Cancel;
		public string Text { get { return Title; } set { Title = value; } }

		protected EffectForm()
		{
			SizeToContent = SizeToContent.WidthAndHeight;
			CanResize = false;
			ShowInTaskbar = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Closing += (s, e) =>
			{
				if(closing) return;
				closing = true;
				OnFormClosing();
			};
		}

		/// <summary>The window is closing: <see cref="DialogResult"/> tells how.</summary>
		protected virtual void OnFormClosing() { }

		/// <summary>Shows the window as a dialog; the answer is the DialogResult.</summary>
		public DialogResult ShowDialog(IWin32Window owner)
		{
			DialogHost.ShowModal(this);
			return DialogResult;
		}

		public DialogResult ShowDialog() { return ShowDialog(null); }

		/// <summary>The row of OK / Cancel buttons at the bottom of a window.</summary>
		internal static StackPanel ButtonRow(FxButton ok, FxButton cancel)
		{
			ok.View.MinWidth = cancel.View.MinWidth = 90;
			ok.View.IsDefault = true;
			cancel.View.IsCancel = true;
			var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 8, 0, 0) };
			row.Children.Add(ok.View);
			row.Children.Add(cancel.View);
			return row;
		}

		public void Dispose() { }
	}
}
