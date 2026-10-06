// The menu entries and toolbar buttons of Builder Effects (UDB's MenusForm was a hidden Form that only owned the ToolStrip items).
using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Windows;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public class MenusForm : IDisposable
	{
		private readonly ToolStripMenuItem menujitter;
		private readonly ToolStripButton buttonjitter;
		private readonly ToolStripMenuItem menusectorflatshading;
		private readonly ToolStripButton buttonsectorflatshading;
		private readonly ToolStripMenuItem toolStripMenuItem1;

		// For the tests
		internal ToolStripMenuItem JitterMenu { get { return menujitter; } }
		internal ToolStripButton JitterButton { get { return buttonjitter; } }
		internal ToolStripMenuItem ShadingMenu { get { return menusectorflatshading; } }
		internal ToolStripButton ShadingButton { get { return buttonsectorflatshading; } }
		internal ToolStripMenuItem ImportMenu { get { return toolStripMenuItem1; } }

		public MenusForm()
		{
			menujitter = new ToolStripMenuItem { Image = Properties.Resources.Jitter, Tag = "applyjitter", Text = "Randomize..." };
			buttonjitter = new ToolStripButton { Image = Properties.Resources.Jitter, Tag = "applyjitter", Text = "Randomize" };
			menusectorflatshading = new ToolStripMenuItem { Image = Properties.Resources.FlatShading, Tag = "applydirectionalshading", Text = "Apply Directional Shading..." };
			buttonsectorflatshading = new ToolStripButton { Image = Properties.Resources.FlatShading, Tag = "applydirectionalshading", Text = "Apply Directional Shading" };
			toolStripMenuItem1 = new ToolStripMenuItem { Image = Properties.Resources.Terrain, Tag = "importobjasterrain", Text = "Wavefront .obj as Terrain..." };
			foreach(ToolStripItem item in new ToolStripItem[] { menujitter, buttonjitter, menusectorflatshading, buttonsectorflatshading, toolStripMenuItem1 })
				item.Click += InvokeTaggedAction;
		}

		public void Dispose() { }

		private void InvokeTaggedAction(object sender, EventArgs e)
		{
			General.Interface.InvokeTaggedAction(sender, e);
		}

		public void Register()
		{
			General.Interface.BeginToolbarUpdate();

			General.Interface.AddModesMenu(menujitter, "002_modify");
			General.Interface.AddModesButton(buttonjitter, "002_modify");
			General.Interface.AddModesMenu(menusectorflatshading, "002_modify");
			General.Interface.AddModesButton(buttonsectorflatshading, "002_modify");
			General.Interface.AddMenu(toolStripMenuItem1, MenuSection.FileImport);

			General.Interface.EndToolbarUpdate();
		}

		public void Unregister()
		{
			General.Interface.BeginToolbarUpdate();

			General.Interface.RemoveMenu(menujitter);
			General.Interface.RemoveButton(buttonjitter);
			General.Interface.RemoveMenu(menusectorflatshading);
			General.Interface.RemoveButton(buttonsectorflatshading);
			General.Interface.RemoveMenu(toolStripMenuItem1);

			General.Interface.EndToolbarUpdate();
		}
	}
}
