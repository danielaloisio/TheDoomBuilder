// The toolbar items of the automap mode: what to show, and the color preset. UDB's MenusForm was a UserControl that only owned the ToolStrip items.
using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Controls;

namespace CodeImp.DoomBuilder.AutomapMode
{
	public class MenusForm : IDisposable
	{
		private readonly ToolStripButton showhiddenlines = new ToolStripButton { CheckOnClick = true, Text = "Show hidden lines", Image = Properties.Resources.ShowHiddenLines };
		private readonly ToolStripButton showsecretsectors = new ToolStripButton { CheckOnClick = true, Text = "Show secrets", Image = Properties.Resources.ShowSecrets };
		private readonly ToolStripButton showlocks = new ToolStripButton { CheckOnClick = true, Text = "Show locks", Image = Properties.Resources.ShowLocks };
		private readonly ToolStripButton showtextures = new ToolStripButton { CheckOnClick = true, Text = "Show textures", Image = Properties.Resources.ShowTextures };
		private readonly ToolStripSeparator colorpresetseparator = new ToolStripSeparator();
		private readonly ToolStripLabel colorpresetlabel = new ToolStripLabel("Color preset:");
		private readonly ToolStripComboBox colorpreset = new ToolStripComboBox();

		public event EventHandler OnShowHiddenLinesChanged;
		public event EventHandler OnShowSecretSectorsChanged;
		public event EventHandler OnShowLocksChanged;
		public event EventHandler OnShowTexturesChanged;
		internal event EventHandler OnColorPresetChanged;

		public bool ShowHiddenLines { get { return showhiddenlines.Checked; } set { showhiddenlines.Checked = value; } }
		public bool ShowSecretSectors { get { return showsecretsectors.Checked; } set { showsecretsectors.Checked = value; } }
		public bool ShowLocks { get { return showlocks.Checked; } set { showlocks.Checked = value; } }
		public bool ShowTextures { get { return showtextures.Checked; } set { showtextures.Checked = value; } }
		internal AutomapMode.ColorPreset ColorPreset { get { return (AutomapMode.ColorPreset)colorpreset.SelectedIndex; } set { colorpreset.SelectedIndex = (int)value; } }

		// For the tests
		internal ToolStripButton HiddenLinesButton { get { return showhiddenlines; } }
		internal ToolStripButton SecretSectorsButton { get { return showsecretsectors; } }
		internal ToolStripButton LocksButton { get { return showlocks; } }
		internal ToolStripButton TexturesButton { get { return showtextures; } }
		internal ToolStripComboBox ColorPresetBox { get { return colorpreset; } }

		public MenusForm()
		{
			colorpreset.Items.AddRange(new object[] { "Doom", "Hexen", "Strife" });
			showhiddenlines.CheckedChanged += (s, e) => { if(OnShowHiddenLinesChanged != null) OnShowHiddenLinesChanged(showhiddenlines.Checked, EventArgs.Empty); };
			showsecretsectors.CheckedChanged += (s, e) => { if(OnShowSecretSectorsChanged != null) OnShowSecretSectorsChanged(showsecretsectors.Checked, EventArgs.Empty); };
			showlocks.CheckedChanged += (s, e) => { if(OnShowLocksChanged != null) OnShowLocksChanged(showlocks.Checked, EventArgs.Empty); };
			showtextures.CheckedChanged += (s, e) => { if(OnShowTexturesChanged != null) OnShowTexturesChanged(showtextures.Checked, EventArgs.Empty); };
			colorpreset.SelectedIndexChanged += (s, e) => { if(OnColorPresetChanged != null) OnColorPresetChanged(colorpreset.SelectedIndex, EventArgs.Empty); };
		}

		public void Dispose() { }

		public void Register()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.AddButton(showhiddenlines);
			General.Interface.AddButton(showsecretsectors);
			if(!General.Map.DOOM) General.Interface.AddButton(showlocks);
			General.Interface.AddButton(showtextures);
			General.Interface.AddButton(colorpresetseparator);
			General.Interface.AddButton(colorpresetlabel);
			General.Interface.AddButton(colorpreset);
			General.Interface.EndToolbarUpdate(); //mxd
		}

		public void Unregister()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.RemoveButton(colorpreset);
			General.Interface.RemoveButton(colorpresetlabel);
			General.Interface.RemoveButton(colorpresetseparator);
			General.Interface.RemoveButton(showlocks);
			General.Interface.RemoveButton(showtextures);
			General.Interface.RemoveButton(showsecretsectors);
			General.Interface.RemoveButton(showhiddenlines);
			General.Interface.EndToolbarUpdate(); //mxd
		}
	}
}
