#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeImp.DoomBuilder.Config;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>A category or a thing type in the thing browser's tree.</summary>
	public sealed class ThingBrowserNode
	{
		public string Title;
		/// <summary>Null for a category.</summary>
		public ThingTypeInfo Info;
		public ThingBrowserNode Parent;
		public readonly List<ThingBrowserNode> Children = new List<ThingBrowserNode>();
		/// <summary>The category (or, for a thing, its category) holds something obsolete.</summary>
		public bool IsObsolete;
		public bool IsCategory { get { return Info == null; } }
		public override string ToString() { return Title; }
	}

	/// <summary>
	/// The logic of UDB's thing type browser (ThingBrowserControl) without any UI: the tree of categories and things, the filter
	/// (titles that start with the text first, then those that contain it), the type number box and what is known about the type
	/// (size, floor/ceiling, blocking). With several things being edited a selection of several types is allowed: the result is then
	/// a random one among the selected.
	/// </summary>
	public sealed class ThingBrowserModel
	{
		private readonly List<ThingBrowserNode> tree = new List<ThingBrowserNode>();
		private readonly List<ThingBrowserNode> things = new List<ThingBrowserNode>();   // every thing node, in tree order
		private readonly List<ThingBrowserNode> selected = new List<ThingBrowserNode>();
		private string filter = "";
		private string typetext = "";

		public event Action<ThingTypeInfo> TypeChanged;

		/// <summary>Several types may be selected (editing several things).</summary>
		public bool UseMultiSelection { get; set; }

		public ThingBrowserModel()
		{
			AddCategories(General.Map.Data.ThingCategories, null, tree);
		}

		private bool AddCategories(ICollection<ThingCategory> categories, ThingBrowserNode parent, List<ThingBrowserNode> into)
		{
			bool anyobsolete = false;
			foreach(ThingCategory tc in categories)
			{
				var cn = new ThingBrowserNode { Title = tc.Title, Parent = parent };
				bool obsolete = AddCategories(tc.Children, cn, cn.Children);
				foreach(ThingTypeInfo ti in tc.Things)
				{
					var n = new ThingBrowserNode { Title = ti.Title, Info = ti, Parent = cn, IsObsolete = ti.IsObsolete };
					if(ti.IsObsolete) { n.Title += " - OBSOLETE"; obsolete = true; }
					cn.Children.Add(n);
					things.Add(n);
				}
				cn.IsObsolete = obsolete;
				anyobsolete |= obsolete;
				into.Add(cn);
			}
			return anyobsolete;
		}

		#region ================== The list

		/// <summary>The roots to show: the category tree, or (with a filter) a flat list of the matching things.</summary>
		public IList<ThingBrowserNode> Roots
		{
			get
			{
				string match = filter.Trim().ToUpperInvariant();
				if(match.Length == 0) return tree;

				var result = new List<ThingBrowserNode>();
				result.AddRange(things.Where(n => n.Title.ToUpperInvariant().StartsWith(match)));
				result.AddRange(things.Where(n => !n.Title.ToUpperInvariant().StartsWith(match) && n.Title.ToUpperInvariant().Contains(match)));
				return result;
			}
		}

		public string Filter { get { return filter; } set { filter = value ?? ""; } }

		#endregion

		#region ================== Selection

		/// <summary>The type number box.</summary>
		public string TypeText { get { return typetext; } }

		/// <summary>The type the number box names, or null when empty or unknown.</summary>
		public ThingTypeInfo Info { get; private set; }

		public IList<ThingBrowserNode> Selected { get { return selected; } }

		/// <summary>The typed number changed: finds the thing and selects it.</summary>
		public void SetTypeText(string text)
		{
			typetext = text ?? "";
			selected.Clear();
			Info = null;
			int index;
			if(typetext.Length > 0 && int.TryParse(typetext, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
			{
				Info = General.Map.Data.GetThingInfoEx(index);
				ThingBrowserNode node = things.FirstOrDefault(n => n.Info.Index == index);
				if(node != null) selected.Add(node);
			}
			if(TypeChanged != null) TypeChanged(Info);
		}

		public void SelectType(int type) { SetTypeText(type.ToString(CultureInfo.InvariantCulture)); }

		/// <summary>Several types at once (editing things of different types): nothing typed, those types selected.</summary>
		public void SelectMultipleTypes(int[] types)
		{
			if(types.Length == 0) { ClearSelectedType(); return; }
			if(types.Length == 1) { SelectType(types[0]); return; }

			ClearSelectedType();
			var set = new HashSet<int>(types);
			selected.AddRange(things.Where(n => set.Contains(n.Info.Index)));
		}

		public void ClearSelectedType()
		{
			selected.Clear();
			typetext = "";
			Info = null;
			if(TypeChanged != null) TypeChanged(null);
		}

		/// <summary>The user selected tree entries (categories select everything inside).</summary>
		public void Select(IEnumerable<ThingBrowserNode> nodes)
		{
			selected.Clear();
			selected.AddRange(nodes);
			List<ThingBrowserNode> valid = ValidNodes();

			if(UseMultiSelection && valid.Count > 1)
			{
				// Nothing sensible to show for several types
				typetext = "";
				Info = null;
			}
			else if(valid.Count == 1)
			{
				Info = valid[0].Info;
				typetext = Info.Index.ToString(CultureInfo.InvariantCulture);
			}
			else return;

			if(TypeChanged != null) TypeChanged(Info);
		}

		// The distinct things under the selection
		private List<ThingBrowserNode> ValidNodes()
		{
			var found = new Dictionary<string, ThingBrowserNode>(StringComparer.Ordinal);
			foreach(ThingBrowserNode n in selected) Collect(n, found);
			return found.Values.ToList();
		}

		private static void Collect(ThingBrowserNode node, Dictionary<string, ThingBrowserNode> found)
		{
			if(node.Children.Count == 0)
			{
				if(node.Info != null && !found.ContainsKey(node.Title)) found.Add(node.Title, node);
			}
			else foreach(ThingBrowserNode n in node.Children) Collect(n, found);
		}

		/// <summary>More than one type is selected (shows the "mixed things" icon, result is random).</summary>
		public bool HasMixedSelection { get { return ValidNodes().Count > 1; } }

		/// <summary>The type to apply: a random one of several selected, else the typed number, else <paramref name="original"/>.</summary>
		public int GetResult(int original)
		{
			List<ThingBrowserNode> valid = ValidNodes();
			if(UseMultiSelection && valid.Count > 0) return valid[General.Random(0, valid.Count - 1)].Info.Index;
			int typed;
			return int.TryParse(typetext, NumberStyles.Integer, CultureInfo.InvariantCulture, out typed) ? typed : original;
		}

		/// <summary>The types selected, when several (for things to be given different types).</summary>
		public int[] GetMultiResult(int[] original)
		{
			List<ThingBrowserNode> valid = ValidNodes();
			if(UseMultiSelection && valid.Count > 0) return valid.Select(n => n.Info.Index).ToArray();
			return original.Select(GetResult).ToArray();
		}

		#endregion

		#region ================== Information about the type

		/// <summary>"64 x 56" for the known thing, else "-".</summary>
		public string SizeText { get { return Info != null ? (Info.Radius * 2) + " x " + Info.Height : "-"; } }
		public string PositionText { get { return Info != null ? (Info.Hangs ? "Ceiling" : "Floor") : "-"; } }

		public string BlockingText
		{
			get
			{
				if(Info == null) return "-";
				switch(Info.Blocking)
				{
					case ThingTypeInfo.THING_BLOCKING_NONE: return "No";
					case ThingTypeInfo.THING_BLOCKING_FULL: return "Completely";
					case ThingTypeInfo.THING_BLOCKING_HEIGHT: return "True-Height";
					default: return "Unknown";
				}
			}
		}

		/// <summary>The actor class name, or "--" when there is none worth showing.</summary>
		public string ClassNameText
		{
			get { return HasClassName ? Info.ClassName : "--"; }
		}

		public bool HasClassName { get { return Info != null && !string.IsNullOrEmpty(Info.ClassName) && !Info.ClassName.StartsWith("$"); } }

		/// <summary>The class name links to the engine's help page when the configuration has one.</summary>
		public string ClassHelpUrl
		{
			get { return HasClassName && !string.IsNullOrEmpty(General.Map.Config.ThingClassHelp) ? General.Map.Config.ThingClassHelp.Replace("%K", Info.ClassName) : null; }
		}

		#endregion
	}
}
