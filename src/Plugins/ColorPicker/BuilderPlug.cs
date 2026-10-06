#region ================== Namespaces

using System;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.ColorPicker.Windows;
using CodeImp.DoomBuilder.Plugins;
using CodeImp.DoomBuilder.VisualModes;
using CodeImp.DoomBuilder.Windows;

#endregion

namespace CodeImp.DoomBuilder.ColorPicker
{
	public class BuilderPlug : Plug
	{
		private static BuilderPlug me;
		public static BuilderPlug Me { get { return me; } }

		public override int MinimumRevision { get { return 2600; } }

		public override string Name { get { return "Color Picker"; } }

		private IColorPicker form;

		// For the tests
		internal ToolsForm Tools { get { return toolsform; } }
		internal IColorPicker Picker { get { return form; } }
		private ToolsForm toolsform;

		private Avalonia.PixelPoint formLocation; //used to keep form's location constant

		public override void OnInitialize() 
		{
			base.OnInitialize();
			me = this;

			// Load menus form
			toolsform = new ToolsForm();

			General.Actions.BindMethods(this);
		}

		public override void OnMapOpenEnd() 
		{
			base.OnMapOpenEnd();
			if(!General.Map.DOOM) toolsform.Register();
		}

		public override void OnMapNewEnd() 
		{
			base.OnMapNewEnd();
			if(!General.Map.DOOM) toolsform.Register();
		}

		public override void OnMapCloseEnd() 
		{
			base.OnMapCloseEnd();
			if(!General.Map.DOOM) toolsform.Unregister();
		}

		public override void OnReloadResources() 
		{
			base.OnReloadResources();
			if(!General.Map.DOOM) toolsform.Register();
		}

		public override void Dispose() 
		{
			base.Dispose();
			General.Actions.UnbindMethods(this);

			if(form != null) form.Close();
			form = null;

			if(toolsform != null) 
			{
				toolsform.Unregister();
				toolsform.Dispose();
				toolsform = null;
			}
		}

		[BeginAction("togglelightpannel")]
		private void ToggleLightPannel() 
		{
			SelectableElement deselectelement = null;

			if(General.Editing.Mode == null || General.Map.DOOM) return;
			string currentModeName = General.Editing.Mode.GetType().Name;

			//display one of colorPickers or tell the user why we can't do that
			if(currentModeName == "ThingsMode")
			{
				if(General.Map.Map.SelectedThingsCount == 0)
				{
					// If nothing is selected try to use a highlighted object
					if (General.Editing.Mode.HighlightedObject != null)
					{
						((Thing)General.Editing.Mode.HighlightedObject).Selected = true;
						deselectelement = (Thing)General.Editing.Mode.HighlightedObject;
					}
					else
					{
						General.Interface.DisplayStatus(StatusType.Warning, "Select or highlight some lights first!");
						return;
					}
				}
				form = new LightColorPicker();
			} 
			else if(currentModeName == "SectorsMode") 
			{
				if(General.Map.UDMF) 
				{
					if(General.Map.Map.SelectedSectorsCount == 0) 
					{
						if (General.Editing.Mode.HighlightedObject != null)
							((Sector)General.Editing.Mode.HighlightedObject).Selected = true;
						else
						{
							General.Interface.DisplayStatus(StatusType.Warning, "Select or highlight some sectors first!");
							return;
						}
					}
					form = new SectorColorPicker();
				} 
				else 
				{
					General.Interface.DisplayStatus(StatusType.Warning, "Sector colors can only be set if map is in UDMF format!");
					return;
				}
			} 
			else if(currentModeName == "BaseVisualMode") 
			{
				//nothing selected in visual mode?
				if( ((VisualMode)General.Editing.Mode).GetSelectedVisualThings(true).Count == 0 ) 
				{
					//check sectors
					int selectedSectorsCount = ((VisualMode)General.Editing.Mode).GetSelectedVisualSectors(true).Count;
					if(General.Map.UDMF && (selectedSectorsCount > 0 || General.Map.Map.SelectedSectorsCount > 0)) 
					{
						form = new SectorColorPicker();
					} 
					else 
					{
						General.Interface.DisplayStatus(StatusType.Warning, "Select some lights " + (General.Map.UDMF ? ", sectors or surfaces " : "") + "first!");
						return;
					}
				} 
				else 
				{
					form = new LightColorPicker();
				}
			} 
			else //wrong mode
			{ 
				General.Interface.DisplayStatus(StatusType.Warning, "Switch to" + (General.Map.UDMF ? " Sectors," : "") + " Things or GZDoom Visual Mode first!");
				return;
			}

			if(form.Setup(currentModeName)) 
			{
				if(formLocation.X == 0 && formLocation.Y == 0) 
				{
					// The first time: at the top right of the main window
					Avalonia.Controls.Window main = global::DoomBuilder.UI.DialogHost.Owner == null ? null : global::DoomBuilder.UI.DialogHost.Owner();
					if(main != null) formLocation = new Avalonia.PixelPoint((int)(main.Position.X + main.Bounds.Width - form.Width - 16), main.Position.Y + 80);
				} 
				form.Location = formLocation;
				form.FormClosed += form_FormClosed;
				form.ShowDialog();

				if(deselectelement != null)
				{
					deselectelement.Selected = false;						
				}
			} 

			General.Interface.RedrawDisplay();
		}

		private void form_FormClosed(object sender, EventArgs e) 
		{
			formLocation = form.Location;
		}
	}
}