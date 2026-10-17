using System;
using System.Collections.Generic;
using System.Linq;
using CodeImp.DoomBuilder.Controls;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The dockers (the side panel's tabs) without any UI: which exist, which one is selected and which was selected before.
	/// Follows UDB's DockersControl: removing the selected docker falls back to the previous one, and selecting the previous
	/// one when there is none picks the first.
	/// </summary>
	public sealed class DockerModel
	{
		private readonly List<Docker> dockers = new List<Docker>();
		private Docker selected;
		private Docker previous;

		public IReadOnlyList<Docker> Dockers { get { return dockers; } }
		public Docker Selected { get { return selected; } }

		/// <summary>The title of the selected docker, or "None" when there is none (what ActiveDockerTabName reports).</summary>
		public string SelectedTitle { get { return selected == null ? "None" : selected.Title; } }

		/// <summary>The list or the selection changed.</summary>
		public event Action Changed;

		/// <summary>A docker was added with the request to draw attention to it (a plugin's panel appearing while the dockers are collapsed).</summary>
		public event Action<Docker> NotifyRequested;

		public bool Contains(Docker d) { return d != null && dockers.Contains(d); }

		/// <param name="pluginprefix">The lower case name of the plugin that adds it; the docker's full name becomes prefix_name.</param>
		public void Add(Docker d, bool notify, string pluginprefix = null)
		{
			if(d == null || Contains(d)) return;
			if(!string.IsNullOrEmpty(pluginprefix)) d.MakeFullName(pluginprefix);

			dockers.Add(d);
			if(selected == null) selected = d;      // a tab control selects its first page
			if(notify && NotifyRequested != null) NotifyRequested(d);
			RaiseChanged();
		}

		/// <summary>Takes a docker down. When it was selected, the previous one (or the first) takes its place.</summary>
		public bool Remove(Docker d)
		{
			if(!Contains(d)) return false;

			if(d == selected) SelectPrevious();
			if(d == selected) selected = null;       // it was the previous one as well: nothing else to fall back on
			if(d == previous) previous = null;
			dockers.Remove(d);
			if(selected == null) selected = dockers.FirstOrDefault();

			RaiseChanged();
			return true;
		}

		public bool Select(Docker d, string pluginprefix = null)
		{
			if(!Contains(d)) return false;
			if(!string.IsNullOrEmpty(pluginprefix)) d.MakeFullName(pluginprefix);

			if(d != selected)
			{
				previous = selected;
				selected = d;
			}
			RaiseChanged();
			return true;
		}

		public void SelectPrevious()
		{
			Docker target = previous != null && Contains(previous) ? previous : dockers.FirstOrDefault();
			if(target != null) selected = target;
			RaiseChanged();
		}

		/// <summary>Orders the tabs by the full names saved in the settings; dockers not named there keep their order after the named ones.</summary>
		public void SortBy(IEnumerable<string> fullnames)
		{
			List<string> order = fullnames.ToList();
			List<Docker> sorted = dockers.OrderBy(d => { int i = order.IndexOf(d.FullName); return i < 0 ? int.MaxValue : i; }).ToList();
			dockers.Clear();
			dockers.AddRange(sorted);
			RaiseChanged();
		}

		private void RaiseChanged() { if(Changed != null) Changed(); }
	}
}
