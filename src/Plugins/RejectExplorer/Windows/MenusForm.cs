// The "Configure colors" button of the reject explorer mode (UDB's MenusForm was a hidden Form that only owned the ToolStrip item).
using System;
using System.Windows.Forms;

namespace CodeImp.DoomBuilder.RejectExplorer
{
	public class MenusForm : IDisposable
	{
		private readonly ToolStripButton colorconfiguration;

		public ToolStripButton ColorConfiguration { get { return colorconfiguration; } }

		public MenusForm()
		{
			colorconfiguration = new ToolStripButton { Image = Properties.Resources.ColorManagement, Tag = "rejectexplorercolorconfiguration", Text = "Configure colors" };
			colorconfiguration.Click += InvokeTaggedAction;
		}

		public void Dispose() { }

		private void InvokeTaggedAction(object sender, EventArgs e)
		{
			General.Interface.InvokeTaggedAction(sender, e);
		}
	}
}
