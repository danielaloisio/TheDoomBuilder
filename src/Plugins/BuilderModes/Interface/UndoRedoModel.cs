using System.Collections.Generic;
using CodeImp.DoomBuilder.Editing;

namespace CodeImp.DoomBuilder.BuilderModes
{
	internal enum UndoRowKind { Undo, Current, Redo, More }

	/// <summary>One line of the undo/redo history list.</summary>
	internal sealed class UndoRedoRow
	{
		public string Text;
		public UndoRowKind Kind;
		public override string ToString() { return Text; }
	}

	/// <summary>
	/// The logic of UDB's Undo / Redo docker without any UI: the history as a list of lines (the levels that can be undone, the one
	/// the map is at, and the ones that can be redone) and what choosing a line does. Long histories show a window of
	/// <see cref="MaxDisplayLevels"/> lines around the current one, with "..." where lines are left out.
	/// </summary>
	internal sealed class UndoRedoModel
	{
		public const int MaxDisplayLevels = 400;

		private readonly List<UndoRedoRow> rows = new List<UndoRedoRow>();

		/// <summary>The description of the first line, which is the map before anything was done ("New Map", "Opened Map").</summary>
		public string BeginDescription { get; set; }

		public IReadOnlyList<UndoRedoRow> Rows { get { return rows; } }

		/// <summary>The line the map is at now.</summary>
		public int CurrentIndex { get; private set; }

		/// <summary>Reads the history again from the map's undo manager.</summary>
		public void Update()
		{
			rows.Clear();
			CurrentIndex = -1;

			// Without a map (or one that is going away) there is no history
			if(General.Map == null || General.Map.IsDisposed || General.Map.UndoRedo == null) return;

			// All levels, oldest first: the ones to undo, then the ones to redo
			List<UndoSnapshot> levels = General.Map.UndoRedo.GetUndoList();
			levels.Reverse();
			int numundos = levels.Count;
			levels.AddRange(General.Map.UndoRedo.GetRedoList());

			// The window of levels to show
			int offset = numundos - (MaxDisplayLevels >> 1);
			if((offset + MaxDisplayLevels) > levels.Count) offset = levels.Count - MaxDisplayLevels;
			if(offset < 0) offset = 0;

			if(offset > 0)
			{
				// There is more above; it is not shown because a long list is slow
				rows.Add(new UndoRedoRow { Text = "...", Kind = UndoRowKind.More });
			}
			else
			{
				// The real beginning
				bool atfirst = (numundos == 0);
				rows.Add(new UndoRedoRow { Text = CodeImp.DoomBuilder.Localization.Localizer.T(BeginDescription), Kind = atfirst ? UndoRowKind.Current : UndoRowKind.Undo });
				if(atfirst) CurrentIndex = 0;
			}

			for(int i = offset; i < levels.Count; i++)
			{
				// No more than MaxDisplayLevels: the last line says there is more below
				bool toomany = (rows.Count - 1) == MaxDisplayLevels;
				UndoRowKind kind = (i == numundos - 1) ? UndoRowKind.Current : (i >= numundos ? UndoRowKind.Redo : UndoRowKind.Undo);
				rows.Add(new UndoRedoRow { Text = toomany ? "..." : CodeImp.DoomBuilder.Localization.Localizer.T(levels[i].Description), Kind = toomany ? UndoRowKind.More : kind });
				if(kind == UndoRowKind.Current && !toomany) CurrentIndex = rows.Count - 1;
				if((rows.Count - 1) > MaxDisplayLevels) break;
			}

			// The current level is always among the lines
			if(CurrentIndex == -1) CurrentIndex = 0;
		}

		/// <summary>
		/// Goes to a line of the history: undoes the levels after it, or redoes the levels up to it. Returns false when it is the
		/// line the map is at already. The list is read again by the plugin when the undo or redo has happened.
		/// </summary>
		public bool GoTo(int index)
		{
			if(index < 0 || index >= rows.Count || index == CurrentIndex || General.Map == null) return false;
			if(rows[index].Kind == UndoRowKind.More) return false;

			int delta = CurrentIndex - index;
			if(delta < 0) General.Map.UndoRedo.PerformRedo(-delta);
			else General.Map.UndoRedo.PerformUndo(delta);
			return true;
		}
	}
}
