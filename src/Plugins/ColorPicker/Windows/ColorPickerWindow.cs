// The window of the color pickers: UDB's pickers were forms with an OK and a Cancel button inside the picker control. OK closes with a positive
// answer and keeps what was done to the map while the picker was open; anything else (Cancel, Escape, the close button) withdraws it.
using System;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.ColorPicker.Controls;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.ColorPicker.Windows
{
	public abstract class ColorPickerWindow : Window
	{
		private bool answered;

		/// <summary>True when the picker was closed with OK.</summary>
		public bool Accepted { get; private set; }

		public event EventHandler FormClosed;

		/// <summary>The picker control of the window.</summary>
		internal ColorPickerControl Picker { get; private set; }

		protected ColorPickerWindow()
		{
			SizeToContent = SizeToContent.WidthAndHeight;
			CanResize = false;
			ShowInTaskbar = false;
			WindowStartupLocation = WindowStartupLocation.Manual;
			Closed += (s, e) => { if(FormClosed != null) FormClosed(this, EventArgs.Empty); };
		}

		public PixelPoint Location { get { return Position; } set { Position = value; } }

		/// <summary>Puts the picker control in the window and hooks its buttons up.</summary>
		protected void Attach(ColorPickerControl picker, Control content)
		{
			Picker = picker;
			Content = content;
			picker.OnOkPressed += (s, e) => Finish(true);
			picker.OnCancelPressed += (s, e) => Finish(false);
			Closing += (s, e) => { if(!answered) { answered = true; Accepted = false; OnClosing(false); } };
		}

		private void Finish(bool ok)
		{
			if(answered) return;
			answered = true;
			Accepted = ok;
			OnClosing(ok);
			Close(ok);
		}

		/// <summary>The picker is closing (true for OK): keep or withdraw what was done.</summary>
		protected abstract void OnClosing(bool ok);

		public bool ShowDialog()
		{
			return DialogHost.ShowModal(this);
		}
	}
}
