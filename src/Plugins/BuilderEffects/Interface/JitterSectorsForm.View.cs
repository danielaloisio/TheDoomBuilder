// The controls of the window (what UDB's designer made).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public partial class JitterSectorsForm
	{
		private static FxButton Update() { return new FxButton("", "Pick other random values", Properties.Resources.Update); }

		private readonly IntControl positionJitterAmmount = new IntControl { Label = "Position:", AllowNegative = false, ExtendedLimits = true, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateTranslation = Update();
		private readonly IntControl ceilingHeightAmmount = new IntControl { Label = "Height:", AllowNegative = false, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateCeilingHeight = Update();
		private readonly FxCombo ceiloffsetmode = new FxCombo("Raise and lower", "Lower only", "Raise only");
		private readonly FxCheck cbUseCeilingVertexHeights = new FxCheck("Use vertex heights");
		private readonly IntControl floorHeightAmmount = new IntControl { Label = "Height:", AllowNegative = false, Minimum = 0, Maximum = 100 };
		private readonly FxButton bUpdateFloorHeight = Update();
		private readonly FxCombo flooroffsetmode = new FxCombo("Raise and lower", "Raise only", "Lower only");
		private readonly FxCheck cbUseFloorVertexHeights = new FxCheck("Use vertex heights");
		private readonly FxCheck cbKeepExistingTextures = new FxCheck("Don't change existing sidedef textures");
		private readonly FxCombo cbUpperTexStyle = new FxCombo("Don't change upper texture", "Use ceiling texture", "Pick upper texture");
		private readonly FxCombo cbLowerTexStyle = new FxCombo("Don't change lower texture", "Use floor texture", "Pick lower texture");
		private readonly FxGroup gbUpperTexture = new FxGroup("Upper Texture:");
		private readonly FxTexture textureUpper = new FxTexture();
		private readonly FxCheck cbPegTop = new FxCheck("Upper Unpegged");
		private readonly FxGroup gbLowerTexture = new FxGroup("Lower texture:");
		private readonly FxTexture textureLower = new FxTexture();
		private readonly FxCheck cbPegBottom = new FxCheck("Lower Unpegged");
		private readonly FxButton bApply = new FxButton("Apply");
		private readonly FxButton bCancel = new FxButton("Cancel");

		// For the tests
		internal IntControl PositionControl { get { return positionJitterAmmount; } }
		internal IntControl CeilingControl { get { return ceilingHeightAmmount; } }
		internal IntControl FloorControl { get { return floorHeightAmmount; } }
		internal FxCombo CeilingOffsetMode { get { return ceiloffsetmode; } }
		internal FxCombo FloorOffsetMode { get { return flooroffsetmode; } }
		internal FxCheck KeepTextures { get { return cbKeepExistingTextures; } }
		internal FxCheck CeilingVertexHeights { get { return cbUseCeilingVertexHeights; } }
		internal FxCheck FloorVertexHeights { get { return cbUseFloorVertexHeights; } }
		internal FxCombo UpperStyle { get { return cbUpperTexStyle; } }
		internal FxCombo LowerStyle { get { return cbLowerTexStyle; } }
		internal FxTexture UpperTexture { get { return textureUpper; } }
		internal FxTexture LowerTexture { get { return textureLower; } }
		internal FxGroup UpperGroup { get { return gbUpperTexture; } }
		internal FxGroup LowerGroup { get { return gbLowerTexture; } }
		internal FxCheck PegTop { get { return cbPegTop; } }
		internal FxCheck PegBottom { get { return cbPegBottom; } }
		internal FxButton UpdateTranslationButton { get { return bUpdateTranslation; } }
		internal FxButton UpdateFloorButton { get { return bUpdateFloorHeight; } }
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
			var position = new FxGroup("Position:");
			position.Content.Children.Add(Row(positionJitterAmmount, bUpdateTranslation.View));

			var ceiling = new FxGroup("Ceiling:");
			ceiling.Content.Children.Add(Row(ceilingHeightAmmount, bUpdateCeilingHeight.View));
			ceiling.Content.Children.Add(Row(new FxLabel("Offset mode:").View, ceiloffsetmode.View));
			ceiling.Content.Children.Add(cbUseCeilingVertexHeights.View);

			var floor = new FxGroup("Floor:");
			floor.Content.Children.Add(Row(floorHeightAmmount, bUpdateFloorHeight.View));
			floor.Content.Children.Add(Row(new FxLabel("Offset mode:").View, flooroffsetmode.View));
			floor.Content.Children.Add(cbUseFloorVertexHeights.View);

			gbUpperTexture.Content.Children.Add(textureUpper.View);
			gbUpperTexture.Content.Children.Add(cbPegTop.View);
			gbLowerTexture.Content.Children.Add(textureLower.View);
			gbLowerTexture.Content.Children.Add(cbPegBottom.View);
			var textures = new FxGroup("Textures:");
			textures.Content.Children.Add(cbKeepExistingTextures.View);
			textures.Content.Children.Add(Row(cbUpperTexStyle.View, cbLowerTexStyle.View));
			textures.Content.Children.Add(Row(gbUpperTexture.View, gbLowerTexture.View));

			var left = new StackPanel { Spacing = 8 };
			left.Children.Add(position.View);
			left.Children.Add(ceiling.View);
			left.Children.Add(floor.View);
			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
			layout.Children.Add(Row(left, textures.View));
			layout.Children.Add(ButtonRow(bApply, bCancel));
			Content = layout;

			positionJitterAmmount.OnValueChanging += positionJitterAmmount_OnValueChanging;
			ceilingHeightAmmount.OnValueChanging += ceilingHeightAmmount_OnValueChanging;
			floorHeightAmmount.OnValueChanging += floorHeightAmmount_OnValueChanging;
			cbKeepExistingTextures.CheckedChanged += cbKeepExistingTextures_CheckedChanged;
			cbUseFloorVertexHeights.CheckedChanged += cbUseFloorVertexHeights_CheckedChanged;
			cbUseCeilingVertexHeights.CheckedChanged += cbUseCeilingVertexHeights_CheckedChanged;
			bUpdateTranslation.Click += bUpdateTranslation_Click;
			bUpdateCeilingHeight.Click += bUpdateCeilingHeight_Click;
			bUpdateFloorHeight.Click += bUpdateFloorHeight_Click;
			ceiloffsetmode.SelectedIndexChanged += ceiloffsetmode_SelectedIndexChanged;
			flooroffsetmode.SelectedIndexChanged += flooroffsetmode_SelectedIndexChanged;
			cbPegTop.CheckedChanged += cbPegTop_CheckedChanged;
			cbPegBottom.CheckedChanged += cbPegBottom_CheckedChanged;
			textureLower.OnValueChanged += textureLower_OnValueChanged;
			textureUpper.OnValueChanged += textureUpper_OnValueChanged;
			cbUpperTexStyle.SelectedIndexChanged += cbUpperTexStyle_SelectedIndexChanged;
			cbLowerTexStyle.SelectedIndexChanged += cbLowerTexStyle_SelectedIndexChanged;
			bApply.Click += bApply_Click;
			bCancel.Click += bCancel_Click;
		}
	}
}
