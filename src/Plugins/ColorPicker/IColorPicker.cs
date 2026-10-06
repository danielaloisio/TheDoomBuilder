using System;
using Avalonia;

namespace CodeImp.DoomBuilder.ColorPicker
{
	/// <summary>A color picker window (for the lights or for the sectors): set up from the selection, then shown as a dialog.</summary>
	interface IColorPicker
	{
		PixelPoint Location { get; set; }
		double Width { get; }
		ColorPickerType Type { get; }
		bool Setup(string editingModeName);
		/// <summary>Shows the picker; true when it was closed with OK.</summary>
		bool ShowDialog();
		void Close();
		event EventHandler FormClosed;
	}
}
