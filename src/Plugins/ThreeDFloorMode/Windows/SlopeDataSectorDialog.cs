// "Slope data sector": asks where the slope mode stores its data when the map has no slope data sector yet. UDB's SlopeDataSectorDialog
// as a modal Avalonia dialog; ShowDialog answers OK once a sector was chosen or created, Cancel otherwise.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Geometry;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class SlopeDataSectorDialog : IDisposable, IWin32Window
	{
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal AvButton UseSelectedButton { get; private set; }
		internal AvButton CreateNewButton { get; private set; }

		// Uses the selected sector (exactly one must be selected)
		internal void UseSelectedSector()
		{
			if(General.Map.Map.SelectedSectorsCount == 0)
				MessageBox.Show("No sectors selected. Please select exactly one sector", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			else if(General.Map.Map.SelectedSectorsCount > 1)
				MessageBox.Show(General.Map.Map.SelectedSectorsCount.ToString() + " sectors selected. Please select exactly one sector", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			else
			{
				General.Map.Map.ClearAllMarks(false);
				General.Map.Map.GetSelectedSectors(true).First().Marked = true;
				Finish();
			}
		}

		// Makes a new control sector in the control sector area
		internal void CreateNewSector()
		{
			List<DrawnVertex> drawnvertices = new List<DrawnVertex>();

			try
			{
				drawnvertices = BuilderPlug.Me.ControlSectorArea.GetNewControlSectorVertices();
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			General.Map.Map.ClearAllMarks(false);

			// DrawLines automatically marks the new sector, so we don't have to do it manually
			if(Tools.DrawLines(drawnvertices) == false)
			{
				MessageBox.Show("Could not draw new sector", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Update textures
			General.Map.Data.UpdateUsedTextures();

			// Update caches
			General.Map.Map.Update();

			General.Interface.RedrawDisplay();
			General.Map.IsChanged = true;
			Finish();
		}

		private void Finish()
		{
			// The dialog's own OK closes it with a positive answer
			dialog.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvButton.ClickEvent));
		}

		private TextBlock Description()
		{
			var red = new SolidColorBrush(Color.FromRgb(200, 40, 40));
			var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("The map does not contain a slope data sector. This sector is required by the slope mode to store data for the slope vertex groups.\n\nYou have two options:\n\n"));
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("  • Use selected sector: uses the currently selected sector to store the slope data. "));
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("Make sure that this sector is only used for this purpose! Do not edit or delete it!") { Foreground = red });
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("\n  • Create sector in CSA: automatically creates a new sector in the control sector area. "));
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("Do not edit or delete it!") { Foreground = red });
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("\n\n"));
			text.Inlines.Add(new Avalonia.Controls.Documents.Run("Creating the sector in the CSA is the recommended method to use.") { FontWeight = FontWeight.Bold });
			return text;
		}

		public DialogResult ShowDialog()
		{
			UseSelectedButton = new AvButton { Content = "Use selected sector" };
			CreateNewButton = new AvButton { Content = "Create sector in CSA" };
			UseSelectedButton.Click += (s, e) => UseSelectedSector();
			CreateNewButton.Click += (s, e) => CreateNewSector();

			var panel = new StackPanel { Spacing = 8 };
			panel.Children.Add(Description());
			dialog = new SimpleDialog("Slope data sector", panel);

			// The two choices sit next to Cancel; OK is only used to close the dialog once one of them worked
			dialog.ExtraButtons.Children.Add(UseSelectedButton);
			dialog.ExtraButtons.Children.Add(CreateNewButton);
			dialog.OkButton.IsVisible = false;
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
