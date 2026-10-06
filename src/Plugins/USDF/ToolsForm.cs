// The toolbar button and the menu entry that open the dialog editor (UDB's ToolsForm was a hidden Form that only owned the ToolStrip items;
// they are on the toolbar while the map configuration has a DIALOGUE lump).
using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Windows;

namespace CodeImp.DoomBuilder.USDF
{
	public class ToolsForm : IDisposable
	{
		private readonly ToolStripButton dialogbutton;
		private readonly ToolStripMenuItem dialogitem;
		private bool disposed;

		// For the tests
		internal ToolStripButton Button { get { return dialogbutton; } }
		internal ToolStripMenuItem Item { get { return dialogitem; } }

		public ToolsForm()
		{
			dialogbutton = new ToolStripButton { Image = Properties.Resources.Dialog, Tag = "opendialogeditor", Text = "Open Dialog Editor" };
			dialogbutton.Click += InvokeTaggedAction;
			dialogitem = new ToolStripMenuItem { Image = Properties.Resources.Dialog, Tag = "opendialogeditor", Text = "Dialog Editor..." };
			dialogitem.Click += InvokeTaggedAction;

			General.Interface.AddButton(dialogbutton, ToolbarSection.Script);
			General.Interface.AddMenu(dialogitem, MenuSection.ViewScriptEdit);
		}

		public void Dispose()
		{
			if(disposed) return;
			disposed = true;
			General.Interface.RemoveButton(dialogbutton);
			General.Interface.RemoveMenu(dialogitem);
		}

		private void InvokeTaggedAction(object sender, EventArgs e)
		{
			General.Interface.InvokeTaggedAction(sender, e);
		}
	}
}
