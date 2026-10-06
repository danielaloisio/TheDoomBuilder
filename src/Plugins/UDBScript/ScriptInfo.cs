#region ================== Copyright (c) 2021 Boris Iwanski

/*
 * This program is free software: you can redistribute it and/or modify
 *
 * it under the terms of the GNU General Public License as published by
 * 
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 *    but WITHOUT ANY WARRANTY; without even the implied warranty of
 * 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * 
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.If not, see<http://www.gnu.org/licenses/>.
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text.RegularExpressions;
using CodeImp.DoomBuilder.IO;

#endregion

namespace CodeImp.DoomBuilder.UDBScript
{
	public class ScriptInfo
	{
		#region ================== Variables

		private uint version;
		private bool ignoreversion;
		private string name;
		private string description;
		private string scriptfile;
		private List<ScriptOption> options;

		#endregion

		#region ================== Properties

		public uint Version { get { return version; } }
		public bool IgnoreVersion { get { return ignoreversion; } set { ignoreversion = value; } }
		public string Name { get { return name; } }
		public string Description { get { return description; } }
		public string ScriptFile { get { return scriptfile; } }
		public List<ScriptOption> Options { get { return options; } }

		#endregion

		#region ================== Constructor

		internal ScriptInfo(string file)
		{
			string data;

			this.scriptfile = file;

			// Set default values
			name = Path.GetFileNameWithoutExtension(file);
			description = "No description.";
			version = 1;
			options = new List<ScriptOption>();

			data = File.ReadAllText(file);

			string configstring = string.Empty;

			// The header of a script is a run of template strings (`#name ...`; `#description ...`;) before any code. They are read with a
			// small scanner of our own: comments and white space are skipped, a template string is a header line, a semicolon is skipped, and
			// anything else is where the code starts. (UDB used the scanner of Esprima for this.)
			int pos = 0;
			string templatestring;
			while (ReadHeaderToken(data, ref pos, out templatestring))
			{
				if (templatestring == null)
					continue;

				string tokenstring = templatestring.Trim();

				Match m = Regex.Match(tokenstring, @"\s*#([^\s]+)\s+(.*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

				if (!m.Success)
					continue;

				string command = m.Groups[1].Value;
				string payload = m.Groups[2].Value;

				switch (command)
				{
					case "scriptoptions":
						configstring = payload;
						break;
					case "name":
						name = Regex.Replace(payload, @"(\r\n?|\n)+", " ", RegexOptions.Singleline);
						break;
					case "description":
						description = Regex.Replace(payload, @"(\r\n?|\n)+", " ", RegexOptions.Singleline);
						break;
					case "version":
						if (!uint.TryParse(payload, out version))
							throw new ArgumentException("Invalid version value");
						if (version == 0)
							throw new ArgumentException("Version number has to be at least 1");
						break;
				}
			}

			if (!string.IsNullOrWhiteSpace(configstring))
				CreateOptions(configstring);
		}

		#endregion

		#region ================== Methods

		/// <summary>
		/// Reads the next token of the header of a script. Returns false when the header is over (the code starts or the file ends). The
		/// token is a template string (templatestring is its text), or a semicolon (templatestring is null).
		/// </summary>
		private static bool ReadHeaderToken(string data, ref int pos, out string templatestring)
		{
			templatestring = null;

			// Skip white space and comments
			while (pos < data.Length)
			{
				if (char.IsWhiteSpace(data[pos]) || data[pos] == '\uFEFF')
				{
					pos++;
				}
				else if (data[pos] == '/' && pos + 1 < data.Length && data[pos + 1] == '/')
				{
					while (pos < data.Length && data[pos] != '\n' && data[pos] != '\r') pos++;
				}
				else if (data[pos] == '/' && pos + 1 < data.Length && data[pos + 1] == '*')
				{
					int end = data.IndexOf("*/", pos + 2, StringComparison.Ordinal);
					pos = end < 0 ? data.Length : end + 2;
				}
				else
				{
					break;
				}
			}

			if (pos >= data.Length)
				return false;

			if (data[pos] == ';')
			{
				pos++;
				return true;
			}

			if (data[pos] == '`')
			{
				int start = pos + 1;
				int end = start;
				while (end < data.Length && data[end] != '`')
				{
					if (data[end] == '\\') end++;      // an escaped character
					end++;
				}
				if (end >= data.Length)
					return false;                       // not closed: not a header

				templatestring = data.Substring(start, end - start);
				pos = end + 1;
				return true;
			}

			return false;
		}

		/// <summary>
		/// Create script options from a config string
		/// </summary>
		/// <param name="configstring">string containing the config information</param>
		private void CreateOptions(string configstring)
		{
			Configuration cfg = new Configuration();
			cfg.InputConfiguration(configstring, true);

			if (cfg.ErrorResult)
				throw new ArgumentException("Error parsing script options: " + cfg.ErrorDescription);

			foreach(DictionaryEntry de in cfg.Root)
			{
				if (!(de.Value is ICollection))
					continue;

				string description = cfg.ReadSetting(string.Format("{0}.description", de.Key), "no description");
				int type = cfg.ReadSetting(string.Format("{0}.type", de.Key), 0);
				string defaultvaluestr = cfg.ReadSetting(string.Format("{0}.default", de.Key), string.Empty);
				IDictionary enumvalues = cfg.ReadSetting(string.Format("{0}.enumvalues", de.Key), new Hashtable());

				if (Array.FindIndex(ScriptOption.ValidTypes, t => (int)t == type) == -1)
				{
					string errordesc = "Error in script configuration of file " + scriptfile + ": option " + de.Key + " has invalid type " + type;
					General.ErrorLogger.Add(ErrorType.Error, errordesc);
					General.WriteLogLine(errordesc);
					continue;
				}

				ScriptOption so = new ScriptOption((string)de.Key, description, type, enumvalues, defaultvaluestr);

				string savedvalue = General.Settings.ReadPluginSetting("scripts." + GetScriptPathHash() + ".options." + so.name, so.defaultvalue.ToString());

				if (string.IsNullOrWhiteSpace(savedvalue))
					so.value = so.defaultvalue;
				else
					so.value = savedvalue;

				so.typehandler.SetValue(so.value);

				options.Add(so);
			}
		}

		/// <summary>
		/// Saves all script options to the config file
		/// </summary>
		public void SaveOptionValues()
		{
			if (options.Count == 0)
				return;

			string hash = GetScriptPathHash();
			int optionswritten = 0;

			foreach(ScriptOption so in options)
			{
				if (so.value.ToString() == so.defaultvalue.ToString())
					General.Settings.DeletePluginSetting("scripts." + hash + ".options." + so.name);
				else
				{
					General.Settings.WritePluginSetting("scripts." + hash + ".options." + so.name, so.value);
					optionswritten++;
				}
			}

			// If no options were written the whole block should be deleted
			if (optionswritten == 0)
			{
				General.Settings.DeletePluginSetting("scripts." + hash + ".options");
				General.Settings.DeletePluginSetting("scripts." + hash);
			}
		}

		/// <summary>
		/// Gets an object with all script options with their values. This can then be easily used to access script options by name in the script
		/// </summary>
		/// <returns>Object containing all script options with their values</returns>
		public ExpandoObject GetScriptOptionsObject()
		{
			// We have to jump through some hoops here to be able to access the elements by name
			ExpandoObject eo = new ExpandoObject();
			var opt = eo as IDictionary<string, object>;

			foreach (ScriptOption so in options)
			{
				opt[so.name] = so.typehandler.GetValue();
			}

			return eo;
		}

		/// <summary>
		/// Creates a SHA256 hash from the script file's path. Used as a section key to save the script's setting in the config file
		/// </summary>
		/// <returns>The hash as a string</returns>
		public string GetScriptPathHash()
		{
			return SHA256Hash.Get(scriptfile);
		}

		#endregion
	}
}
