// The "3D floors" window: every 3D floor of the selected sectors as a row (ThreeDFloorHelperControl), with commands to add, duplicate,
// split and detach them. UDB's ThreeDFloorEditorWindow as a modal Avalonia dialog; the plugin sets ThreeDFloors, shows it, and reads
// ThreeDFloors back when the answer is OK.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;
using AvCheckBox = Avalonia.Controls.CheckBox;
using Control = Avalonia.Controls.Control;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class ThreeDFloorEditorWindow : IDisposable, IWin32Window
	{
		#region ================== Variables

		private List<ThreeDFloor> threedfloors = new List<ThreeDFloor>();
		private List<Sector> selectedsectors = new List<Sector>();
		private readonly List<ThreeDFloorHelperControl> controlpool = new List<ThreeDFloorHelperControl>();

		private StackPanel threeDFloorPanel;
		private Border no3dfloorspanel;
		private AvCheckBox sharedThreeDFloorsCheckBox;
		private SimpleDialog dialog;

		#endregion

		#region ================== Properties

		public IntPtr Handle { get { return IntPtr.Zero; } }
		public List<Sector> SelectedSectors { get { return selectedsectors; } }
		public List<ThreeDFloor> ThreeDFloors { get { return threedfloors; } set { threedfloors = value; } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal IReadOnlyList<ThreeDFloorHelperControl> Controls { get { return controlpool; } }
		internal IEnumerable<ThreeDFloorHelperControl> UsedControls { get { return controlpool.Where(c => c.Used); } }
		internal bool NoFloorsMessageVisible { get { return no3dfloorspanel != null && no3dfloorspanel.IsVisible; } }
		internal AvCheckBox SharedOnlyBox { get { return sharedThreeDFloorsCheckBox; } }
		internal IReadOnlyList<AvButton> Commands { get { return commands; } }
		private readonly List<AvButton> commands = new List<AvButton>();

		#endregion

		#region ================== Window

		private AvButton Command(string text, Action click)
		{
			var button = new AvButton { Content = text, Margin = new Thickness(0, 0, 6, 0) };
			button.Click += (s, e) => click();
			commands.Add(button);
			return button;
		}

		private Control Build()
		{
			commands.Clear();
			threeDFloorPanel = new StackPanel { Spacing = 0 };
			sharedThreeDFloorsCheckBox = new AvCheckBox { Content = "Show shared 3D floors only", IsChecked = false, VerticalAlignment = VerticalAlignment.Center };
			sharedThreeDFloorsCheckBox.IsCheckedChanged += (s, e) => ShowSharedOnly();

			var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8, Margin = new Thickness(0, 24) };
			empty.Children.Add(new TextBlock { Text = "There are no 3D floors", HorizontalAlignment = HorizontalAlignment.Center });
			empty.Children.Add(Command("Add 3D floor", AddThreeDFloor));
			no3dfloorspanel = new Border { Child = empty };

			var bar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
			bar.Children.Add(Command("Add 3D floor", AddThreeDFloor));
			bar.Children.Add(Command("Detach all", DetachAll));
			bar.Children.Add(Command("Split all", SplitAll));
			bar.Children.Add(Command("Check all", () => CheckAll(true)));
			bar.Children.Add(Command("Uncheck all", () => CheckAll(false)));
			bar.Children.Add(sharedThreeDFloorsCheckBox);

			var list = new StackPanel();
			list.Children.Add(no3dfloorspanel);
			list.Children.Add(threeDFloorPanel);

			var layout = new DockPanel();
			DockPanel.SetDock(bar, Dock.Top);
			layout.Children.Add(bar);
			layout.Children.Add(new ScrollViewer { Content = list, MaxHeight = 560, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
			return layout;
		}

		public DialogResult ShowDialog()
		{
			// The window is shown again and again: start from the selection as it is now, with a clean list
			selectedsectors = new List<Sector>(General.Map.Map.GetSelectedSectors(true));
			controlpool.Clear();

			dialog = new SimpleDialog("3D floors", Build(), 1040);
			dialog.Validate = Accept;
			FillThreeDFloorPanel(threedfloors);

			bool ok = DialogHost.ShowModal(dialog);
			Cleanup();
			return ok ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }

		// OK: every control that is in use writes its 3D floor
		private bool Accept()
		{
			threedfloors = new List<ThreeDFloor>();

			foreach(ThreeDFloorHelperControl ctrl in controlpool)
			{
				if(ctrl.Used)
				{
					ctrl.ApplyToThreeDFloor();
					threedfloors.Add(ctrl.ThreeDFloor);
				}
			}

			return true;
		}

		// The window closed, whatever the answer: get rid of the dummy sectors and make all controls available
		private void Cleanup()
		{
			foreach(ThreeDFloorHelperControl ctrl in controlpool)
			{
				if(ctrl.Sector != null)
					ctrl.Sector.Dispose();

				ctrl.Used = false;
			}

			General.Map.Map.Update();
		}

		public void Dispose() { }

		#endregion

		#region ================== Controls

		// Gets a control from the pool or creates a new one
		private ThreeDFloorHelperControl GetThreeDFloorControl()
		{
			ThreeDFloorHelperControl ctrl = controlpool.FirstOrDefault(o => o.Used == false);

			if(ctrl == null)
			{
				ctrl = new ThreeDFloorHelperControl();
				ctrl.Editor = this;
				ctrl.Used = true;
				controlpool.Add(ctrl);
				threeDFloorPanel.Children.Add(ctrl.View);
			}
			else
			{
				ctrl.SetDefaults();
				ctrl.Used = true;
			}

			return ctrl;
		}

		private void ScrollIntoView(ThreeDFloorHelperControl ctrl)
		{
			ctrl.View.BringIntoView();
		}

		public void AddThreeDFloor()
		{
			ThreeDFloorHelperControl ctrl = GetThreeDFloorControl();
			ctrl.Show();
			ScrollIntoView(ctrl);
			no3dfloorspanel.IsVisible = false;
		}

		public void DuplicateThreeDFloor(ThreeDFloorHelperControl ctrl)
		{
			ThreeDFloorHelperControl dup = GetThreeDFloorControl();
			dup.Update(ctrl);
			dup.Show();
			ScrollIntoView(dup);
		}

		/// <summary>
		/// Splits the 3D floor, creating a new 3D floor for every checked sector
		/// </summary>
		/// <param name="ctrl">The control for the 3D floor that's to be split</param>
		public void SplitThreeDFloor(ThreeDFloorHelperControl ctrl)
		{
			List<int> items = new List<int>();
			List<ThreeDFloorHelperControl> controls = new List<ThreeDFloorHelperControl>() { ctrl };
			int numsplits = 0;

			// Create a list of all checked sectors
			for(int i = 0; i < ctrl.checkedListBoxSectors.Items.Count; i++)
			{
				if(ctrl.checkedListBoxSectors.GetItemCheckState(i) == CheckState.Checked)
					items.Add(i);
			}

			int useitem = items.Count - 1;

			/*
			Case 1: all tagged sectors are also selected sectors. In this case we can reuse
				the original control, so one less additional control is needed
			Case 2: multiple tagged sectors are also selected sectors. In this case we can
				reuse the original control, so one less additional control is needed
			Case 3: only one tagged sector is also the selected sector. In this case we
				have to add exactly one additional control
			*/
			if(items.Count == 1)
				numsplits = 1;
			else
				numsplits = items.Count - 1;

			// Get new controls for the additional 3D floors
			for(int i = 0; i < numsplits; i++)
			{
				var newctrl = GetThreeDFloorControl();
				newctrl.Update(ctrl);
				controls.Add(newctrl);
			}

			// Update the checkboxes of the controls to reflect the split 3D floors
			for(int i = controls.Count - 1; i >= 0; i--)
			{
				// Uncheck all sectors...
				for(int j = 0; j < items.Count; j++)
					controls[i].checkedListBoxSectors.SetItemChecked(items[j], false);

				// ... and only check a single one
				if(useitem >= 0)
					controls[i].checkedListBoxSectors.SetItemChecked(items[useitem], true);

				useitem--;
			}

			// Show the new controls
			foreach(ThreeDFloorHelperControl c in controls)
				c.Show();
		}

		public void DetachThreeDFloor(ThreeDFloorHelperControl ctrl)
		{
			ThreeDFloorHelperControl dup = GetThreeDFloorControl();
			dup.Update(ctrl);

			for(int i = 0; i < ctrl.checkedListBoxSectors.Items.Count; i++)
			{
				if(ctrl.checkedListBoxSectors.GetItemCheckState(i) == CheckState.Checked)
					ctrl.checkedListBoxSectors.SetItemChecked(i, false);
			}

			dup.Show();
			ScrollIntoView(dup);
		}

		private void FillThreeDFloorPanel(List<ThreeDFloor> threedfloors)
		{
			if(threedfloors.Count > 0)
			{
				// Create a new controller instance for each linedef and set its properties
				foreach(ThreeDFloor tdf in threedfloors.OrderByDescending(o => o.TopHeight).ToList())
				{
					ThreeDFloorHelperControl ctrl = GetThreeDFloorControl();
					ctrl.Update(tdf);
					ctrl.Show();
				}

				no3dfloorspanel.IsVisible = false;
			}
			else
			{
				no3dfloorspanel.IsVisible = true;
			}

			// Hide all unused pool controls
			if(controlpool.Count - threedfloors.Count > 0)
			{
				foreach(ThreeDFloorHelperControl ctrl in controlpool.Skip(threedfloors.Count))
				{
					ctrl.Used = false;
					ctrl.Hide();
				}
			}
		}

		private void ShowSharedOnly()
		{
			ICollection<Sector> selected = General.Map.Map.GetSelectedSectors(true);

			if(selected.Count > 1 && sharedThreeDFloorsCheckBox.IsChecked == true)
			{
				var hideControls = new List<ThreeDFloorHelperControl>();

				foreach(Sector s in selected)
				{
					foreach(ThreeDFloorHelperControl ctrl in controlpool)
					{
						// If the selected sector is not in the control's tagged sectors the control should be hidden
						if(!ctrl.ThreeDFloor.TaggedSectors.Contains(s))
							hideControls.Add(ctrl);
					}
				}

				foreach(ThreeDFloorHelperControl ctrl in hideControls)
				{
					// Hide controls, unless they are new
					if(ctrl.IsNew == false)
						ctrl.Hide();
				}
			}
			else
			{
				foreach(ThreeDFloorHelperControl ctrl in controlpool)
					if(ctrl.Used)
						ctrl.Show();
			}
		}

		private void DetachAll()
		{
			foreach(ThreeDFloorHelperControl ctrl in controlpool.Where(o => o.Used).ToList())
				DetachThreeDFloor(ctrl);
		}

		private void SplitAll()
		{
			foreach(ThreeDFloorHelperControl ctrl in controlpool.Where(o => o.Used).ToList())
				SplitThreeDFloor(ctrl);
		}

		private void CheckAll(bool ischecked)
		{
			foreach(ThreeDFloorHelperControl ctrl in controlpool.Where(o => o.Used))
				for(int i = 0; i < ctrl.checkedListBoxSectors.Items.Count; i++)
					ctrl.checkedListBoxSectors.SetItemChecked(i, ischecked);
		}

		#endregion
	}
}
