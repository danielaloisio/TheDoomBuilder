// One 3D floor in the docker next to the map: its top flat, border texture and bottom flat with the heights, and a green stripe at the
// left when it is the highlighted one. UDB's ThreeDFloorHelperTooltipElementControl; the pictures and labels keep their names and the
// properties the mode sets (Image, Text), so ThreeDFloorMode fills them the way it always did.
using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DoomBuilder.UI;
using Control = Avalonia.Controls.Control;
using Color = Avalonia.Media.Color;
using AvImage = Avalonia.Controls.Image;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class ThreeDFloorHelperTooltipElementControl
	{
		/// <summary>A picture box: set <see cref="Image"/> and it shows.</summary>
		public sealed class PictureSlot
		{
			private readonly AvImage picture = new AvImage { Width = 64, Height = 64, Stretch = Stretch.Uniform };
			internal Control View { get { return picture; } }
			internal bool HasImage { get { return picture.Source != null; } }
			public System.Drawing.Image Image { set { picture.Source = value == null ? null : ImageConvert.ToAvalonia(value); } }
		}

		/// <summary>A label: set <see cref="Text"/>.</summary>
		public sealed class TextSlot
		{
			private readonly TextBlock label = new TextBlock { Text = "X", HorizontalAlignment = HorizontalAlignment.Center };
			internal Control View { get { return label; } }
			public string Text { get { return label.Text; } set { label.Text = value; } }
		}

		public readonly PictureSlot sectorTopFlat = new PictureSlot();
		public readonly PictureSlot sectorBorderTexture = new PictureSlot();
		public readonly PictureSlot sectorBottomFlat = new PictureSlot();
		public readonly TextSlot topHeight = new TextSlot();
		public readonly TextSlot borderHeight = new TextSlot();
		public readonly TextSlot bottomHeight = new TextSlot();

		private readonly Border view = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0, 192, 0)), Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 4) };
		private bool highlighted;

		/// <summary>The control to put on screen.</summary>
		public Control View { get { return view; } }

		public bool Visible { get { return view.IsVisible; } set { view.IsVisible = value; } }

		/// <summary>The green stripe at the left.</summary>
		public bool Highlighted
		{
			get { return highlighted; }
			set { highlighted = value; Refresh(); }
		}

		// For the tests
		internal PictureSlot[] Pictures { get { return new[] { sectorTopFlat, sectorBorderTexture, sectorBottomFlat }; } }
		internal TextSlot[] Heights { get { return new[] { topHeight, borderHeight, bottomHeight }; } }

		public ThreeDFloorHelperTooltipElementControl()
		{
			highlighted = false;

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
			void Cell(int col, string title, PictureSlot picture, TextSlot height)
			{
				var stack = new StackPanel { Margin = new Thickness(0, 0, 6, 0), Spacing = 1 };
				stack.Children.Add(new TextBlock { Text = title, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11 });
				stack.Children.Add(picture.View);
				stack.Children.Add(height.View);
				Grid.SetColumn(stack, col);
				grid.Children.Add(stack);
			}
			Cell(0, "Top", sectorTopFlat, topHeight);
			Cell(1, "Border", sectorBorderTexture, borderHeight);
			Cell(2, "Bottom", sectorBottomFlat, bottomHeight);
			view.Child = grid;
			Refresh();
		}

		/// <summary>Draws it again (the stripe follows <see cref="Highlighted"/>).</summary>
		public void Refresh()
		{
			view.BorderThickness = highlighted ? new Thickness(5, 0, 0, 0) : new Thickness(0);
		}
	}
}
