// The controls of the window (what UDB's designer made).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public partial class JitterThingsForm
	{
		private static FxButton Update() { return new FxButton("", "Pick other random values", Properties.Resources.Update); }

		private readonly IntControl positionJitterAmmount = new IntControl { Label = "Position:", AllowNegative = false, ExtendedLimits = true, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateTranslation = Update();
		private readonly IntControl heightJitterAmmount = new IntControl { Label = "Height:", AllowNegative = false, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateHeight = Update();
		private readonly IntControl rotationJitterAmmount = new IntControl { Label = "Angle:", AllowNegative = false, Minimum = 0, Maximum = 359 };
		private readonly FxButton bUpdateAngle = Update();
		private readonly IntControl pitchAmmount = new IntControl { Label = "Pitch:", AllowNegative = false, Minimum = 0, Maximum = 359 };
		private readonly FxButton bUpdatePitch = Update();
		private readonly IntControl rollAmmount = new IntControl { Label = "Roll:", AllowNegative = false, Minimum = 0, Maximum = 359 };
		private readonly FxButton bUpdateRoll = Update();
		private readonly FxCheck cbRelativePitch = new FxCheck("Relative to initial pitch");
		private readonly FxCheck cbRelativeRoll = new FxCheck("Relative to initial roll");
		private readonly FxCheck cbNegativePitch = new FxCheck("Use negative pitch");
		private readonly FxCheck cbNegativeRoll = new FxCheck("Use negative roll");
		private readonly FxGroup scalegroup = new FxGroup(" Scale: ");
		private readonly FxCheck cbRelativeScale = new FxCheck("Relative to initial scale");
		private readonly FxCheck cbUniformScale = new FxCheck("Same width and height");
		private readonly FxCheck cbNegativeScaleX = new FxCheck("Use negative width");
		private readonly FxCheck cbNegativeScaleY = new FxCheck("Use negative height");
		private readonly FxLabel minScaleXLabel = new FxLabel("Width min.:");
		private readonly FxNumber minScaleX = new FxNumber(-100, 100, 1, 2, 0.1m);
		private readonly FxLabel maxScaleXLabel = new FxLabel("max.:");
		private readonly FxNumber maxScaleX = new FxNumber(-100, 100, 1, 2, 0.1m);
		private readonly FxButton bUpdateScaleX = Update();
		private readonly FxLabel minScaleYLabel = new FxLabel("Height min.:");
		private readonly FxNumber minScaleY = new FxNumber(-100, 100, 1, 2, 0.1m);
		private readonly FxLabel maxScaleYLabel = new FxLabel("max.:");
		private readonly FxNumber maxScaleY = new FxNumber(-100, 100, 1, 2, 0.1m);
		private readonly FxButton bUpdateScaleY = Update();
		private readonly FxButton bApply = new FxButton("Apply");
		private readonly FxButton bCancel = new FxButton("Cancel");

		// For the tests
		internal IntControl PositionControl { get { return positionJitterAmmount; } }
		internal IntControl HeightControl { get { return heightJitterAmmount; } }
		internal IntControl AngleControl { get { return rotationJitterAmmount; } }
		internal IntControl PitchControl { get { return pitchAmmount; } }
		internal IntControl RollControl { get { return rollAmmount; } }
		internal FxNumber MinScaleX { get { return minScaleX; } }
		internal FxNumber MaxScaleX { get { return maxScaleX; } }
		internal FxNumber MinScaleY { get { return minScaleY; } }
		internal FxNumber MaxScaleY { get { return maxScaleY; } }
		internal FxCheck RelativeScale { get { return cbRelativeScale; } }
		internal FxCheck UniformScale { get { return cbUniformScale; } }
		internal FxCheck RelativePitch { get { return cbRelativePitch; } }
		internal FxGroup ScaleGroup { get { return scalegroup; } }
		internal FxButton UpdateTranslationButton { get { return bUpdateTranslation; } }
		internal FxButton UpdateAngleButton { get { return bUpdateAngle; } }
		internal FxButton ApplyButton { get { return bApply; } }
		internal FxButton CancelButton { get { return bCancel; } }

		private static StackPanel Row(params Control[] controls)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			foreach(Control c in controls) row.Children.Add(c);
			return row;
		}

		private void BuildView()
		{
			var position = new FxGroup(" Position: ");
			position.Content.Children.Add(Row(positionJitterAmmount, bUpdateTranslation.View));
			position.Content.Children.Add(Row(heightJitterAmmount, bUpdateHeight.View));

			var rotation = new FxGroup(" Rotation: ");
			rotation.Content.Children.Add(Row(rotationJitterAmmount, bUpdateAngle.View));
			rotation.Content.Children.Add(Row(pitchAmmount, bUpdatePitch.View));
			rotation.Content.Children.Add(cbRelativePitch.View);
			rotation.Content.Children.Add(cbNegativePitch.View);
			rotation.Content.Children.Add(Row(rollAmmount, bUpdateRoll.View));
			rotation.Content.Children.Add(cbRelativeRoll.View);
			rotation.Content.Children.Add(cbNegativeRoll.View);
			ToolTip.SetTip(cbNegativePitch.View, "When checked, 50% of the time \nnegative pitch will be used.");
			ToolTip.SetTip(cbNegativeRoll.View, "When checked, 50% of the time \nnegative roll will be used");

			scalegroup.Content.Children.Add(cbRelativeScale.View);
			scalegroup.Content.Children.Add(cbUniformScale.View);
			scalegroup.Content.Children.Add(Row(minScaleXLabel.View, minScaleX.View, maxScaleXLabel.View, maxScaleX.View, bUpdateScaleX.View));
			scalegroup.Content.Children.Add(cbNegativeScaleX.View);
			scalegroup.Content.Children.Add(Row(minScaleYLabel.View, minScaleY.View, maxScaleYLabel.View, maxScaleY.View, bUpdateScaleY.View));
			scalegroup.Content.Children.Add(cbNegativeScaleY.View);
			ToolTip.SetTip(cbNegativeScaleX.View, "When checked, width scale will be picked from\n[-max .. -min] - [min .. max] ranges");
			ToolTip.SetTip(cbNegativeScaleY.View, "When checked, height scale will be picked from\n[-max .. -min] - [min .. max] ranges");

			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
			layout.Children.Add(position.View);
			layout.Children.Add(rotation.View);
			layout.Children.Add(scalegroup.View);
			layout.Children.Add(ButtonRow(bApply, bCancel));
			Content = layout;

			positionJitterAmmount.OnValueChanged += positionJitterAmmount_OnValueChanged;
			rotationJitterAmmount.OnValueChanged += rotationJitterAmmount_OnValueChanged;
			heightJitterAmmount.OnValueChanging += heightJitterAmmount_OnValueChanging;
			pitchAmmount.OnValueChanging += pitchAmmount_OnValueChanging;
			rollAmmount.OnValueChanging += rollAmmount_OnValueChanging;
			minScaleX.ValueChanged += minScaleX_ValueChanged;
			maxScaleX.ValueChanged += minScaleX_ValueChanged;
			minScaleY.ValueChanged += minScaleY_ValueChanged;
			maxScaleY.ValueChanged += minScaleY_ValueChanged;
			bUpdateTranslation.Click += bUpdateTranslation_Click;
			bUpdateHeight.Click += bUpdateHeight_Click;
			bUpdateAngle.Click += bUpdateAngle_Click;
			bUpdatePitch.Click += bUpdatePitch_Click;
			bUpdateRoll.Click += bUpdateRoll_Click;
			bUpdateScaleX.Click += bUpdateScaleX_Click;
			bUpdateScaleY.Click += bUpdateScaleY_Click;
			bApply.Click += bApply_Click;
			bCancel.Click += bCancel_Click;
		}
	}
}
