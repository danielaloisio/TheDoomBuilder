// Small wrappers that give Avalonia controls the members the forms of UDB's Builder Effects used on the WinForms ones (Checked, Enabled, Value, ...),
// so that the logic of those forms stays as it was and only the layout is new.
using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvButton = Avalonia.Controls.Button;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvComboBox = Avalonia.Controls.ComboBox;

namespace CodeImp.DoomBuilder.StairSectorBuilderMode
{
	internal class FxCheck
	{
		private readonly AvCheckBox view;
		public AvCheckBox View { get { return view; } }
		public event EventHandler CheckedChanged;
		public bool Checked { get { return view.IsChecked == true; } set { view.IsChecked = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }

		public FxCheck(string text)
		{
			view = new AvCheckBox { Content = text };
			view.IsCheckedChanged += (s, e) => { if(CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); };
		}
	}

	internal class FxButton
	{
		private readonly AvButton view;
		public AvButton View { get { return view; } }
		public event EventHandler Click;
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }

		public FxButton(string text, string tooltip = null, System.Drawing.Image image = null)
		{
			view = new AvButton { Content = text, HorizontalContentAlignment = HorizontalAlignment.Center };
			if(image != null) view.Content = new Avalonia.Controls.Image { Source = global::DoomBuilder.UI.ImageConvert.ToAvalonia(image), Width = 16, Height = 16 };
			if(tooltip != null) ToolTip.SetTip(view, tooltip);
			view.Click += (s, e) => { if(Click != null) Click(this, EventArgs.Empty); };
		}

		public void PerformClick() { if(Click != null) Click(this, EventArgs.Empty); }
	}

	internal class FxLabel
	{
		private readonly TextBlock view;
		public TextBlock View { get { return view; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }
		public string Text { get { return view.Text; } set { view.Text = value; } }
		public FxLabel(string text) { view = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }; }
	}

	internal class FxNumber
	{
		private readonly NumericUpDown view;
		public NumericUpDown View { get { return view; } }
		public event EventHandler ValueChanged;
		public decimal Value { get { return view.Value ?? 0; } set { view.Value = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }

		public FxNumber(decimal minimum, decimal maximum, decimal value, int decimals, decimal increment)
		{
			view = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = value, Increment = increment, FormatString = decimals > 0 ? "0." + new string('0', decimals) : "0", MinWidth = 90 };
			view.ValueChanged += (s, e) => { if(ValueChanged != null) ValueChanged(this, EventArgs.Empty); };
		}
	}

	internal class FxCombo
	{
		private readonly AvComboBox view;
		private readonly ObservableCollection<object> items = new ObservableCollection<object>();
		public AvComboBox View { get { return view; } }
		public ObservableCollection<object> Items { get { return items; } }
		public event EventHandler SelectedIndexChanged;
		public int SelectedIndex { get { return view.SelectedIndex; } set { view.SelectedIndex = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }

		public FxCombo(params object[] options)
		{
			foreach(object o in options) items.Add(o);
			view = new AvComboBox { ItemsSource = items, MinWidth = 140 };
			view.SelectionChanged += (s, e) => { if(SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); };
		}
	}

	/// <summary>A titled box around some content (a WinForms GroupBox).</summary>
	internal class FxGroup
	{
		private readonly Border view;
		private readonly StackPanel content = new StackPanel { Spacing = 4 };
		public Control View { get { return view; } }
		public StackPanel Content { get { return content; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }

		public FxGroup(string title)
		{
			var panel = new StackPanel { Spacing = 4 };
			panel.Children.Add(new TextBlock { Text = title.Trim(), FontWeight = Avalonia.Media.FontWeight.Bold });
			panel.Children.Add(content);
			view = new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(8), Child = panel };
		}
	}

	/// <summary>A radio button (Checked, Enabled).</summary>
	internal class FxRadio
	{
		private readonly RadioButton view;
		public RadioButton View { get { return view; } }
		public bool Checked { get { return view.IsChecked == true; } set { view.IsChecked = value; } }
		public FxRadio(string text, string group) { view = new RadioButton { Content = text, GroupName = group }; }
	}

	/// <summary>A text box (Text).</summary>
	internal class FxText
	{
		private readonly TextBox view;
		public TextBox View { get { return view; } }
		public string Text { get { return view.Text ?? ""; } set { view.Text = value; } }
		public FxText() { view = new TextBox { MinWidth = 220 }; }
	}

