// "Choose a Sound Environment": the list of the reverbs of the game configuration, and whether the sound environment thing is active.
// UDB's ReverbsPickerForm as a modal Avalonia dialog; ApplyTo puts the choice into a thing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.SoundPropagationMode
{
	public class ReverbsPickerForm : IDisposable, IWin32Window
	{
		private struct ReverbListItem
		{
			private readonly string name;
			public readonly int Arg0;
			public readonly int Arg1;

			public ReverbListItem(string name, int arg0, int arg1)
			{
				this.name = name + " (" + arg0 + " " + arg1 + ")";
				Arg0 = arg0;
				Arg1 = arg1;
			}

			public override string ToString()
			{
				return name;
			}
		}

		// Remember the last choice
		private static string previousenvironmentname;

		private readonly ListBox list = new ListBox { MinWidth = 320, Height = 260 };
		private readonly CheckBox cbactiveenv = new CheckBox { Content = "Active Sound Environment" };
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal ListBox List { get { return list; } }
		internal CheckBox ActiveBox { get { return cbactiveenv; } }
		internal SimpleDialog Dialog { get { return dialog; } }

		public ReverbsPickerForm(Thing t)
		{
			var items = new List<object>();
			foreach(KeyValuePair<string, KeyValuePair<int, int>> reverb in General.Map.Data.Reverbs)
				items.Add(new ReverbListItem(reverb.Key, reverb.Value.Key, reverb.Value.Value));
			list.ItemsSource = items;

			// The reverb of the thing, or else the one picked last time
			foreach(object item in items)
			{
				ReverbListItem rli = (ReverbListItem)item;
				if(rli.Arg0 == t.Args[0] && rli.Arg1 == t.Args[1])
				{
					list.SelectedItem = item;
					break;
				}
			}

			if(!string.IsNullOrEmpty(previousenvironmentname) && list.SelectedItem == null)
			{
				foreach(object item in items)
				{
					if(item.ToString() == previousenvironmentname)
					{
						list.SelectedItem = item;
						break;
					}
				}
			}

			cbactiveenv.IsChecked = !BuilderPlug.ThingDormant(t);
		}

		/// <summary>Puts the picked reverb (and the active / dormant choice) into a thing.</summary>
		public void ApplyTo(Thing t)
		{
			if(list.SelectedItem == null) return;
			ReverbListItem rli = (ReverbListItem)list.SelectedItem;
			t.Args[0] = rli.Arg0;
			t.Args[1] = rli.Arg1;
			BuilderPlug.SetThingDormant(t, cbactiveenv.IsChecked != true);
		}

		public DialogResult ShowDialog()
		{
			var layout = new StackPanel { Spacing = 6 };
			layout.Children.Add(new TextBlock { Text = "Sound Environments:", FontWeight = Avalonia.Media.FontWeight.Bold });
			layout.Children.Add(list);
			layout.Children.Add(cbactiveenv);
			dialog = new SimpleDialog("Choose a Sound Environment", layout);
			dialog.OkButton.IsEnabled = list.SelectedItem != null;
			list.SelectionChanged += (s, e) => dialog.OkButton.IsEnabled = list.SelectedItem != null;
			list.DoubleTapped += (s, e) => { if(list.SelectedItem != null) dialog.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); };
			dialog.Validate = () => { if(list.SelectedItem != null) previousenvironmentname = list.SelectedItem.ToString(); return list.SelectedItem != null; };
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
