using System.Collections.Generic;
using System.Windows.Forms;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The plugins' part of the preferences dialog (UDB's PreferencesForm + PreferencesController): asks the plugins for their tabs when
	/// the dialog is built, tells them when OK or Cancel is pressed and when the dialog closes. A tab is a TabPage whose
	/// <see cref="Control.NativeControl"/> is the control the shell shows.
	/// </summary>
	public sealed class PluginPreferences
	{
		private readonly PreferencesForm form = new PreferencesForm();
		private readonly PreferencesController controller;
		private bool closed;

		/// <summary>The tabs the plugins added, in plugin order.</summary>
		public IReadOnlyList<TabPage> Tabs { get { return form.Pages; } }

		public PluginPreferences()
		{
			controller = new PreferencesController(form);
			controller.AllowAddTab = true;
			if(General.Plugins != null) General.Plugins.OnShowPreferences(controller);
			controller.AllowAddTab = false;
		}

		/// <summary>OK was pressed (and the dialog's own values are valid).</summary>
		public void Accept() { controller.RaiseAccept(); }

		/// <summary>Cancel was pressed.</summary>
		public void Cancel() { controller.RaiseCancel(); }

		/// <summary>The dialog closed, whatever the answer: the plugins let go of their controls (once).</summary>
		public void Close()
		{
			if(closed) return;
			closed = true;
			if(General.Plugins != null) General.Plugins.OnClosePreferences(controller);
		}
	}
}
