using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeImp.DoomBuilder.Localization
{
	/// <summary>
	/// The texts of the editor in the user's language. The English text of the interface is the key, as in gettext:
	/// <c>Localizer.T("&amp;File")</c> returns the translation when the language file has one, and the same English text otherwise,
	/// so a missing translation only leaves English on the screen. Language files are <c>Languages/&lt;culture&gt;.json</c> next to the program:
	/// <c>{ "language": "Português (Brasil)", "strings": { "&amp;File": "&amp;Arquivo" } }</c>.
	/// </summary>
	public static class Localizer
	{
		public const string FolderName = "Languages";

		/// <summary>A language the editor has a file for.</summary>
		public sealed class Language
		{
			public string Code { get; internal set; }       // the culture name, e.g. "pt-BR"
			public string Name { get; internal set; }       // in its own language, e.g. "Português (Brasil)"
			public string File { get; internal set; }
		}

		private static Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
		private static string current = "en";

		// Texts with values in them ("Edit {0} linedefs"): the call sites build messages by concatenation, so a message that has no exact entry is
		// matched against these. The values are translated too ("Undo {0}" with an undo description).
		private sealed class Template
		{
			public Regex Pattern;
			public string Translation;
			public int Count;
		}
		private static List<Template> templates = new List<Template>();
		private static readonly Dictionary<string, string> templatecache = new Dictionary<string, string>(StringComparer.Ordinal); // text -> result ("" = no template fits)
		private const int MaxCache = 4096;
		[ThreadStatic] private static int depth;

		/// <summary>The code of the language in use ("en" when the texts are the English ones).</summary>
		public static string CurrentLanguage { get { return current; } }

		/// <summary>The languages found in the folder, by code.</summary>
		public static List<Language> Available(string appdirectory)
		{
			var result = new List<Language>();
			string folder = Path.Combine(appdirectory ?? "", FolderName);
			if(!Directory.Exists(folder)) return result;
			foreach(string file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
			{
				try
				{
					using(JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file)))
					{
						string name = doc.RootElement.TryGetProperty("language", out JsonElement n) ? n.GetString() : null;
						string code = Path.GetFileNameWithoutExtension(file);
						result.Add(new Language { Code = code, Name = string.IsNullOrEmpty(name) ? code : name, File = file });
					}
				}
				catch(Exception) { /* a broken file is not offered */ }
			}
			return result;
		}

		/// <summary>
		/// Chooses and loads the language. <paramref name="requested"/> is a code ("pt-BR"), or empty to follow the system.
		/// An exact match wins, then the same language without the country ("pt-PT" gets "pt" or any "pt-*"); English when nothing fits.
		/// </summary>
		public static string Load(string appdirectory, string requested)
		{
			Language chosen = Choose(Available(appdirectory), string.IsNullOrEmpty(requested) ? CultureInfo.CurrentUICulture.Name : requested);
			strings = new Dictionary<string, string>(StringComparer.Ordinal);
			templates = new List<Template>();
			lock(templatecache) { templatecache.Clear(); }
			current = "en";
			if(chosen == null) return current;

			try
			{
				using(JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(chosen.File)))
				{
					if(doc.RootElement.TryGetProperty("strings", out JsonElement list))
						foreach(JsonProperty p in list.EnumerateObject())
							if(p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(p.Value.GetString())) strings[p.Name] = p.Value.GetString();
				}
				templates = BuildTemplates(strings);
				current = chosen.Code;
			}
			catch(Exception e)
			{
				General.WriteLogLine("Unable to load the language file \"" + chosen.File + "\": " + e.Message);
				strings = new Dictionary<string, string>(StringComparer.Ordinal);
				templates = new List<Template>();
			}
			return current;
		}

		internal static Language Choose(List<Language> available, string culture)
		{
			if(string.IsNullOrEmpty(culture)) return null;
			Language exact = available.FirstOrDefault(l => string.Equals(l.Code, culture, StringComparison.OrdinalIgnoreCase));
			if(exact != null) return exact;
			string neutral = culture.Split('-')[0];
			if(string.Equals(neutral, "en", StringComparison.OrdinalIgnoreCase)) return null;
			return available.FirstOrDefault(l => string.Equals(l.Code, neutral, StringComparison.OrdinalIgnoreCase))
				?? available.FirstOrDefault(l => l.Code.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Back to the English texts.</summary>
		public static void Reset()
		{
			strings = new Dictionary<string, string>(StringComparer.Ordinal);
			templates = new List<Template>();
			lock(templatecache) { templatecache.Clear(); }
			current = "en";
		}

		private static List<Template> BuildTemplates(Dictionary<string, string> table)
		{
			var result = new List<Template>();
			foreach(KeyValuePair<string, string> pair in table)
			{
				MatchCollection holes = Regex.Matches(pair.Key, @"\{(\w+)\}");
				if(holes.Count == 0) continue;

				// Longest literal text first when two templates both match
				string pattern = "^" + Regex.Replace(Regex.Escape(pair.Key), @"\\\{(\w+)\}", m => "(?<v" + m.Groups[1].Value + ">.*?)") + "$";
				try
				{
					result.Add(new Template { Pattern = new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant), Translation = pair.Value, Count = holes.Count });
				}
				catch(ArgumentException) { /* a key that is not a usable pattern stays exact-only */ }
			}
			return result.OrderByDescending(t => t.Pattern.ToString().Length).ToList();
		}

		private static string ApplyTemplate(string text)
		{
			if(templates.Count == 0 || depth > 3) return null;
			lock(templatecache)
			{
				if(templatecache.TryGetValue(text, out string cached)) return cached.Length == 0 ? null : cached;
			}

			string result = MatchTemplates(text);
			if(depth == 0)
			{
				lock(templatecache)
				{
					if(templatecache.Count >= MaxCache) templatecache.Clear();
					templatecache[text] = result ?? "";
				}
			}
			return result;
		}

		private static string MatchTemplates(string text)
		{
			foreach(Template t in templates)
			{
				Match m = t.Pattern.Match(text);
				if(!m.Success) continue;

				depth++;
				try
				{
					return Regex.Replace(t.Translation, @"\{(\w+)\}", h =>
					{
						Group g = m.Groups["v" + h.Groups[1].Value];
						if(!g.Success) return h.Value;

						// The value may be a text of its own (" linedefs", "SHOWN"): translate it without its surrounding spaces
						string value = g.Value, trimmed = value.Trim();
						if(trimmed.Length == 0) return value;
						int lead = value.IndexOf(trimmed, StringComparison.Ordinal);
						return value.Substring(0, lead) + T(trimmed) + value.Substring(lead + trimmed.Length);
					});
				}
				finally { depth--; }
			}
			return null;
		}

		/// <summary>The text in the language in use; the same text when there is no translation.</summary>
		public static string T(string text)
		{
			if(string.IsNullOrEmpty(text)) return text;
			if(strings.TryGetValue(text, out string translated)) return translated;
			return ApplyTemplate(text) ?? text;
		}

		/// <summary>
		/// The translation of a text that has an entry of its own, or null. Unlike <see cref="T(string)"/> it never matches a template, so it is safe for
		/// text that may be user data. Access keys are matched in either style: "&amp;File" (WinForms) and "_File" (Avalonia).
		/// </summary>
		public static string Exact(string text)
		{
			if(string.IsNullOrEmpty(text) || strings.Count == 0) return null;
			if(strings.TryGetValue(text, out string translated)) return translated;

			int amp = text.IndexOf('&'), und = text.IndexOf('_');
			if(und >= 0 && amp < 0 && strings.TryGetValue(text.Remove(und, 1).Insert(und, "&"), out translated)) return translated.Replace('&', '_');
			if(amp >= 0 && und < 0 && strings.TryGetValue(text.Remove(amp, 1).Insert(amp, "_"), out translated)) return translated.Replace('_', '&');
			return null;
		}

		/// <summary>A translated text with values in it ({0}, {1}...).</summary>
		public static string T(string text, params object[] args)
		{
			return string.Format(CultureInfo.CurrentCulture, T(text), args);
		}
	}
}
