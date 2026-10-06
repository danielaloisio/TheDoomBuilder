// A list of check boxes with three states, for the sectors a 3D floor is tagged to (UDB's CheckedListBox of the 3D floor control).
// Checked and unchecked are the user's to change; "indeterminate" marks a tagged sector that is not selected (shown filled, locked).
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Control = Avalonia.Controls.Control;
using CheckBox = Avalonia.Controls.CheckBox;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class CheckedSectorList
	{
		private class Entry
		{
			public string Text;
			public CheckBox Box;
		}

		private readonly List<Entry> entries = new List<Entry>();
		private readonly StackPanel panel = new StackPanel();
		private readonly ScrollViewer view;
		private bool programmatic;

		/// <summary>The control to put on screen.</summary>
		public Control View { get { return view; } }

		/// <summary>The state of a row changed (by the user or by the code): row, new state.</summary>
		public event Action<int, CheckState> ItemCheck;

		public CheckedSectorList()
		{
			view = new ScrollViewer { Content = panel, MinHeight = 60, MaxHeight = 140 };
			Items = new ItemList(this);
		}

		public ItemList Items { get; private set; }

		public class ItemList
		{
			private readonly CheckedSectorList list;
			internal ItemList(CheckedSectorList list) { this.list = list; }

			public int Count { get { return list.entries.Count; } }
			public string this[int index] { get { return list.entries[index].Text; } }

			/// <summary>Adds a row and returns its index.</summary>
			public int Add(string text, bool ischecked)
			{
				var box = new CheckBox { Content = text, IsChecked = ischecked };
				var entry = new Entry { Text = text, Box = box };
				list.entries.Add(entry);
				list.panel.Children.Add(box);
				box.IsCheckedChanged += (s, e) => { if(!list.programmatic) list.Raise(entry); };
				return list.entries.Count - 1;
			}

			public void Clear()
			{
				list.entries.Clear();
				list.panel.Children.Clear();
			}
		}

		private void Raise(Entry entry)
		{
			if(ItemCheck != null) ItemCheck(entries.IndexOf(entry), ToState(entry.Box.IsChecked));
		}

		private static CheckState ToState(bool? value) { return value == null ? CheckState.Indeterminate : value == true ? CheckState.Checked : CheckState.Unchecked; }

		public CheckState GetItemCheckState(int index) { return ToState(entries[index].Box.IsChecked); }
		public bool GetItemChecked(int index) { return entries[index].Box.IsChecked != false; }

		public void SetItemChecked(int index, bool ischecked)
		{
			// A locked row (a tagged sector that is not selected) keeps its state
			if(GetItemCheckState(index) == CheckState.Indeterminate) return;
			entries[index].Box.IsChecked = ischecked;
		}

		/// <summary>Sets any state; indeterminate also locks the row.</summary>
		public void SetItemCheckState(int index, CheckState state)
		{
			Entry entry = entries[index];
			programmatic = true;
			entry.Box.IsThreeState = state == CheckState.Indeterminate;
			entry.Box.IsChecked = state == CheckState.Indeterminate ? (bool?)null : state == CheckState.Checked;
			entry.Box.IsHitTestVisible = state != CheckState.Indeterminate;
			programmatic = false;
			Raise(entry);
		}
	}
}
