using System;
using System.Collections.Generic;
using System.Globalization;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.GZBuilder;
using CodeImp.DoomBuilder.GZBuilder.Data;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>One line of an info panel: a caption and a value. A disabled one is shown dimmed (a value that is not set).</summary>
	public sealed class InfoField
	{
		public string Label;
		public string Value;
		public bool Enabled = true;
		public bool Highlight;
		/// <summary>Draws the value in the error color (an unknown script).</summary>
		public bool Error;
		/// <summary>An ARGB color swatch to show next to the value (UDMF light/fade colors), or null.</summary>
		public int? Color;

		public InfoField(string label, string value, bool enabled = true) { Label = label; Value = value; Enabled = enabled; }
	}

	/// <summary>A texture or flat of an info panel: its name, size, image and the UDMF values that belong to it.</summary>
	public sealed class InfoTexture
	{
		public string Caption;
		public string Name = "";
		/// <summary>"64x128" once the image is loaded (it is read when asked, so a refresh after loading shows it); empty before or when sizes are off.</summary>
		public string SizeText { get { return Image == null ? "" : ElementInfoBuilder.SizeText(Image); } }
		/// <summary>The image, or null when there is none to show (a "-" that is not required).</summary>
		public ImageData Image;
		/// <summary>No texture where one is needed: the "missing texture" picture is shown.</summary>
		public bool Missing;
		public bool Highlight;
		public List<InfoField> Fields = new List<InfoField>();
	}

	/// <summary>A framed part of an info panel (the element itself, a sidedef, the floor...).</summary>
	public sealed class InfoGroup
	{
		public string Title;
		public bool Highlight;
		public List<InfoField> Fields = new List<InfoField>();
		public List<InfoTexture> Textures = new List<InfoTexture>();
	}

	/// <summary>What the info panel shows for a map element (UDB's Linedef/Sector/Thing/VertexInfoPanel, without the controls).</summary>
	public sealed class ElementInfo
	{
		public MapElementType Kind;
		public string Title;
		public List<InfoGroup> Groups = new List<InfoGroup>();
		/// <summary>The flags that are set (and the activations of a linedef).</summary>
		public List<string> Flags = new List<string>();
		/// <summary>The sprite of a thing, or null.</summary>
		public ImageData Sprite;
		public bool SpriteIsInternal;
		public string SpriteName = "";
		/// <summary>The angle of a thing in degrees (for the dial), or -1.</summary>
		public int Angle = -1;
		/// <summary>A short text for the collapsed panel (a linedef's action).</summary>
		public string Summary = "";

		public bool IsComplete()
		{
			foreach(InfoGroup g in Groups)
				foreach(InfoTexture t in g.Textures)
					if(t.Image != null && !t.Image.IsPreviewLoaded) return false;
			return Sprite == null || Sprite.IsPreviewLoaded;
		}
	}

	/// <summary>Builds the <see cref="ElementInfo"/> of a vertex, linedef, sector or thing.</summary>
	public static class ElementInfoBuilder
	{
		private static string Num(double v) { return v.ToString(CultureInfo.InvariantCulture); }

		private static string TextureName(string name)
		{
			return name.Length > DataManager.CLASIC_IMAGE_NAME_LENGTH ? name : name.ToUpperInvariant();
		}

		private static string TagText(int tag)
		{
			return tag + (General.Map.Options.TagLabels.ContainsKey(tag) ? " - " + General.Map.Options.TagLabels[tag] : string.Empty);
		}

		private static void AddTags(InfoGroup group, List<int> tags, int tag)
		{
			if(tags.Count > 1)
			{
				var texts = new string[tags.Count];
				for(int i = 0; i < tags.Count; i++) texts[i] = tags[i].ToString();
				group.Fields.Add(new InfoField("Tags:", string.Join(", ", texts)));
			}
			else group.Fields.Add(new InfoField("Tag:", TagText(tag), tag != 0));
		}

		#region ================== Vertex

		public static ElementInfo ForVertex(Vertex v)
		{
			var info = new ElementInfo { Kind = MapElementType.VERTEX, Title = "Vertex " + v.Index };
			var group = new InfoGroup { Title = info.Title };
			group.Fields.Add(new InfoField("Position:", Num(v.Position.x) + ", " + Num(v.Position.y)));

			// Height offsets
			if(General.Map.UDMF)
			{
				bool have = !double.IsNaN(v.ZCeiling);
				group.Fields.Add(new InfoField("Ceiling Z:", have ? Num(v.ZCeiling) : "--", have));
				have = !double.IsNaN(v.ZFloor);
				group.Fields.Add(new InfoField("Floor Z:", have ? Num(v.ZFloor) : "--", have));
			}

			info.Groups.Add(group);
			return info;
		}

		#endregion

		#region ================== Sector

		public static ElementInfo ForSector(Sector s, bool highlightceiling, bool highlightfloor)
		{
			var info = new ElementInfo { Kind = MapElementType.SECTOR };
			info.Title = "Sector " + s.Index + " (" + (s.Sidedefs == null ? "no" : s.Sidedefs.Count.ToString()) + " sidedefs)";

			var group = new InfoGroup { Title = info.Title };
			group.Fields.Add(new InfoField("Effect:", s.Effect + " - " + General.Map.Config.GetSectorEffectInfo(s.Effect).Title, s.Effect != 0));
			AddTags(group, s.Tags, s.Tag);
			group.Fields.Add(new InfoField("Ceiling:", s.CeilHeight.ToString()) { Highlight = highlightceiling });
			group.Fields.Add(new InfoField("Floor:", s.FloorHeight.ToString()) { Highlight = highlightfloor });
			group.Fields.Add(new InfoField("Height:", (s.CeilHeight - s.FloorHeight).ToString()));
			group.Fields.Add(new InfoField("Brightness:", s.Brightness.ToString()));

			bool udmf = General.Map.UDMF && s.Fields != null;
			if(udmf)
			{
				bool hascolor = s.Fields.ContainsKey("lightcolor");
				group.Fields.Add(new InfoField("Light:", hascolor ? "" : "--", hascolor) { Color = hascolor ? (PixelColorArgb(s.Fields.GetValue("lightcolor", 0xFFFFFF))) : (int?)null });
				bool hasfade = s.Fields.ContainsKey("fadecolor");
				group.Fields.Add(new InfoField("Fade:", hasfade ? "" : "--", hasfade) { Color = hasfade ? (PixelColorArgb(s.Fields.GetValue("fadecolor", 0))) : (int?)null });
			}
			info.Groups.Add(group);

			info.Groups.Add(SectorSurface("Ceiling", s.CeilTexture, s.LongCeilTexture, udmf ? s : null, "ceiling", highlightceiling));
			info.Groups.Add(SectorSurface("Floor", s.FloorTexture, s.LongFloorTexture, udmf ? s : null, "floor", highlightfloor));

			// Flags
			if(General.Map.UDMF)
			{
				foreach(KeyValuePair<string, string> flag in General.Map.Config.SectorFlags)
					if(s.Flags.ContainsKey(flag.Key) && s.Flags[flag.Key]) info.Flags.Add(flag.Value);
				foreach(KeyValuePair<string, string> flag in General.Map.Config.CeilingPortalFlags)
					if(s.Flags.ContainsKey(flag.Key) && s.Flags[flag.Key]) info.Flags.Add(flag.Value + " (ceil. portal)");
				foreach(KeyValuePair<string, string> flag in General.Map.Config.FloorPortalFlags)
					if(s.Flags.ContainsKey(flag.Key) && s.Flags[flag.Key]) info.Flags.Add(flag.Value + " (floor portal)");
			}
			return info;
		}

		private static int PixelColorArgb(int rgb) { return unchecked((int)0xFF000000) | (rgb & 0xFFFFFF); }

		// The ceiling or the floor: the flat and, in UDMF, what has been set on it
		private static InfoGroup SectorSurface(string title, string name, long longname, Sector udmf, string surface, bool highlight)
		{
			var group = new InfoGroup { Title = title, Highlight = highlight };
			InfoTexture tex = new InfoTexture { Caption = title, Name = TextureName(name), Highlight = highlight };
			if(longname == MapSet.EmptyLongName) tex.Missing = true;
			else
			{
				ImageData image = General.Map.Data.GetFlatImage(name);
				tex.Image = image;
			}
			group.Textures.Add(tex);

			if(udmf != null)
			{
				UniFields f = udmf.Fields;
				string light = "light" + surface;
				if(f.ContainsKey(light) || f.ContainsKey(light + "absolute"))
				{
					int value = f.GetValue(light, 0);
					tex.Fields.Add(new InfoField("Light:", f.GetValue(light + "absolute", false) ? value + " (abs.)" : value + " (" + Math.Min(255, Math.Max(0, value + udmf.Brightness)) + ")"));
				}

				double panx = f.GetValue("xpanning" + surface, 0.0), pany = f.GetValue("ypanning" + surface, 0.0);
				if(panx != 0 || pany != 0) tex.Fields.Add(new InfoField("Offset:", Num(panx) + ", " + Num(pany)));

				double scalex = f.GetValue("xscale" + surface, 1.0), scaley = f.GetValue("yscale" + surface, 1.0);
				if(scalex != 1.0 || scaley != 1.0) tex.Fields.Add(new InfoField("Scale:", Num(scalex) + ", " + Num(scaley)));

				double angle = f.GetValue("rotation" + surface, 0.0);
				if(angle != 0.0) tex.Fields.Add(new InfoField("Angle:", angle + "°"));
			}
			return group;
		}

		internal static string SizeText(ImageData image)
		{
			if(General.Settings.ShowTextureSizes && image.ImageState == ImageLoadState.Ready && !string.IsNullOrEmpty(image.Name) && !(image is UnknownImage))
				return Math.Abs(image.ScaledWidth) + "x" + Math.Abs(image.ScaledHeight);
			return "";
		}

		#endregion

		#region ================== Linedef

		public static ElementInfo ForLinedef(Linedef l, Sidedef highlightside)
		{
			var info = new ElementInfo { Kind = MapElementType.LINEDEF, Title = "Linedef " + l.Index };
			LinedefActionInfo act = General.Map.Config.GetLinedefActionInfo(l.Action);
			info.Summary = General.Map.Config.LinedefActions.ContainsKey(l.Action) ? General.Map.Config.LinedefActions[l.Action].ToString()
				: (l.Action == 0 ? l.Action + " - None" : l.Action + " - Unknown");

			var group = new InfoGroup { Title = info.Title };
			group.Fields.Add(new InfoField("Action:", act.ToString(), act.Index != 0));

			// Hexen: activation. UDMF: lock number. Doom: neither. Tags are not shown in Hexen format
			if(!General.Map.FormatInterface.HasBuiltInActivations && General.Map.FormatInterface.HasNumericLinedefActivations)
			{
				string activation = "";
				foreach(LinedefActivateInfo ai in General.Map.Config.LinedefActivates)
					if(l.Activate == ai.Index) { activation = ai.Title; break; }
				group.Fields.Add(new InfoField("Activation:", activation, l.Activate != 0 || l.Action != 0));
			}
			else
			{
				if(General.Map.UDMF)
				{
					int locknum = l.Fields.GetValue("locknumber", 0);
					string text = "None";
					if(locknum != 0)
					{
						text = locknum.ToString();
						if(General.Map.Config.Enums.ContainsKey("keys"))
							foreach(EnumItem item in General.Map.Config.Enums["keys"])
								if(item.GetIntValue() == locknum) { text = locknum + " - " + item.Title; break; }
					}
					group.Fields.Add(new InfoField("Lock:", text, locknum != 0));
				}
				AddTags(group, l.Tags, l.Tag);
			}

			group.Fields.Add(new InfoField("Length:", l.Length.ToString("0.##")));
			group.Fields.Add(new InfoField("Angle:", l.AngleDeg + "°"));

			bool upper = l.IsFlagSet(General.Map.Config.UpperUnpeggedFlag), lower = l.IsFlagSet(General.Map.Config.LowerUnpeggedFlag);
			string pegged = upper && lower ? "Upper & Lower" : (upper ? "Upper" : (lower ? "Lower" : "None"));
			group.Fields.Add(new InfoField("Unpegged:", pegged, pegged != "None"));

			if(General.Map.FormatInterface.HasActionArgs) group.Fields.AddRange(ArgumentFields(act.Args, l.Args, l.Action, l.Fields));
			info.Groups.Add(group);

			if(l.Front != null) info.Groups.Add(SidedefGroup("Front", l.Front, highlightside));
			if(l.Back != null) info.Groups.Add(SidedefGroup("Back", l.Back, highlightside));

			// Activations and flags
			foreach(LinedefActivateInfo ai in General.Map.Config.LinedefActivates)
				if(l.Flags.ContainsKey(ai.Key) && l.Flags[ai.Key]) info.Flags.Add(ai.Title);
			foreach(KeyValuePair<string, string> flag in General.Map.Config.LinedefFlags)
				if(l.Flags.ContainsKey(flag.Key) && l.Flags[flag.Key]) info.Flags.Add(flag.Value);
			if(l.Front != null)
				foreach(KeyValuePair<string, string> flag in General.Map.Config.SidedefFlags)
					if(l.Front.Flags.ContainsKey(flag.Key) && l.Front.Flags[flag.Key]) info.Flags.Add("Front: " + flag.Value);
			if(l.Back != null)
				foreach(KeyValuePair<string, string> flag in General.Map.Config.SidedefFlags)
					if(l.Back.Flags.ContainsKey(flag.Key) && l.Back.Flags[flag.Key]) info.Flags.Add("Back: " + flag.Value);
			return info;
		}

		private static InfoGroup SidedefGroup(string side, Sidedef sd, Sidedef highlightside)
		{
			bool highlight = sd == highlightside;
			var group = new InfoGroup { Title = side + " Sidedef " + sd.Index, Highlight = highlight };

			if(General.Map.UDMF)
			{
				group.Title += ". Offset " + sd.OffsetX + ", " + sd.OffsetY + ". Sector " + sd.Sector.Index;
				if(sd.Fields.ContainsKey("light"))
				{
					int light = (int)sd.Fields["light"].Value;
					group.Fields.Add(new InfoField(side + " light:", sd.Fields.GetValue("lightabsolute", false) ? light + " (abs.)" : light + " (" + Math.Min(255, Math.Max(0, light + sd.Sector.Brightness)) + ")") { Highlight = highlight });
				}
				else group.Fields.Add(new InfoField(side + " light:", "-- (" + sd.Sector.Brightness + ")", highlight) { Highlight = highlight });
			}
			else
			{
				group.Title += ". Sector " + sd.Sector.Index;
				if(sd.OffsetX != 0 || sd.OffsetY != 0) group.Fields.Add(new InfoField(side + " offset:", sd.OffsetX + ", " + sd.OffsetY) { Highlight = highlight });
				else group.Fields.Add(new InfoField(side + " offset:", "--, --", false));
			}

			group.Textures.Add(SidedefTexture("Upper", sd.HighTexture, sd.HighRequired(), sd.Fields, "top", highlight));
			group.Textures.Add(SidedefTexture("Middle", sd.MiddleTexture, sd.MiddleRequired(), sd.Fields, "mid", highlight));
			group.Textures.Add(SidedefTexture("Lower", sd.LowTexture, sd.LowRequired(), sd.Fields, "bottom", highlight));
			return group;
		}

		private static InfoTexture SidedefTexture(string caption, string name, bool required, UniFields fields, string part, bool highlight)
		{
			var tex = new InfoTexture { Caption = caption, Name = TextureName(name), Highlight = highlight };

			if(name.Length < 1 || name == "-") tex.Missing = required;
			else
			{
				ImageData image = General.Map.Data.GetTextureImage(name);
				tex.Image = image;
			}

			if(General.Map.UDMF)
			{
				AddPair(tex, fields, "Offset:", "offsetx_" + part, "offsety_" + part, 0.0, highlight);
				AddPair(tex, fields, "Scale:", "scalex_" + part, "scaley_" + part, 1.0, highlight);
			}
			return tex;
		}

		// A paired UDMF value is only listed when it differs from its default
		private static void AddPair(InfoTexture tex, UniFields fields, string label, string paramx, string paramy, double defaultvalue, bool highlight)
		{
			double x = UniFields.GetFloat(fields, paramx, defaultvalue);
			double y = UniFields.GetFloat(fields, paramy, defaultvalue);
			if(x != defaultvalue || y != defaultvalue)
				tex.Fields.Add(new InfoField(label, Num(x) + ", " + Num(y)));
		}

		#endregion

		#region ================== Thing

		public static ElementInfo ForThing(Thing t)
		{
			var info = new ElementInfo { Kind = MapElementType.THING, Title = "Thing " + t.Index };
			ThingTypeInfo ti = General.Map.Data.GetThingInfo(t.Type);

			LinedefActionInfo act;
			if(General.Map.Config.LinedefActions.ContainsKey(t.Action)) act = General.Map.Config.LinedefActions[t.Action];
			else if(t.Action == 0) act = new LinedefActionInfo(0, "None", true, false);
			else act = new LinedefActionInfo(t.Action, "Unknown", false, false);

			// The height: absolute, or relative to the floor or ceiling of the sector it is in
			t.DetermineSector();
			string zinfo;
			if(ti.AbsoluteZ || t.Sector == null) zinfo = Num(t.Position.z) + " (abs.)";
			else if(ti.Hangs) zinfo = t.Position.z + " (" + Num(Math.Round(Sector.GetCeilingPlane(t.Sector).GetZ(t.Position) - t.Position.z - ti.Height, General.Map.FormatInterface.VertexDecimals)) + ")";
			else zinfo = t.Position.z + " (" + Num(Math.Round(Sector.GetFloorPlane(t.Sector).GetZ(t.Position) + t.Position.z, General.Map.FormatInterface.VertexDecimals)) + ")";

			var group = new InfoGroup { Title = info.Title };
			group.Fields.Add(new InfoField("Type:", t.Type + " - " + ti.Title + (ti.IsObsolete ? " - OBSOLETE" : "")));
			if(General.Map.FormatInterface.HasThingAction) group.Fields.Add(new InfoField("Action:", act.ToString(), act.Index != 0));
			bool displayclassname = !string.IsNullOrEmpty(ti.ClassName) && !ti.ClassName.StartsWith("$");
			group.Fields.Add(new InfoField("Class:", displayclassname ? ti.ClassName : "--", displayclassname));
			group.Fields.Add(new InfoField("Position:", Num(t.Position.x) + ", " + Num(t.Position.y) + ", " + zinfo));
			group.Fields.Add(new InfoField("Tag:", TagText(t.Tag), t.Tag != 0));
			group.Fields.Add(new InfoField("Angle:", t.AngleDoom + "°"));
			info.Angle = t.AngleDoom;

			if(General.Map.FormatInterface.HasActionArgs)
			{
				ArgumentInfo[] arginfo = ((t.Action == 0 && ti.Args[0] != null) ? ti.Args : act.Args);
				group.Fields.AddRange(ArgumentFields(arginfo, t.Args, t.Action, t.Fields));
			}
			info.Groups.Add(group);

			// The sprite
			if(ti.Sprite.ToLowerInvariant().StartsWith(DataManager.INTERNAL_PREFIX) && ti.Sprite.Length > DataManager.INTERNAL_PREFIX.Length)
			{
				info.Sprite = General.Map.Data.GetSpriteImage(ti.Sprite);
				info.SpriteIsInternal = true;
			}
			else if(ti.Sprite.Length <= 8 && ti.Sprite.Length > 0)
			{
				info.Sprite = General.Map.Data.GetSpriteImage(ti.Sprite);
				info.SpriteName = ti.Sprite;
			}

			// Flags (a flag the thing type renames is highlighted by the panel)
			Dictionary<string, string> rename = ti.FlagsRename;
			foreach(KeyValuePair<string, string> flag in General.Map.Config.ThingFlags)
			{
				if(!t.Flags.ContainsKey(flag.Key) || !t.Flags[flag.Key]) continue;
				info.Flags.Add(rename != null && rename.ContainsKey(flag.Key) ? rename[flag.Key] : flag.Value);
			}
			return info;
		}

		#endregion

		#region ================== Arguments

		// The five arguments of a linedef or thing action, with the names of ACS script arguments when the script is known
		private static List<InfoField> ArgumentFields(ArgumentInfo[] arginfo, int[] args, int action, UniFields fields)
		{
			var result = new List<InfoField>();
			bool isacsscript = (Array.IndexOf(GZGeneral.ACS_SPECIALS, action) != -1);
			bool isarg0str = (General.Map.UDMF && fields.ContainsKey("arg0str"));
			string arg0str = isarg0str ? fields.GetValue("arg0str", string.Empty) : string.Empty;
			ScriptItem scriptitem = null;

			// Named script?
			if(isacsscript && isarg0str && General.Map.NamedScripts.ContainsKey(arg0str.ToLowerInvariant()))
				scriptitem = General.Map.NamedScripts[arg0str.ToLowerInvariant()];
			// Script number?
			else if(isacsscript && General.Map.NumberedScripts.ContainsKey(args[0]))
			{
				scriptitem = General.Map.NumberedScripts[args[0]];
				arg0str = (scriptitem.HasCustomName ? scriptitem.Name : scriptitem.Index.ToString());
			}

			string[] labels = new string[5];
			bool[] enabled = new bool[5];
			for(int i = 0; i < 5; i++)
			{
				labels[i] = (isarg0str ? arginfo[i].TitleStr : arginfo[i].Title) + ":";
				enabled[i] = arginfo[i].Used;
			}

			bool unknownscript = false;
			if(scriptitem != null)
			{
				int first;
				string[] argnames = scriptitem.GetArgumentsDescriptions(action, out first);
				for(int i = first; i < argnames.Length && i < 5; i++)
				{
					if(string.IsNullOrEmpty(argnames[i])) continue;
					labels[i] = argnames[i] + ":";
					enabled[i] = true;
				}
			}
			else if(isacsscript)
			{
				labels[0] = "Unknown script " + (isarg0str ? "name" : "number") + ":";
				unknownscript = true;
			}

			for(int i = 0; i < 5; i++)
			{
				string value;
				if(i == 0 && isarg0str) value = arg0str;
				else
				{
					TypeHandler th = General.Types.GetArgumentHandler(arginfo[i]);
					th.SetValue(args[i]);
					value = th.GetStringValue();
				}
				result.Add(new InfoField(labels[i], value, enabled[i]) { Error = i == 0 && unknownscript });
			}
			return result;
		}

		#endregion
	}
}
