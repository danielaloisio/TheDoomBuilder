// The docker of the blockmap explorer mode: what is known about the block under the mouse and about the whole BLOCKMAP lump.
// UDB's BlockmapExplorerDocker with Avalonia controls (it travels inside the Docker as the native control of a shim Control).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CodeImp.DoomBuilder.BlockmapExplorer.Controls
{
	internal class BlockmapExplorerDocker : System.Windows.Forms.Control
	{
		private readonly TextBlock lblColumn = Value(), lblRow = Value(), lblOffset = Value(), lblIsSublist = Value(), lblNumLinesInBlock = Value();
		private readonly TextBlock lblTotalColumns = Value(), lblTotalRows = Value(), lblTotalBlocks = Value(), lblUniqueBlocks = Value();
		private readonly TextBlock lblLinesNotInBlocks = Value(), lblLumpSize = Value(), lblOffsetListEnd = Value();
		private readonly TextBlock lblQuestionableOffsets = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(200, 40, 40)), IsVisible = false, Margin = new Thickness(6, 0) };
		private readonly Avalonia.Controls.CheckBox cbShowQuestionableBlocks = new Avalonia.Controls.CheckBox { Content = "Show questionable blocks", IsVisible = false, Margin = new Thickness(6, 0) };

		/// <summary>The check box as the mode used it in UDB (Checked, CheckedChanged).</summary>
		internal class CheckView
		{
			private readonly Avalonia.Controls.CheckBox box;
			public CheckView(Avalonia.Controls.CheckBox box) { this.box = box; box.IsCheckedChanged += (s, e) => CheckedChanged?.Invoke(this, System.EventArgs.Empty); }
			public bool Checked { get { return box.IsChecked == true; } set { box.IsChecked = value; } }
			public Avalonia.Controls.CheckBox Box { get { return box; } }
			public event System.EventHandler CheckedChanged;
		}

		private CheckView showquestionable;
		internal CheckView ShowQuestionableBlocks { get { return showquestionable ?? (showquestionable = new CheckView(cbShowQuestionableBlocks)); } }

		// For the tests
		internal string Column { get { return lblColumn.Text; } }
		internal string Row { get { return lblRow.Text; } }
		internal string Offset { get { return lblOffset.Text; } }
		internal string LinesInBlock { get { return lblNumLinesInBlock.Text; } }
		internal string IsSublist { get { return lblIsSublist.Text; } }
		internal string TotalColumns { get { return lblTotalColumns.Text; } }
		internal string TotalRows { get { return lblTotalRows.Text; } }
		internal string TotalBlocks { get { return lblTotalBlocks.Text; } }
		internal string UniqueBlocks { get { return lblUniqueBlocks.Text; } }
		internal string LinesNotInBlocks { get { return lblLinesNotInBlocks.Text; } }
		internal string LumpSize { get { return lblLumpSize.Text; } }
		internal string OffsetListEnd { get { return lblOffsetListEnd.Text; } }
		internal TextBlock QuestionableOffsets { get { return lblQuestionableOffsets; } }

		private static TextBlock Value() { return new TextBlock { Text = "0", HorizontalAlignment = HorizontalAlignment.Left }; }

		private static Control Group(string title, params (string, TextBlock)[] rows)
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 4, Margin = new Thickness(8) };
			for(int i = 0; i < rows.Length; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				var label = new TextBlock { Text = rows[i].Item1 };
				Grid.SetRow(label, i);
				Grid.SetRow(rows[i].Item2, i);
				Grid.SetColumn(rows[i].Item2, 1);
				grid.Children.Add(label);
				grid.Children.Add(rows[i].Item2);
			}
			var panel = new StackPanel();
			panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, Margin = new Thickness(8, 6, 8, 0) });
			panel.Children.Add(grid);
			return panel;
		}

		internal BlockmapExplorerDocker()
		{
			var root = new StackPanel { Spacing = 4 };
			root.Children.Add(Group("Block info", ("Column:", lblColumn), ("Row:", lblRow), ("Offset (bytes):", lblOffset), ("Is sublist:", lblIsSublist), ("Lines in block:", lblNumLinesInBlock)));
			root.Children.Add(Group("Blockmap info", ("Columns:", lblTotalColumns), ("Rows:", lblTotalRows), ("Total blocks:", lblTotalBlocks), ("Unique blocks:", lblUniqueBlocks),
				("Lines not in blocks:", lblLinesNotInBlocks), ("Lump size:", lblLumpSize), ("Offset list end:", lblOffsetListEnd)));
			root.Children.Add(lblQuestionableOffsets);
			root.Children.Add(cbShowQuestionableBlocks);
			NativeControl = new ScrollViewer { Content = root };
		}

		internal void SetBlockInfo(int column, int row, BlockmapData blockmapData)
		{
			lblColumn.Text = column.ToString();
			lblRow.Text = row.ToString();
			lblOffset.Text = blockmapData.BlockPointers[column, row].ToString();
			lblNumLinesInBlock.Text = blockmapData.GetLinesInBlock(column, row).Count.ToString();
			lblIsSublist.Text = blockmapData.Blocks[blockmapData.BlockPointers[column, row]]?.IsSublist == true ? "Yes" : "No";
		}

		internal void SetInfo(int totalBlocks, int uniqueBlocks, int questionableOffsetsCount, int columns, int rows, int linesNotInBlocks, long lumpSize)
		{
			lblTotalColumns.Text = columns.ToString();
			lblTotalRows.Text = rows.ToString();

			lblTotalBlocks.Text = totalBlocks.ToString();
			lblUniqueBlocks.Text = uniqueBlocks.ToString();

			lblLinesNotInBlocks.Text = linesNotInBlocks.ToString();

			lblLumpSize.Text = lumpSize.ToString();
			lblOffsetListEnd.Text = (8 + totalBlocks * 2).ToString();

			lblQuestionableOffsets.IsVisible = cbShowQuestionableBlocks.IsVisible = questionableOffsetsCount > 0;
			lblQuestionableOffsets.Text = $"There are {questionableOffsetsCount} offset{(questionableOffsetsCount == 1 ? "" : "s")} pointing into the header or offset list, which indicates problems with the blockmap, for example exceeding the maximum size";
		}

		internal void ClearBlockInfo()
		{
			lblColumn.Text = "-";
			lblRow.Text = "-";
			lblOffset.Text = "-";
			lblNumLinesInBlock.Text = "-";
			lblIsSublist.Text = "-";
		}
	}
}
