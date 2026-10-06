// The options of a script: one row for each, with its description and an editor for the value (a text box, a drop-down for the options that are
// lists, a browse button for the types that have one). UDB's ScriptOptionsControl was a DataGridView; the rules are the same: an empty value is
// the default, a value that is the default is shown dimmed, and what is typed goes into the type handler of the option.
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Config;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;
using AvComboBox = Avalonia.Controls.ComboBox;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class ScriptOptionsControl : UserControl
	{
		/// <summary>A row of the options.</summary>
		internal sealed class OptionRow
		{
			public ScriptOption Option;
			public TextBlock Description;
			public TextBox Box;
			public AvComboBox Combo;
			public AvButton Browse;

			/// <summary>The control that edits the value.</summary>
			public Control Editor { get { return (Control)Combo ?? Box; } }
			public string Text { get { return Combo != null ? (Combo.Text ?? "") : (Box.Text ?? ""); } }
		}

		private readonly Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 4, ColumnSpacing = 8, Margin = new Thickness(4) };
		private readonly List<OptionRow> rows = new List<OptionRow>();
		private bool updating;

		public ScriptOptionsControl()
		{
			Content = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
			MinHeight = 60;
		}

		// For the tests
		internal IReadOnlyList<OptionRow> Rows { get { return rows; } }

		/// <summary>Shows these options (each one keeps its own value: that is what the script gets).</summary>
		public void SetOptions(IEnumerable<ScriptOption> options)
		{
			Clear();
			foreach(ScriptOption so in options) AddRow(so);
		}

		public void Clear()
		{
			grid.Children.Clear();
			grid.RowDefinitions.Clear();
			rows.Clear();
		}

		private void AddRow(ScriptOption so)
		{
			int index = rows.Count;
			grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			var row = new OptionRow { Option = so, Description = new TextBlock { Text = so.description, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 } };

			var cell = new DockPanel();
			if(so.typehandler.IsEnumerable)
			{
				so.ReloadTypeHandler();
				row.Combo = new AvComboBox { IsEditable = !so.typehandler.IsLimitedToEnums, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 120 };
				var items = so.typehandler.GetEnumList().Select(e => e.Title).ToList();
				row.Combo.ItemsSource = items;
				row.Combo.Text = so.typehandler.GetStringValue();
				string current = so.typehandler.GetStringValue();
				int sel = items.FindIndex(t => string.Compare(t, current, StringComparison.OrdinalIgnoreCase) == 0);
				if(sel < 0)
				{
					// The value may be the value of an item, not its title
					sel = so.typehandler.GetEnumList().ToList().FindIndex(e => string.Compare(e.Value, current, StringComparison.OrdinalIgnoreCase) == 0);
				}
				if(sel >= 0) row.Combo.SelectedIndex = sel;
				row.Combo.SelectionChanged += (s, e) => { if(!updating && row.Combo.SelectedItem != null) Commit(row, row.Combo.SelectedItem.ToString()); };
				row.Combo.LostFocus += (s, e) => { if(!updating) Commit(row, row.Combo.Text); };
				cell.Children.Add(row.Combo);
			}
			else
			{
				row.Box = new TextBox { Text = so.value == null ? "" : so.value.ToString(), HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 120 };
				row.Box.LostFocus += (s, e) => { if(!updating) Commit(row, row.Box.Text); };
				row.Box.KeyDown += (s, e) => { if(e.Key == Avalonia.Input.Key.Enter && !updating) Commit(row, row.Box.Text); };
				if(so.typehandler.IsBrowseable)
				{
					row.Browse = new AvButton { Content = "...", Margin = new Thickness(2, 0, 0, 0), Padding = new Thickness(6, 0) };
					row.Browse.Click += (s, e) => BrowseClicked(row);
					DockPanel.SetDock(row.Browse, Dock.Right);
					cell.Children.Add(row.Browse);
				}
				cell.Children.Add(row.Box);
			}

			Grid.SetRow(row.Description, index);
			Grid.SetRow(cell, index);
			Grid.SetColumn(cell, 1);
			grid.Children.Add(row.Description);
			grid.Children.Add(cell);
			rows.Add(row);
			Dim(row);
		}

		// What was typed (or picked) goes into the option; empty means the default
		private void Commit(OptionRow row, string text)
		{
			ScriptOption so = row.Option;
			object newvalue = text;
			if(string.IsNullOrWhiteSpace(text))
			{
				newvalue = so.defaultvalue;
				updating = true;
				if(row.Box != null) row.Box.Text = newvalue == null ? "" : newvalue.ToString();
				if(row.Combo != null) row.Combo.Text = newvalue == null ? "" : newvalue.ToString();
				updating = false;
			}

			so.typehandler.SetValue(newvalue);
			so.value = newvalue;
			Dim(row);
		}

		// A value that is the default is dimmed
		private void Dim(OptionRow row)
		{
			bool isdefault = row.Option.value != null && row.Option.defaultvalue != null && row.Option.value.ToString() == row.Option.defaultvalue.ToString();
			row.Editor.Opacity = isdefault ? 0.6 : 1.0;
		}

		private void BrowseClicked(OptionRow row)
		{
			ScriptOption so = row.Option;
			so.typehandler.Browse(General.Interface);
			updating = true;
			row.Box.Text = so.typehandler.GetStringValue();
			updating = false;
			so.value = so.typehandler.GetValue();
			Dim(row);
		}

		/// <summary>Takes what is being typed into the options (called before the script runs).</summary>
		public void EndEdit()
		{
			foreach(OptionRow row in rows) Commit(row, row.Text);
		}

		public void EndAddingOptions() { }

		/// <summary>Back to the default of every option.</summary>
		public void ResetToDefaults()
		{
			foreach(OptionRow row in rows)
			{
				ScriptOption so = row.Option;
				updating = true;
				string text = so.defaultvalue == null ? "" : so.defaultvalue.ToString();
				if(row.Box != null) row.Box.Text = text;
				if(row.Combo != null) row.Combo.Text = text;
				updating = false;
				so.typehandler.SetValue(so.defaultvalue);
				so.value = so.defaultvalue;
				Dim(row);
			}
		}

		/// <summary>The values of the options as the object the script gets.</summary>
		public ExpandoObject GetScriptOptions()
		{
			ExpandoObject eo = new ExpandoObject();
			var options = eo as IDictionary<string, object>;
			foreach(OptionRow row in rows) options[row.Option.name] = row.Option.typehandler.GetValue();
			return eo;
		}
	}
}
