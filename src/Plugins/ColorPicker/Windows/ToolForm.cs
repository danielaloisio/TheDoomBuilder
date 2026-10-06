// The menu entry and the toolbar button that open the color picker (UDB's ToolsForm was a hidden Form that only owned the ToolStrip items).
using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Controls;

namespace CodeImp.DoomBuilder.ColorPicker
{
	public class ToolsForm : IDisposable
	{
		private readonly ToolStripButton cpButton;
		private readonly ToolStripMenuItem cpMenu;

		// For the tests
		internal ToolStripButton Button { get { return cpButton; } }
		internal ToolStripMenuItem Menu { get { return cpMenu; } }

		public ToolsForm()
		{
			cpButton = new ToolStripButton { Image = Properties.Resources.cp, Tag = "togglelightpannel", Text = "Pick Sector/Light Color" };
			cpButton.Click += InvokeTaggedAction;
			cpMenu = new ToolStripMenuItem { Image = Properties.Resources.cp, Tag = "togglelightpannel", Text = "Pick Sector/Light Color..." };
			cpMenu.Click += InvokeTaggedAction;
		}

		public void Dispose() { }

		public void Register()
		{
			General.Interface.AddModesMenu(cpMenu, "002_modify");
			General.Interface.AddModesButton(cpButton, "002_modify");
		}

		public void Unregister()
		{
			General.Interface.RemoveMenu(cpMenu);
			General.Interface.RemoveButton(cpButton);
		}

		private void InvokeTaggedAction(object sender, EventArgs e)
		{
			General.Interface.InvokeTaggedAction(sender, e);
		}
	}
}
