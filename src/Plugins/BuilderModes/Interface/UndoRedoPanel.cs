using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CodeImp.DoomBuilder.BuilderModes
{
	/// <summary>
	/// The "Undo / Redo" docker: the map's history as a list. Choosing a line takes the map to that point. The logic is
	/// <see cref="UndoRedoModel"/>; this is the control the shell shows (it travels inside the Docker as a native control).
	/// </summary>
	internal class UndoRedoPanel : System.Windows.Forms.Control
	{
		private readonly UndoRedoModel model = new UndoRedoModel();
		private readonly ListBox list = new ListBox();
		private bool updating;

		public UndoRedoPanel()
		{
			list.SelectionChanged += OnSelectionChanged;
			list.KeyUp += (s, e) => General.Interface.FocusDisplay();
			NativeControl = list;
		}

		internal UndoRedoModel Model { get { return model; } }
		internal ListBox List { get { return list; } }

		/// <summary>The description of the first line.</summary>
		public void SetBeginDescription(string description) { model.BeginDescription = description; }

		/// <summary>Shows the history of the map as it is now.</summary>
		public void UpdateList()
		{
			Action update = () =>
			{
				updating = true;
				model.Update();
				list.ItemsSource = RowControls();
				if(model.CurrentIndex >= 0 && model.CurrentIndex < model.Rows.Count)
				{
					list.SelectedIndex = model.CurrentIndex;
					list.ScrollIntoView(model.CurrentIndex);
				}
				updating = false;
			};
			if(Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) update(); else Avalonia.Threading.Dispatcher.UIThread.Post(update);
		}

		// The levels still to be redone are shown dimmed; the current one is the selected line
		private System.Collections.Generic.List<TextBlock> RowControls()
		{
			var controls = new System.Collections.Generic.List<TextBlock>();
			foreach(UndoRedoRow row in model.Rows)
			{
				var text = new TextBlock { Text = row.Text, TextTrimming = TextTrimming.CharacterEllipsis };
				if(row.Kind == UndoRowKind.Redo || row.Kind == UndoRowKind.More) text.Opacity = 0.5;
				controls.Add(text);
			}
			return controls;
		}

		private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(updating || list.SelectedIndex < 0) return;
			int index = list.SelectedIndex;
			if(model.GoTo(index)) UpdateList();       // the plugin also refreshes after the undo or redo itself
			else if(index != model.CurrentIndex) { updating = true; list.SelectedIndex = model.CurrentIndex; updating = false; }
			General.Interface.FocusDisplay();
		}

		public void Dispose() { }
	}
}
