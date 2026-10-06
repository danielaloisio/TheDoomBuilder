// The controls of the window (what UDB's designer made).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public partial class JitterVerticesForm
	{
		private readonly IntControl positionJitterAmmount = new IntControl { Label = "Position:", AllowNegative = false, ExtendedLimits = true, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateTranslation = new FxButton("", "Pick other random values", Properties.Resources.Update);
		private readonly FxButton bApply = new FxButton("Apply");
		private readonly FxButton bCancel = new FxButton("Cancel");

		// For the tests
		internal IntControl PositionControl { get { return positionJitterAmmount; } }
		internal FxButton UpdateButton { get { return bUpdateTranslation; } }
		internal FxButton ApplyButton { get { return bApply; } }
		internal FxButton CancelButton { get { return bCancel; } }

		private void BuildView()
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			row.Children.Add(positionJitterAmmount);
			row.Children.Add(bUpdateTranslation.View);
			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 6 };
			layout.Children.Add(row);
			layout.Children.Add(ButtonRow(bApply, bCancel));
			Content = layout;

			positionJitterAmmount.OnValueChanging += positionJitterAmmount_OnValueChanging;
			bUpdateTranslation.Click += bUpdateTranslation_Click;
			bApply.Click += bApply_Click;
			bCancel.Click += bCancel_Click;
		}
	}
}