	/// <summary>The picker of a texture of the jitter window (TextureSelectorControl of UDB).</summary>
	internal class FxTexture
	{
		private readonly global::DoomBuilder.UI.TextureSelector view = new global::DoomBuilder.UI.TextureSelector { Width = 120, Height = 150 };
		public global::DoomBuilder.UI.TextureSelector View { get { return view; } }
		public event EventHandler OnValueChanged;
		public string TextureName { get { return view.TextureName; } set { view.TextureName = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }
		public FxTexture() { view.ValueChanged += (s, e) => { if(OnValueChanged != null) OnValueChanged(this, EventArgs.Empty); }; }
		public void Initialize() { }
	}

	/// <summary>A number box of the plugin (ButtonsNumericTextbox of UDB): Text, GetResult and WhenTextChanged.</summary>
	internal class FxNum
	{
		private readonly global::DoomBuilder.UI.NumberBox view;
		public global::DoomBuilder.UI.NumberBox View { get { return view; } }
		public event EventHandler WhenTextChanged;
		public string Text { get { return view.Text; } set { view.Text = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }
		public int GetResult(int original) { return view.GetResult(original); }

		public FxNum(bool allownegative)
		{
			view = new global::DoomBuilder.UI.NumberBox { AllowDecimal = false, AllowNegative = allownegative, MinWidth = 100 };
			view.WhenTextChanged += (s, e) => { if(WhenTextChanged != null) WhenTextChanged(this, EventArgs.Empty); };
		}
	}

	/// <summary>The picker of a flat of the window (FlatSelectorControl of UDB).</summary>
	internal class FxFlat
	{
		private readonly global::DoomBuilder.UI.FlatSelector view = new global::DoomBuilder.UI.FlatSelector { Width = 120, Height = 150 };
		public global::DoomBuilder.UI.FlatSelector View { get { return view; } }
		public string TextureName { get { return view.TextureName; } set { view.TextureName = value; } }
		public bool Enabled { get { return view.IsEnabled; } set { view.IsEnabled = value; } }
	}

	/// <summary>A page of a tab control.</summary>
	internal class FxTab
	{
		public TabItem Item { get; private set; }
		public string Text { get { return (string)Item.Header; } set { Item.Header = value; } }
		public FxTab(string text, Control content) { Item = new TabItem { Header = text, Content = content }; }
	}

	/// <summary>A tab control with the members the form used (TabPages, SelectedIndex, SelectedTab).</summary>
	internal class FxTabs
	{
		private readonly TabControl view = new TabControl();
		private readonly System.Collections.Generic.List<FxTab> pages = new System.Collections.Generic.List<FxTab>();
		public TabControl View { get { return view; } }
		public event EventHandler SelectedIndexChanged;
		public int Count { get { return pages.Count; } }
		public FxTab this[int index] { get { return pages[index]; } }
		public FxTabs() { view.SelectionChanged += (s, e) => { if(e.Source == view && SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }; }
		public void Add(FxTab page) { pages.Add(page); view.Items.Add(page.Item); }
		public void Remove(FxTab page) { pages.Remove(page); view.Items.Remove(page.Item); }
		public int SelectedIndex { get { return view.SelectedIndex; } set { view.SelectedIndex = value; } }
		public FxTab SelectedTab { get { int i = view.SelectedIndex; return i >= 0 && i < pages.Count ? pages[i] : null; } set { view.SelectedIndex = pages.IndexOf(value); } }
	}

	/// <summary>The list of prefabs: a name and a type for each, one of them selected.</summary>
	internal class FxPrefabList
	{
		private readonly ListBox view = new ListBox { MinHeight = 90 };
		private readonly System.Collections.ObjectModel.ObservableCollection<string> items = new System.Collections.ObjectModel.ObservableCollection<string>();
		public ListBox View { get { return view; } }
		public event EventHandler DoubleClick;
		public int Count { get { return items.Count; } }
		public int SelectedIndex { get { return view.SelectedIndex; } }
		public string TextOf(int index) { return items[index]; }
		public FxPrefabList() { view.ItemsSource = items; view.DoubleTapped += (s, e) => { if(DoubleClick != null) DoubleClick(this, EventArgs.Empty); }; }
		private static string Row(string name, string type) { return name + "    [" + type + "]"; }
		public void Add(string name, string type) { items.Add(Row(name, type)); }
		public void Insert(int index, string name, string type) { items.Insert(index, Row(name, type)); }
		public void RemoveAt(int index) { items.RemoveAt(index); }
	}
}
