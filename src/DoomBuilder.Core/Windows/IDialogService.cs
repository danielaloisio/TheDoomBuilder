using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Map;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The dialogs the Core opens, answered by the host application. UDB's code asks synchronously
	/// (<c>if(form.ShowDialog(owner) == DialogResult.OK)</c>); the form objects in <c>Compat/DialogStubs.cs</c> keep that call shape and hand
	/// themselves to this service, which shows the real UI (Avalonia windows) and writes the user's choices back into them.
	/// </summary>
	public interface IDialogService
	{
		DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultbutton);

		/// <summary>Open or save file dialog. On OK, <see cref="FileDialog.FileName"/> (and FileNames) hold the choice.</summary>
		DialogResult ShowFileDialog(FileDialog dialog);

		/// <summary>Map options for a new map or the options of the open one. On OK, <c>Options</c> is the edited result.</summary>
		DialogResult ShowMapOptions(MapOptionsForm form);

		/// <summary>Choose the map, game configuration and resources for opening a file.</summary>
		DialogResult ShowOpenMapOptions(OpenMapOptionsForm form);

		/// <summary>Switch to another map of the same WAD.</summary>
		DialogResult ShowChangeMap(ChangeMapForm form);

		/// <summary>"Paste Special": what to do with the tags and actions of the pasted elements. Fills in <see cref="PasteOptionsForm.Options"/>.</summary>
		DialogResult ShowPasteOptions(PasteOptionsForm form);

		/// <summary>Runs an external command showing its output; OK = continue (it succeeded, or the user chose to go on).</summary>
		DialogResult ShowRunExternalCommand(RunExternalCommandForm form);

		/// <summary>The list of errors and warnings the editor has collected (<see cref="ErrorLogger"/>).</summary>
		void ShowErrors();

		/// <summary>
		/// The game configurations (resources, nodebuilders, test program, editing modes), opened on a page (-1 for the first).
		/// On OK the changes are already applied and saved; <paramref name="reloadresources"/> tells whether the open map must reload its resources.
		/// </summary>
		DialogResult ShowConfiguration(int page, out bool reloadresources);

		/// <summary>
		/// The program preferences. On OK they are already applied to the settings; <paramref name="reloadresources"/> tells whether the
		/// open map must load its resources again.
		/// </summary>
		DialogResult ShowPreferences(out bool reloadresources);

		/// <summary>Program name, version and links.</summary>
		void ShowAbout();

		/// <summary>The texture (or flat) browser. Returns the chosen name, or <paramref name="value"/> when cancelled.</summary>
		string BrowseImage(string value, bool flats);

		/// <summary>The thing type browser. Returns the chosen type, or <paramref name="type"/> when cancelled.</summary>
		int BrowseThing(int type);

		/// <summary>
		/// The edit dialogs of map elements. They change the elements live (an undo level is made on the first change) and call
		/// <paramref name="valueschanged"/> so the map can be redrawn; Cancel withdraws the changes.
		/// </summary>
		DialogResult ShowEditVertices(ICollection<Vertex> vertices, bool allowpositionchange, EventHandler valueschanged);
		DialogResult ShowEditLinedefs(ICollection<Linedef> lines, bool selectfront, bool selectback, EventHandler valueschanged);
		DialogResult ShowEditSectors(ICollection<Sector> sectors, EventHandler valueschanged);
		DialogResult ShowEditThings(ICollection<Thing> things, EventHandler valueschanged);
	}

	/// <summary>
	/// Answers every dialog as cancelled and logs messages: the default when no host has provided a real service
	/// (command-line tools, tests that do not care about dialogs).
	/// </summary>
	public class NoDialogs : IDialogService
	{
		public virtual DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultbutton)
		{
			Console.Error.WriteLine("[MessageBox] " + caption + ": " + text);
			return buttons == MessageBoxButtons.YesNo || buttons == MessageBoxButtons.YesNoCancel ? DialogResult.No
				 : buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.RetryCancel ? DialogResult.Cancel : DialogResult.OK;
		}

		public virtual DialogResult ShowFileDialog(FileDialog dialog) { return DialogResult.Cancel; }
		public virtual DialogResult ShowMapOptions(MapOptionsForm form) { return DialogResult.Cancel; }
		public virtual DialogResult ShowOpenMapOptions(OpenMapOptionsForm form) { return DialogResult.Cancel; }
		public virtual DialogResult ShowChangeMap(ChangeMapForm form) { return DialogResult.Cancel; }
		public virtual DialogResult ShowPasteOptions(PasteOptionsForm form) { return DialogResult.Cancel; }

		// Without a window the command just runs, and it counts as continued when it succeeded
		public virtual DialogResult ShowRunExternalCommand(RunExternalCommandForm form) { return form.Runner.RunToEnd() ? DialogResult.OK : DialogResult.Cancel; }
		public virtual DialogResult ShowConfiguration(int page, out bool reloadresources) { reloadresources = false; return DialogResult.Cancel; }
		public virtual DialogResult ShowPreferences(out bool reloadresources) { reloadresources = false; return DialogResult.Cancel; }
		public virtual void ShowErrors() { }
		public virtual void ShowAbout() { }
		public virtual string BrowseImage(string value, bool flats) { return value; }
		public virtual int BrowseThing(int type) { return type; }
		public virtual DialogResult ShowEditVertices(ICollection<Vertex> vertices, bool allowpositionchange, EventHandler valueschanged) { return DialogResult.Cancel; }
		public virtual DialogResult ShowEditLinedefs(ICollection<Linedef> lines, bool selectfront, bool selectback, EventHandler valueschanged) { return DialogResult.Cancel; }
		public virtual DialogResult ShowEditSectors(ICollection<Sector> sectors, EventHandler valueschanged) { return DialogResult.Cancel; }
		public virtual DialogResult ShowEditThings(ICollection<Thing> things, EventHandler valueschanged) { return DialogResult.Cancel; }
	}

	/// <summary>
	/// A dialog service for tests: records what was asked and answers from queues of canned replies (cancel when a queue is empty).
	/// </summary>
	public sealed class ScriptedDialogs : NoDialogs
	{
		public readonly List<string> Messages = new List<string>();
		public readonly Queue<DialogResult> MessageAnswers = new Queue<DialogResult>();
		public readonly Queue<string> FilesToChoose = new Queue<string>();
		public readonly List<FileDialog> FileDialogsShown = new List<FileDialog>();
		public Func<MapOptionsForm, DialogResult> OnMapOptions;
		public Func<OpenMapOptionsForm, DialogResult> OnOpenMapOptions;
		public Func<ChangeMapForm, DialogResult> OnChangeMap;
		public Func<PasteOptionsForm, DialogResult> OnPasteOptions;
		public Func<int, DialogResult> OnConfiguration;
		public Func<DialogResult> OnPreferences;
		public int ErrorsShown;
		public int AboutShown;

		public override DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultbutton)
		{
			Messages.Add(caption + ": " + text);
			return MessageAnswers.Count > 0 ? MessageAnswers.Dequeue() : base.ShowMessage(text, caption, buttons, icon, defaultbutton);
		}

		public override DialogResult ShowFileDialog(FileDialog dialog)
		{
			FileDialogsShown.Add(dialog);
			if(FilesToChoose.Count == 0) return DialogResult.Cancel;
			dialog.FileName = FilesToChoose.Dequeue();
			return DialogResult.OK;
		}

		public override DialogResult ShowMapOptions(MapOptionsForm form) { return OnMapOptions != null ? OnMapOptions(form) : DialogResult.Cancel; }
		public override DialogResult ShowOpenMapOptions(OpenMapOptionsForm form) { return OnOpenMapOptions != null ? OnOpenMapOptions(form) : DialogResult.Cancel; }
		public override DialogResult ShowChangeMap(ChangeMapForm form) { return OnChangeMap != null ? OnChangeMap(form) : DialogResult.Cancel; }
		public override DialogResult ShowPasteOptions(PasteOptionsForm form) { return OnPasteOptions != null ? OnPasteOptions(form) : DialogResult.Cancel; }
		public override DialogResult ShowConfiguration(int page, out bool reloadresources) { reloadresources = false; return OnConfiguration != null ? OnConfiguration(page) : DialogResult.Cancel; }
		public override DialogResult ShowPreferences(out bool reloadresources) { reloadresources = false; return OnPreferences != null ? OnPreferences() : DialogResult.Cancel; }
		public override void ShowErrors() { ErrorsShown++; }
		public override void ShowAbout() { AboutShown++; }
	}
}
