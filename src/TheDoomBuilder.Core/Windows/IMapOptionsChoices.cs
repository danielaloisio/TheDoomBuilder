using System.Collections.Generic;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// What the open-map, new-map and map-options dialogs have in common: the game configuration, its script compiler, a few
	/// flags and the resources to load. A single dialog layout edits any of the models through this.
	/// </summary>
	public interface IMapOptionsChoices
	{
		IReadOnlyList<ConfigurationInfo> Configs { get; }
		ConfigurationInfo SelectedConfig { get; set; }

		IReadOnlyDictionary<string, ScriptConfiguration> ScriptCompilers { get; }
		string SelectedScriptCompiler { get; set; }
		bool ScriptCompilerAvailable { get; }

		bool StrictPatches { get; set; }
		bool LongTextureNamesAvailable { get; }
		bool UseLongTextureNames { get; set; }

		/// <summary>What the configuration always loads (shown, not editable).</summary>
		DataLocationList FixedResources { get; }

		/// <summary>The map's own extra resources (editable).</summary>
		DataLocationList Resources { get; }
	}
}
