// The tag range button on the toolbar (UDB's ToolsForm was a hidden Form that only owned the ToolStrip items).
using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.IO;

namespace CodeImp.DoomBuilder.TagRange
{
	public class ToolsForm : IDisposable
	{
		private readonly ToolStripSeparator seperator1 = new ToolStripSeparator();
		private readonly ToolStripButton tagrangebutton;
		private readonly ToolStripSeparator seperator2 = new ToolStripSeparator();
		bool buttonontoolbar;

		// For the tests
		internal ToolStripButton Button { get { return tagrangebutton; } }
		internal bool ButtonOnToolbar { get { return buttonontoolbar; } }

		// Constructor
		public ToolsForm()
		{
			tagrangebutton = new ToolStripButton { Image = Properties.Resources.tag_blue, Tag = "rangetagselection", Text = "Tag Range" };
			tagrangebutton.Click += InvokeTaggedAction;
			buttonontoolbar = false;
		}

		public void Dispose() { }

		// This invokes an action from control event
		private void InvokeTaggedAction(object sender, EventArgs e)
		{
			General.Interface.InvokeTaggedAction(sender, e);
		}

		// This adds or removes the button from the DB toolbar
		public void UpdateButton()
		{
			if(buttonontoolbar)
			{
				General.Interface.RemoveButton(seperator1);
				General.Interface.RemoveButton(tagrangebutton);
				General.Interface.RemoveButton(seperator2);
				buttonontoolbar = false;
			}

			if(General.Editing.Mode == null)
				return;

			string modename = General.Editing.Mode.GetType().Name;
			IMapSetIO mapset = General.Map.FormatInterface;
			if((modename == "SectorsMode") ||
			   ((modename == "LinedefsMode") && mapset.HasLinedefTag) ||
			   ((modename == "ThingsMode") && mapset.HasThingTag))
			{
				General.Interface.AddButton(seperator1);
				General.Interface.AddButton(tagrangebutton);
				General.Interface.AddButton(seperator2);
				buttonontoolbar = true;
			}
		}
	}
}
