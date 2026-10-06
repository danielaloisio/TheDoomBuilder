// Two small dialogs that the Find and Replace types use to fill their search box: a tri-state flags list ("set", "not set", "don't
// care") and the thing type browser with several types selected. UDB has them in Core/Windows; they are only used by this plugin here.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.BuilderModes
{
	/// <summary>"Select flags": each flag is checked (the flag must be set), unchecked ("!flag": must not be set) or left undetermined.</summary>
	internal static class FlagsForm
	{
		/// <summary>Returns the flags as UDB writes them ("flag1,!flag2"), or the value given when cancelled.</summary>
		public static string ShowDialog(IWin32Window owner, string value, IDictionary<string, string> flagdefs)
		{
			var boxes = new List<KeyValuePair<string, Avalonia.Controls.CheckBox>>();     // flag name, box
			var panel = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = 360, Margin = new Thickness(6) };
			foreach(KeyValuePair<string, string> def in flagdefs)
			{
				var box = new Avalonia.Controls.CheckBox { Content = def.Value, IsThreeState = true, IsChecked = null, MinWidth = 180, Margin = new Thickness(0, 1, 12, 1) };
				boxes.Add(new KeyValuePair<string, Avalonia.Controls.CheckBox>(def.Key, box));
				panel.Children.Add(box);
			}

			// Check the boxes of the flags in the value
			if(!string.IsNullOrEmpty((value ?? "").Trim()))
			{
				foreach(string s in value.Split(','))
				{
					string str = s.Trim();
					bool negative = str.StartsWith("!");
					if(negative) str = str.Substring(1);
					foreach(var pair in boxes)
						if(pair.Key == str) pair.Value.IsChecked = !negative;
				}
			}

			var dialog = new SimpleDialog("Select flags", new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
			if(!DialogHost.ShowModal(dialog)) return value;

			return string.Join(",", boxes.Where(p => p.Value.IsChecked != null).Select(p => p.Value.IsChecked == false ? "!" + p.Key : p.Key));
		}
	}
}

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>The thing browser with multiple selection (UDB's ThingMultipleBrowserForm).</summary>
	internal static class ThingMultipleBrowserForm
	{
		/// <summary>Returns the thing types chosen, or the ones given when cancelled.</summary>
		public static int[] BrowseThings(IWin32Window owner, int[] types)
		{
			var browser = new ThingBrowser { UseMultiSelection = true, MinWidth = 440, Height = 460 };
			browser.SelectMultipleTypes(types);
			var dialog = new SimpleDialog("Browse thing types", browser);
			browser.TypeDoubleClicked += () => dialog.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
			dialog.Opened += (s, e) => browser.FocusFilter();
			if(!DialogHost.ShowModal(dialog)) return types;
			return browser.Model.GetMultiResult(types);
		}
	}
}
