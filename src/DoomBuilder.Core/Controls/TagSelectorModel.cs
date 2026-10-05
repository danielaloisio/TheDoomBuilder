using System;
using System.Collections.Generic;
using System.Globalization;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;

namespace CodeImp.DoomBuilder.Controls
{
	/// <summary>A tag in use, with the label the user gave it.</summary>
	public sealed class TagInfo
	{
		public readonly int Tag;
		public readonly string Label;
		public TagInfo(int tag, string label) { Tag = tag; Label = label; }
		public override string ToString() { return string.IsNullOrEmpty(Label) ? Tag.ToString() : Tag + " - " + Label; }
	}

	/// <summary>
	/// The logic of UDB's TagSelector without any UI: the tags in use for an element type, and what the user typed read as a tag.
	/// Besides a number it takes ">=N" / "&lt;=N" (consecutive tags, one more for each element), "++N" / "--N" (relative to each
	/// element's own tag) and a label (or a new text, which creates a new tag with that label).
	/// </summary>
	public sealed class TagSelectorModel
	{
		private List<int> tags = new List<int>();
		private readonly List<TagInfo> infos = new List<TagInfo>();
		private UniversalType elementtype;
		private bool valid;
		private int tag;
		private int rangemode;    // 0 none, 1 positive (>=), -1 negative (<=)
		private int offsetmode;   // 0 none, 1 positive (++), -1 negative (--)

		/// <summary>The text of the box (what the user typed or picked).</summary>
		public string Text { get; set; }

		/// <summary>The tags in use, highest first.</summary>
		public IList<TagInfo> Infos { get { return infos; } }

		public TagSelectorModel() { Text = ""; }

		public void Setup(UniversalType mapelementtype)
		{
			tags = new List<int>();
			infos.Clear();
			elementtype = mapelementtype;

			switch(elementtype)
			{
				case UniversalType.SectorTag:
					foreach(Sector s in General.Map.Map.Sectors)
						foreach(int t in s.Tags) if(t != 0 && !tags.Contains(t)) tags.Add(t);
					break;

				case UniversalType.LinedefTag:
					if(General.Map.FormatInterface.HasLinedefTag)
						foreach(Linedef l in General.Map.Map.Linedefs)
							foreach(int t in l.Tags) if(t != 0 && !tags.Contains(t)) tags.Add(t);
					break;

				case UniversalType.ThingTag:
					if(General.Map.FormatInterface.HasThingTag)
						foreach(Thing t in General.Map.Map.Things)
							if(t.Tag != 0 && !tags.Contains(t.Tag)) tags.Add(t.Tag);
					break;
			}

			tags.Sort((a, b) => -1 * a.CompareTo(b));
			foreach(int t in tags)
				infos.Add(new TagInfo(t, General.Map.Options.TagLabels.ContainsKey(t) ? General.Map.Options.TagLabels[t] : string.Empty));
		}

		public void SetTag(int newtag)
		{
			Text = newtag.ToString();
			tag = newtag;
			valid = true;
			rangemode = 0;
			offsetmode = 0;
		}

		/// <summary>The elements being edited have different tags: the box stays empty.</summary>
		public void ClearTag()
		{
			Text = "";
			rangemode = 0;
			offsetmode = 0;
			valid = false;
		}

		/// <summary>The tag to apply, or <paramref name="original"/> when nothing valid was given.</summary>
		public int GetTag(int original) { return valid ? tag : original; }

		/// <summary>The tag for the element at <paramref name="offset"/> in the list (ranges and relative values).</summary>
		public int GetSmartTag(int original, int offset)
		{
			if(!valid) return original;
			if(rangemode != 0) return tag + offset * rangemode;
			if(offsetmode != 0) return original + tag * offsetmode;
			return tag;
		}

		/// <summary>Reads <see cref="Text"/>; must be called before the tag is used.</summary>
		public void ValidateTag()
		{
			rangemode = 0;
			offsetmode = 0;

			string text = (Text ?? "").Trim().ToLowerInvariant();
			if(string.IsNullOrEmpty(text)) { valid = false; return; }

			// Picked from the list (shown as "tag - label")
			foreach(TagInfo info in infos)
				if(string.Equals(Text.Trim(), info.ToString(), StringComparison.OrdinalIgnoreCase)) { tag = info.Tag; valid = true; return; }

			if(text.Length > 2)
			{
				if(text.StartsWith(">=")) { rangemode = 1; text = text.Substring(2); }
				else if(text.StartsWith("<=")) { rangemode = -1; text = text.Substring(2); }
				else if(text.StartsWith("++")) { offsetmode = 1; text = text.Substring(2); }
				else if(text.StartsWith("--")) { offsetmode = -1; text = text.Substring(2); }
			}

			if(!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out tag))
			{
				// Maybe a label?
				foreach(TagInfo info in infos)
					if(info.Label.ToLowerInvariant().Contains(text)) { tag = info.Tag; valid = true; return; }

				// A new label: gets a new tag
				tag = General.Map.Map.GetNewTag();
				General.Map.Options.TagLabels[tag] = Text.Trim();
			}
			valid = true;
		}

		/// <summary>A tag not used by anything.</summary>
		public void NewTag() { SetTag(General.Map.Map.GetNewTag()); }

		/// <summary>A tag not used by this type of element.</summary>
		public void UnusedTag() { SetTag(General.Map.Map.GetNewTag(elementtype)); }

		public void Clear() { SetTag(0); }

		/// <summary>One more (direction +1) or one less (-1) than the current tag.</summary>
		public void Step(int direction)
		{
			ValidateTag();
			if(!valid) tag = 0;
			else tag = General.Clamp(tag + direction, General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
			SetTag(tag);
		}
	}

	/// <summary>What an action/effect selector shows for a number that is not in its list (UDB's ActionSelectorControl).</summary>
	public static class ActionSelectorLogic
	{
		/// <summary>The grey text for a number without a list entry; empty for an empty box.</summary>
		public static string DescribeUnknown(string numbertext, List<Config.GeneralizedCategory> categories, List<Config.GeneralizedOption> options)
		{
			if(string.IsNullOrEmpty(numbertext)) return "";
			int number;
			int.TryParse(numbertext, out number);
			if(number == 0) return "None";
			if(categories != null && Config.GameConfiguration.IsGeneralized(number, categories))
				return "Generalized (" + General.Map.Config.GetGeneralizedActionCategory(number) + ")";
			if(options != null && Config.GameConfiguration.IsGeneralizedSectorEffect(number, options))
				return General.Map.Config.GetGeneralizedSectorEffectName(number);
			return "Unknown";
		}
	}
}
