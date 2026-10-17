#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Editing;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>One entry of the list of game configurations: a working copy that only reaches <c>General.Configs</c> on Apply.</summary>
	internal sealed class ConfigEntry
	{
		public ConfigurationInfo Info { get; private set; }
		public string Name { get { return Info.Name; } }
		public bool Enabled { get { return Info.Enabled; } set { Info.Enabled = value; } }
		internal ConfigEntry(ConfigurationInfo info) { Info = info; }
		public override string ToString() { return Info.Name; }
	}

	/// <summary>An editing mode the user may switch on or off for a configuration.</summary>
	internal sealed class ModeChoice
	{
		public EditModeInfo Info { get; private set; }
		public string Text { get; internal set; }
		public bool Enabled { get; internal set; }
		/// <summary>False when the configuration's map format or features cannot use the mode: shown greyed out and off.</summary>
		public bool Supported { get; internal set; }
		internal ModeChoice(EditModeInfo info) { Info = info; }
		public override string ToString() { return Text; }
	}

	/// <summary>
	/// The logic of UDB's "Game Configurations" dialog (ConfigForm) without any UI: the configurations with their resources,
	/// nodebuilders, test program and editing modes. Everything is edited on copies; <see cref="Apply"/> writes them back.
	/// Not covered yet: texture sets, several test engines per configuration, copy/paste between configurations.
	/// </summary>
	internal sealed class ConfigModel
	{
		private readonly List<ConfigEntry> entries = new List<ConfigEntry>();
		private readonly List<ModeChoice> modes = new List<ModeChoice>();
		private ConfigEntry selected;

		public ConfigModel()
		{
			foreach(ConfigurationInfo ci in General.Configs) entries.Add(new ConfigEntry(ci.Clone()));

			// Nodebuilders are allowed to be empty
			Nodebuilders = new List<NodebuilderInfo> { new NodebuilderInfo() };
			Nodebuilders.AddRange(General.Nodebuilders);

			foreach(EditModeInfo emi in General.Editing.ModesInfo)
				if(emi.IsOptional) modes.Add(new ModeChoice(emi));
		}

		#region ================== Properties

		public IReadOnlyList<ConfigEntry> Entries { get { return entries; } }
		public List<NodebuilderInfo> Nodebuilders { get; private set; }
		public IReadOnlyList<ModeChoice> Modes { get { return modes; } }
		public ConfigEntry Selected { get { return selected; } }
		public GameConfiguration Game { get; private set; }

		/// <summary>True when the resources of a configuration changed, so the open map must reload them.</summary>
		public bool ReloadResources { get; private set; }

		/// <summary>The configuration of the open map, which the dialog selects first.</summary>
		public ConfigEntry CurrentMapEntry
		{
			get { return General.Map == null ? null : entries.FirstOrDefault(e => e.Info.Filename == General.Map.ConfigSettings.Filename); }
		}

		#endregion

		#region ================== Selection

		/// <summary>Selects a configuration (null for none): loads its game configuration and fills in the defaults.</summary>
		public void Select(ConfigEntry entry)
		{
			selected = entry;
			if(entry == null) { Game = null; return; }

			Game = new GameConfiguration(entry.Info.Configuration);
			entry.Info.ApplyDefaults(Game);

			foreach(ModeChoice mode in modes)
			{
				EditModeAttribute a = mode.Info.Attributes;
				string deprecated = a.IsDeprecated ? ", deprecated" : "";
				if(a.SupportedMapFormats != null && Array.IndexOf(a.SupportedMapFormats, Game.FormatInterface) == -1)
				{
					mode.Text = a.DisplayName + " (map format not supported" + deprecated + ")";
					mode.Supported = false;
					mode.Enabled = false;
				}
				else if(a.RequiredMapFeatures != null && !Game.SupportsMapFeatures(a.RequiredMapFeatures))
				{
					mode.Text = a.DisplayName + " (map feature not supported)";
					mode.Supported = false;
					mode.Enabled = false;
				}
				else
				{
					mode.Text = a.DisplayName + (a.IsDeprecated ? " (deprecated)" : "");
					mode.Supported = true;
					mode.Enabled = entry.Info.EditModes.ContainsKey(mode.Info.Type.FullName) && entry.Info.EditModes[mode.Info.Type.FullName];
				}
			}
			EnsureStartMode();
		}

		#endregion

		#region ================== Resources

		public DataLocationList Resources { get { return selected == null ? new DataLocationList() : selected.Info.Resources; } }

		/// <summary>Replaces the resources of the selected configuration.</summary>
		public void SetResources(IEnumerable<DataLocation> resources)
		{
			if(selected == null) return;
			List<DataLocation> copy = resources.ToList();       // the caller may pass the very list being replaced
			selected.Info.Resources.Clear();
			selected.Info.Resources.AddRange(copy);
			selected.Info.Changed = true;
			ReloadResources = true;
		}

		#endregion

		#region ================== Nodebuilders

		public NodebuilderInfo NodebuilderSave { get { return selected == null ? null : Nodebuilders.FirstOrDefault(n => n.Name == selected.Info.NodebuilderSave); } }
		public NodebuilderInfo NodebuilderTest { get { return selected == null ? null : Nodebuilders.FirstOrDefault(n => n.Name == selected.Info.NodebuilderTest); } }

		public void SetNodebuilderSave(NodebuilderInfo info)
		{
			if(selected == null || info == null) return;
			selected.Info.NodebuilderSave = info.Name;
			selected.Info.Changed = true;
		}

		public void SetNodebuilderTest(NodebuilderInfo info)
		{
			if(selected == null || info == null) return;
			selected.Info.NodebuilderTest = info.Name;
			selected.Info.Changed = true;
		}

		#endregion

		#region ================== Testing

		public string TestProgram { get { return selected == null ? "" : selected.Info.TestProgram ?? ""; } }
		public string TestEngineName { get { return selected == null ? "" : selected.Info.TestProgramName ?? ""; } }
		public string TestParameters { get { return selected == null ? "" : selected.Info.TestParameters ?? ""; } }
		public bool CustomParameters { get { return selected != null && selected.Info.CustomParameters; } }
		public bool ShortPaths { get { return selected != null && selected.Info.TestShortPaths; } }
		public bool LinuxPaths { get { return selected != null && selected.Info.TestLinuxPaths; } }
		public int Skill { get { return selected == null ? 0 : selected.Info.TestSkill; } }

		/// <summary>The program that runs a test map. The engine is named after its folder (or its file when it has none).</summary>
		public void SetTestProgram(string path)
		{
			if(selected == null) return;
			selected.Info.TestProgram = path ?? "";
			selected.Info.TestProgramName = EngineNameOf(path);
			selected.Info.Changed = true;
		}

		/// <summary>The name UDB gives an engine from where it lives: "C:\Games\GZDoom\gzdoom.exe" is "GZDoom".</summary>
		public static string EngineNameOf(string path)
		{
			if(string.IsNullOrEmpty(path)) return "";

			// Both separators count, whatever platform this runs on (settings are shared between them)
			string p = path.Replace('\\', '/');
			int last = p.LastIndexOf('/');
			if(last > 0)
			{
				string folder = p.Substring(0, last);
				int pos = folder.LastIndexOf('/');
				return pos != -1 ? folder.Substring(pos + 1) : folder;
			}
			return Path.GetFileNameWithoutExtension(p);
		}

		public void SetTestParameters(string parameters)
		{
			if(selected == null) return;
			selected.Info.TestParameters = parameters ?? "";
			selected.Info.Changed = true;
		}

		public void SetCustomParameters(bool value)
		{
			if(selected == null) return;
			selected.Info.CustomParameters = value;
			selected.Info.Changed = true;
		}

		/// <summary>Short (8.3) paths and Linux paths exclude each other: turning one on turns the other off.</summary>
		public void SetShortPaths(bool value)
		{
			if(selected == null) return;
			selected.Info.TestShortPaths = value;
			if(value) selected.Info.TestLinuxPaths = false;
			selected.Info.Changed = true;
		}

		public void SetLinuxPaths(bool value)
		{
			if(selected == null) return;
			selected.Info.TestLinuxPaths = value;
			if(value) selected.Info.TestShortPaths = false;
			selected.Info.Changed = true;
		}

		public void SetSkill(int skill)
		{
			if(selected == null) return;
			selected.Info.TestSkill = skill;
			selected.Info.Changed = true;
		}

		/// <summary>What the parameters become for the open map (null when there is no map to show it for).</summary>
		public string ParametersExample()
		{
			if(General.Map == null || selected == null) return null;
			return General.Map.Launcher.ConvertParameters(TestParameters, Skill, ShortPaths, LinuxPaths);
		}

		#endregion

		#region ================== Editing modes

		/// <summary>The modes that may start the editor: enabled ones that are safe to start in.</summary>
		public List<ModeChoice> StartModes
		{
			get { return modes.Where(m => m.Enabled && m.Info.Attributes.SafeStartMode).ToList(); }
		}

		public ModeChoice StartMode
		{
			get { return selected == null ? null : modes.FirstOrDefault(m => m.Info.Type.Name == selected.Info.StartMode); }
		}

		public void SetModeEnabled(ModeChoice mode, bool enabled)
		{
			if(selected == null || !mode.Supported) return;
			mode.Enabled = enabled;
			selected.Info.EditModes[mode.Info.Type.FullName] = enabled;
			selected.Info.Changed = true;
			EnsureStartMode();
		}

		public void SetStartMode(ModeChoice mode)
		{
			if(selected == null || mode == null) return;
			selected.Info.StartMode = mode.Info.Type.Name;
			selected.Info.Changed = true;
		}

		// The start mode must be one that is enabled; when it is not, the first one that is
		private void EnsureStartMode()
		{
			if(selected == null) return;
			ModeChoice current = StartMode;
			if(current != null && current.Enabled) return;
			List<ModeChoice> candidates = StartModes;
			if(candidates.Count == 0) return;
			selected.Info.StartMode = candidates[0].Info.Type.Name;
			selected.Info.Changed = true;
		}

		#endregion

		#region ================== Apply

		/// <summary>The first enabled configuration with a resource that does not exist, or null when all are fine.</summary>
		public ConfigEntry FindInvalidResources()
		{
			return entries.FirstOrDefault(e => e.Enabled && !e.Info.Resources.IsValid());
		}

		/// <summary>Writes the changes to the real configurations and saves them.</summary>
		public void Apply()
		{
			for(int i = 0; i < entries.Count; i++)
			{
				ConfigurationInfo ci = entries[i].Info;
				General.Configs[i].Enabled = ci.Enabled;
				if(ci.Changed) General.Configs[i].Apply(ci);
			}
			General.SaveGameSettings();
		}

		#endregion
	}
}
