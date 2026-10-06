// The controls of the window (what UDB's designer made).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	public partial class DirectionalShadingForm
	{
		private readonly NumberBox sunangletb = new NumberBox { AllowNegative = true, MinWidth = 100 };
		private readonly AngleDial sunangle = new AngleDial { Width = 90, Height = 90 };
		private readonly IntControl lightamount = new IntControl { Label = "Amount:", AllowNegative = false, Minimum = 0, Maximum = 255 };
		private readonly ColorField lightcolor = new ColorField();
		private readonly IntControl shadeamount = new IntControl { Label = "Amount:", AllowNegative = false, Minimum = 0, Maximum = 255 };
		private readonly ColorField shadecolor = new ColorField();
		private readonly FxButton apply = new FxButton("Apply");
		private readonly FxButton cancel = new FxButton("Cancel");

		// For the tests
		internal NumberBox SunAngleBox { get { return sunangletb; } }
		internal AngleDial SunAngleDial { get { return sunangle; } }
		internal IntControl LightAmount { get { return lightamount; } }
		internal ColorField LightColor { get { return lightcolor; } }
		internal IntControl ShadeAmount { get { return shadeamount; } }
		internal ColorField ShadeColor { get { return shadecolor; } }
		internal FxButton ApplyButton { get { return apply; } }
		internal FxButton CancelButton { get { return cancel; } }

		private static StackPanel Row(params Control[] controls)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			foreach(Control c in controls) row.Children.Add(c);
			return row;
		}

		private void BuildView()
		{
			var sun = new FxGroup(" Sun angle ");
			sun.Content.Children.Add(Row(sunangle, sunangletb));
			var light = new FxGroup(" Light");
			light.Content.Children.Add(lightamount);
			light.Content.Children.Add(Row(new TextBlock { Text = "Color:", VerticalAlignment = VerticalAlignment.Center }, lightcolor));
			var shade = new FxGroup(" Shade");
			shade.Content.Children.Add(shadeamount);
			shade.Content.Children.Add(Row(new TextBlock { Text = "Color:", VerticalAlignment = VerticalAlignment.Center }, shadecolor));

			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
			layout.Children.Add(sun.View);
			layout.Children.Add(light.View);
			layout.Children.Add(shade.View);
			layout.Children.Add(ButtonRow(apply, cancel));
			Content = layout;

			sunangle.AngleChanged += sunangle_AngleChanged;
			sunangletb.WhenTextChanged += sunangletb_WhenTextChanged;
			lightamount.OnValueChanging += OnShadingChanged;
			shadeamount.OnValueChanging += OnShadingChanged;
			lightcolor.ColorChanged += OnShadingChanged;
			shadecolor.ColorChanged += OnShadingChanged;
			apply.Click += apply_Click;
			cancel.Click += cancel_Click;
		}
	}
}
