using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeImp.DoomBuilder.Actions;

namespace CodeImp.DoomBuilder.Windows
{
	public enum PaletteGroup { Recent, Usable, Unusable }

	/// <summary>One line of the command palette.</summary>
	public sealed class PaletteEntry
	{
		internal PaletteEntry(CodeImp.DoomBuilder.Actions.Action action, PaletteGroup group)
		{
			Action = action;
			Group = group;
			Title = action.Title;
			Shortcut = CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(action.ShortcutKey);
			string category;
			Category = General.Actions.Categories.TryGetValue(action.Category, out category) ? category : string.Empty;
		}

		internal CodeImp.DoomBuilder.Actions.Action Action { get; private set; }
		public PaletteGroup Group { get; private set; }
		public string Title { get; private set; }
		public string Category { get; private set; }
		public string Shortcut { get; private set; }

		/// <summary>False for actions nothing is bound to (they are listed last and do nothing when run).</summary>
		public bool Usable { get { return Action.BeginBound || Action.EndBound; } }
		public string ActionName { get { return Action.Name; } }
	}

	/// <summary>
	/// The command palette without any UI (UDB's CommandPaletteControl): the actions matching what was typed, the ones used
	/// lately first, then the ones that can be run, then the rest, each sorted by title.
	/// </summary>
	public sealed class CommandPaletteModel
	{
		public const int MaxRecent = 5;
		private readonly List<CodeImp.DoomBuilder.Actions.Action> recent = new List<CodeImp.DoomBuilder.Actions.Action>();

		public IReadOnlyList<string> RecentNames { get { return recent.Select(a => a.Name).ToList(); } }

		/// <summary>The entries for the text typed. With nothing typed the recent actions come first.</summary>
		public List<PaletteEntry> Search(string text)
		{
			string search = (text ?? "").Trim();
			bool withrecent = search.Length == 0;

			var matching = General.Actions.GetAllActions().Where(a => MatchText(a.Title, search)).ToList();
			var result = new List<PaletteEntry>();

			if(withrecent)
				foreach(CodeImp.DoomBuilder.Actions.Action a in recent) result.Add(new PaletteEntry(a, PaletteGroup.Recent));

			foreach(var a in matching.Where(a => a.BeginBound || a.EndBound).OrderBy(a => a.Title)) result.Add(new PaletteEntry(a, PaletteGroup.Usable));
			foreach(var a in matching.Where(a => !(a.BeginBound || a.EndBound)).OrderBy(a => a.Title)) result.Add(new PaletteEntry(a, PaletteGroup.Unusable));
			return result;
		}

		/// <summary>Remembers the action as the most recent one and runs it.</summary>
		public void Run(PaletteEntry entry)
		{
			if(entry == null) return;

			recent.Remove(entry.Action);
			recent.Insert(0, entry.Action);
			if(recent.Count > MaxRecent) recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);

			General.Actions.InvokeAction(entry.Action.Name);
		}

		/// <summary>
		/// Words may be abbreviated: "ex sel" finds "Export Selection". The text matches when it is contained as it is, or when every
		/// typed word starts some word of the title, in the same order.
		/// </summary>
		public static bool MatchText(string text, string search)
		{
			text = Regex.Replace((text ?? "").ToLowerInvariant().Trim(), @"\s+", " ");
			search = Regex.Replace((search ?? "").ToLowerInvariant().Trim(), @"\s+", " ");

			if(string.IsNullOrWhiteSpace(search) || text.Contains(search)) return true;

			List<string> textitems = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
			string[] searchitems = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			for(int i = 0; i < searchitems.Length; i++)
			{
				string si = searchitems[i];
				if(string.IsNullOrEmpty(si)) continue;

				// The longest start of the typed word that begins some word of the title; the rest is looked for in the words after it
				string result = null;
				while(si.Length > 0)
				{
					string start = si;
					result = textitems.FirstOrDefault(ti => ti.StartsWith(start));
					if(result != null)
					{
						searchitems[i] = searchitems[i].Remove(0, si.Length);
						i--;
						break;
					}
					si = si.Remove(si.Length - 1);
				}

				if(result == null) return false;
				textitems.RemoveRange(0, textitems.IndexOf(result) + 1);
			}

			return true;
		}
	}
}
