// The docker next to the map in the 3D floor mode: a column of ThreeDFloorHelperTooltipElementControl, one per 3D floor of the highlighted
// sector. UDB's ThreeDFloorPanel; like the other dockers it travels as the native control of a shim Control.
using Avalonia.Controls;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class ThreeDFloorPanel : System.Windows.Forms.Control
	{
		private readonly StackPanel list = new StackPanel();

		public ThreeDFloorPanel()
		{
			NativeControl = new ScrollViewer { Content = list };
		}

		/// <summary>Adds an element at the end.</summary>
		public void Add(ThreeDFloorHelperTooltipElementControl element)
		{
			list.Children.Add(element.View);
		}

		// For the tests
		internal int Count { get { return list.Children.Count; } }
	}
}
