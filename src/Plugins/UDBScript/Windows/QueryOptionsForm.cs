// The window of QueryOptions.query(): asks the user for the values of the options that the script added. UDB's QueryOptionsForm.
// The options are only data until the window is shown (a script runs on its own thread, and a window can only be made on the UI thread);
// ShowDialog builds the window, so it has to be called from the UI thread (QueryOptions.query does that).
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Windows.Forms;
using Avalonia.Controls;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class QueryOptionsForm
	{
		private readonly List<ScriptOption> options = new List<ScriptOption>();
		private SimpleDialog dialog;
		private ScriptOptionsControl parametersview;

		// For the tests
		internal IReadOnlyList<ScriptOption> Options { get { return options; } }
		internal SimpleDialog Dialog { get { return dialog; } }
		internal ScriptOptionsControl ParametersView { get { return parametersview; } }

		public void AddOption(string name, string description, int type, object defaultvalue)
		{
			AddOption(name, description, type, defaultvalue, null);
		}

		public void AddOption(string name, string description, int type, object defaultvalue, Dictionary<string, object> enumvalues)
		{
			ScriptOption so = new ScriptOption(name, description, type, enumvalues, defaultvalue);
			so.ReloadTypeHandler();
			options.Add(so);
		}

		public void Clear()
		{
			options.Clear();
		}

		public ExpandoObject GetScriptOptions()
		{
			ExpandoObject eo = new ExpandoObject();
			var values = eo as IDictionary<string, object>;
			foreach(ScriptOption so in options) values[so.name] = so.typehandler.GetValue();
			return eo;
		}

		public DialogResult ShowDialog()
		{
			parametersview = new ScriptOptionsControl { MinWidth = 420 };
			parametersview.SetOptions(options);
			dialog = new SimpleDialog("Query options", parametersview);
			dialog.Validate = () => { parametersview.EndEdit(); return true; };
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}
	}
}
