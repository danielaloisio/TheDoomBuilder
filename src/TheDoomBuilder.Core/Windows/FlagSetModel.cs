using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>One flag of a flag list: its key (as stored on the element), the title shown, and a three-state value (null = elements differ).</summary>
	public sealed class FlagItem
	{
		public string Key { get; private set; }
		public string Title { get; private set; }
		/// <summary>True, false, or null when the elements being edited disagree.</summary>
		public bool? Value { get; set; }
		public FlagItem(string key, string title) { Key = key; Title = title; }
		public override string ToString() { return Title; }
	}

	/// <summary>
	/// A list of flags for several elements (UDB's CheckboxArrayControl without the UI): each flag is on, off, or undetermined when
	/// the elements disagree; a click makes it on or off, and only flags that are not undetermined are written back.
	/// </summary>
	public sealed class FlagSetModel
	{
		private readonly List<FlagItem> items = new List<FlagItem>();

		public IList<FlagItem> Items { get { return items; } }

		/// <summary>Lists the flags of the configuration (key -> title).</summary>
		public FlagSetModel(IEnumerable<KeyValuePair<string, string>> flags)
		{
			foreach(KeyValuePair<string, string> f in flags) items.Add(new FlagItem(f.Key, f.Value));
		}

		/// <summary>Reads the state of every flag from the elements: on or off when they agree, undetermined when not.</summary>
		public void Load<T>(IEnumerable<T> elements, Func<T, string, bool> isset)
		{
			List<T> list = elements.ToList();
			foreach(FlagItem item in items)
			{
				if(list.Count == 0) { item.Value = false; continue; }
				bool first = isset(list[0], item.Key);
				item.Value = list.All(e => isset(e, item.Key) == first) ? first : (bool?)null;
			}
		}

		/// <summary>Writes the flags that are not undetermined to an element.</summary>
		public void Apply(Action<string, bool> setflag)
		{
			foreach(FlagItem item in items)
				if(item.Value.HasValue) setflag(item.Key, item.Value.Value);
		}

		public FlagItem Find(string key) { return items.FirstOrDefault(i => i.Key == key); }

		/// <summary>The next state a click gives a flag: undetermined -> on -> off -> on...</summary>
		public static bool? NextState(bool? current) { return current == true ? false : true; }
	}
}
