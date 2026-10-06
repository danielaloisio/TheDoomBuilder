// The popup menu of the WadAuthor mode (UDB's WAuthorTools was a hidden Form that only owned the ContextMenuStrip). Like in UDB the entries
// are not wired to anything yet: they show what the mode is meant to offer for a linedef.
using System;
using Avalonia.Controls;
using AvMenuItem = Avalonia.Controls.MenuItem;

namespace CodeImp.DoomBuilder.BuilderModes.Editing
{
	public class WAuthorTools : IDisposable
	{
		private readonly ContextMenu linedefpopup = new ContextMenu();

		public WAuthorTools()
		{
			linedefpopup.Items.Add(new AvMenuItem { Header = "Properties..." });
			linedefpopup.Items.Add(new Separator());
			linedefpopup.Items.Add(new AvMenuItem { Header = "Delete" });
			linedefpopup.Items.Add(new AvMenuItem { Header = "Split" });
			linedefpopup.Items.Add(new AvMenuItem { Header = "Flip" });
			linedefpopup.Items.Add(new AvMenuItem { Header = "Curve..." });
		}

		// For the tests
		internal ContextMenu Menu { get { return linedefpopup; } }

		/// <summary>The popup for a linedef; Show opens it over the main window, where the pointer is.</summary>
		public WAuthorPopup LinedefPopup { get { return new WAuthorPopup(linedefpopup); } }

		public void Dispose() { }
	}

	public class WAuthorPopup
	{
		private readonly ContextMenu menu;
		internal WAuthorPopup(ContextMenu menu) { this.menu = menu; }

		public void Show(System.Drawing.Point screenposition)
		{
			Window owner = global::DoomBuilder.UI.DialogHost.Owner == null ? null : global::DoomBuilder.UI.DialogHost.Owner();
			if(owner != null) menu.Open(owner);
		}
	}
}
