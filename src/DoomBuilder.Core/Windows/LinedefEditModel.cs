#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>One of the three texture parts of a side.</summary>
	public enum SidePart { High, Middle, Low }

	/// <summary>What the dialog shows for one side (front or back) of the lines being edited.</summary>
	public sealed class SideSettings
	{
		public readonly string[] Textures = { "", "", "" };
		public readonly bool[] MultipleTextures = new bool[3];
		public readonly bool[] Required = new bool[3];
		public string Sector = "";
		public string OffsetX = "", OffsetY = "";
		/// <summary>Whether the lines have this side: true, false, or null when some have it and some do not.</summary>
		public bool? Exists;
		/// <summary>UDMF: the flags of the sidedefs.</summary>
		public FlagSetModel Flags;
	}

	/// <summary>
	/// The logic of UDB's linedef dialogs (LinedefEditForm and its UDMF version) without any UI: flags, activation, action with its
	/// arguments, tag, and both sides (existence, sector, textures, offsets). Flags, textures and offsets are applied to the lines as
	/// they are edited, under one undo level; the rest on OK. <see cref="Cancel"/> withdraws what was done.
	/// </summary>
	internal sealed class LinedefEditModel
	{
		private sealed class SideOriginal
		{
			public readonly int OffsetX, OffsetY;
			public readonly string High, Middle, Low;
			public SideOriginal(Sidedef s) { OffsetX = s.OffsetX; OffsetY = s.OffsetY; High = s.HighTexture; Middle = s.MiddleTexture; Low = s.LowTexture; }
		}

		private sealed class Original
		{
			public readonly SideOriginal Front, Back;
			public Original(Linedef l) { Front = l.Front != null ? new SideOriginal(l.Front) : null; Back = l.Back != null ? new SideOriginal(l.Back) : null; }
		}

		private readonly List<Linedef> lines;
		private readonly List<Original> originals = new List<Original>();
		private readonly bool oldmapischanged;
		private bool undocreated;

		public event EventHandler ValuesChanged;

		public string Title { get; private set; }
		public bool UDMF { get { return General.Map.UDMF; } }
		public ICollection<Linedef> Lines { get { return lines; } }

		public bool HasTag { get { return General.Map.FormatInterface.HasLinedefTag; } }
		public bool HasActionArgs { get { return General.Map.FormatInterface.HasActionArgs; } }
		public bool HasPresetActivations { get { return General.Map.FormatInterface.HasPresetActivations; } }

		public FlagSetModel Flags { get; private set; }
		public IList<LinedefActivateInfo> Activations { get { return General.Map.Config.LinedefActivates; } }

		/// <summary>The activation all lines share, or null (nothing selected).</summary>
		public LinedefActivateInfo Activation { get; private set; }

		public int Action { get; private set; }
		public bool ActionEmpty { get; private set; }
		public int Tag { get; private set; }
		public bool TagsDiffer { get; private set; }
		public string MoreTags { get; private set; }
		public bool MoreTagsDiffer { get; private set; }

		public SideSettings Front { get; private set; }
		public SideSettings Back { get; private set; }

		/// <summary>The arguments the dialog's arguments box edits (set by the dialog).</summary>
		public ArgumentsModel Arguments { get; set; }

		public LinedefEditModel(ICollection<Linedef> lines)
		{
			this.lines = lines.ToList();
			oldmapischanged = General.Map.IsChanged;
			Title = this.lines.Count > 1 ? "Edit Linedefs (" + this.lines.Count + ")" : "Edit Linedef";

			Linedef fl = this.lines[0];
			Flags = new FlagSetModel(General.Map.Config.LinedefFlags.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
			Flags.Load(this.lines, (l, key) => l.IsFlagSet(key));

			// All lines share an activation when each matches the same entry
			Activation = ActivationOf(fl);
			Action = fl.Action;
			Tag = fl.Tag;
			MoreTags = MoreTagsOf(fl);

			Front = new SideSettings();
			Back = new SideSettings();
			LoadSide(Front, l => l.Front, true);
			LoadSide(Back, l => l.Back, true);

			foreach(Linedef l in this.lines)
			{
				if(ActivationOf(l) != Activation) Activation = null;
				if(l.Action != Action) ActionEmpty = true;
				if(l.Tag != fl.Tag) TagsDiffer = true;
				if(MoreTagsOf(l) != MoreTagsOf(fl)) { MoreTagsDiffer = true; MoreTags = ""; }
				LoadSide(Front, s => s.Front, false);
				LoadSide(Back, s => s.Back, false);
				originals.Add(new Original(l));
			}

			// The side flags (UDMF)
			if(UDMF)
			{
				Front.Flags = new FlagSetModel(General.Map.Config.SidedefFlags.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
				Back.Flags = new FlagSetModel(General.Map.Config.SidedefFlags.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
				Front.Flags.Load(this.lines.Where(l => l.Front != null).Select(l => l.Front), (s, key) => s.IsFlagSet(key));
				Back.Flags.Load(this.lines.Where(l => l.Back != null).Select(l => l.Back), (s, key) => s.IsFlagSet(key));
			}
		}

		private static string MoreTagsOf(Linedef l) { return string.Join(", ", l.Tags.Skip(1).Select(t => t.ToString())); }

		// The last activation whose bits the line has, as UDB picks it
		private static LinedefActivateInfo ActivationOf(Linedef l)
		{
			LinedefActivateInfo found = null;
			foreach(LinedefActivateInfo ai in General.Map.Config.LinedefActivates)
				if((l.Activate & ai.Index) == ai.Index) found = ai;
			return found;
		}

		// Reads a side of the line being looked at: the first line sets the values, the next ones clear what differs
		private void LoadSide(SideSettings s, Func<Linedef, Sidedef> get, bool firstline)
		{
			Linedef line = firstline ? lines[0] : null;
			if(firstline)
			{
				Sidedef side = get(line);
				s.Exists = side != null;
				if(side != null)
				{
					s.Textures[0] = side.HighTexture; s.Textures[1] = side.MiddleTexture; s.Textures[2] = side.LowTexture;
					s.Required[0] = side.HighRequired(); s.Required[1] = side.MiddleRequired(); s.Required[2] = side.LowRequired();
					s.Sector = side.Sector.Index.ToString();
					s.OffsetX = side.OffsetX.ToString();
					s.OffsetY = side.OffsetY.ToString();
				}
				return;
			}

			// Called once per line with the line's own side through <get>; find the line by counting
			Linedef l = lines[originals.Count];
			Sidedef sd = get(l);
			if((sd != null) != s.Exists) s.Exists = null;
			if(sd == null) return;

			string[] now = { sd.HighTexture, sd.MiddleTexture, sd.LowTexture };
			bool[] req = { sd.HighRequired(), sd.MiddleRequired(), sd.LowRequired() };
			for(int i = 0; i < 3; i++)
			{
				if(!string.IsNullOrEmpty(s.Textures[i]) && s.Textures[i] != now[i])
				{
					if(!s.Required[i] && req[i]) s.Required[i] = true;
					s.MultipleTextures[i] = true;
					s.Textures[i] = string.Empty;
				}
			}
			if(s.Sector != sd.Sector.Index.ToString()) s.Sector = string.Empty;
			if(s.OffsetX != sd.OffsetX.ToString()) s.OffsetX = string.Empty;
			if(s.OffsetY != sd.OffsetY.ToString()) s.OffsetY = string.Empty;
		}

		#region ================== Applying

		private void MakeUndo()
		{
			if(undocreated) return;
			undocreated = true;
			General.Map.UndoRedo.CreateUndo("Edit " + (lines.Count > 1 ? lines.Count + " linedefs" : "linedef"));
			if(UDMF)
				foreach(Linedef l in lines)
				{
					l.Fields.BeforeFieldsChange();
					if(l.Front != null) l.Front.Fields.BeforeFieldsChange();
					if(l.Back != null) l.Back.Fields.BeforeFieldsChange();
				}
		}

		private void Changed()
		{
			General.Map.IsChanged = true;
			if(ValuesChanged != null) ValuesChanged(this, EventArgs.Empty);
		}

		/// <summary>A flag was clicked.</summary>
		public void SetFlag(string key, bool value)
		{
			MakeUndo();
			foreach(Linedef l in lines) l.SetFlag(key, value);
			Changed();
		}

		/// <summary>A sidedef flag was clicked (UDMF).</summary>
		public void SetSideFlag(bool front, string key, bool value)
		{
			MakeUndo();
			foreach(Linedef l in lines)
			{
				Sidedef s = front ? l.Front : l.Back;
				if(s != null) s.SetFlag(key, value);
			}
			Changed();
		}

		/// <summary>
		/// A texture box changed. An empty name puts each side's original texture back; otherwise <paramref name="result"/> gives the
		/// texture for a side from its current one (so "keep" works with mixed selections).
		/// </summary>
		public void SetTexture(bool front, SidePart part, string name, Func<string, string> result)
		{
			MakeUndo();
			int i = 0;
			foreach(Linedef l in lines)
			{
				Sidedef s = front ? l.Front : l.Back;
				SideOriginal o = front ? originals[i].Front : originals[i].Back;
				i++;
				if(s == null) continue;

				string current = part == SidePart.High ? s.HighTexture : part == SidePart.Middle ? s.MiddleTexture : s.LowTexture;
				string value = string.IsNullOrEmpty(name)
					? (o != null ? (part == SidePart.High ? o.High : part == SidePart.Middle ? o.Middle : o.Low) : "-")
					: result(current);
				if(part == SidePart.High) s.SetTextureHigh(value);
				else if(part == SidePart.Middle) s.SetTextureMid(value);
				else s.SetTextureLow(value);
			}
			General.Map.Data.UpdateUsedTextures();
			Changed();
		}

		/// <summary>The offset boxes of a side changed.</summary>
		public void SetOffsets(bool front, NumericInput x, NumericInput y)
		{
			MakeUndo();
			x.ResetIncrementStep();
			y.ResetIncrementStep();
			int i = 0;
			foreach(Linedef l in lines)
			{
				Sidedef s = front ? l.Front : l.Back;
				SideOriginal o = front ? originals[i].Front : originals[i].Back;
				i++;
				if(s == null) continue;
				s.OffsetX = x.GetResult(o != null ? o.OffsetX : 0);
				s.OffsetY = y.GetResult(o != null ? o.OffsetY : 0);
			}
			Changed();
		}

		#endregion

		#region ================== OK / Cancel

		public string Validate(TagSelectorModel tag, int action)
		{
			if(HasTag)
			{
				tag.ValidateTag();
				int t = tag.GetTag(0);
				if(t < General.Map.FormatInterface.MinTag || t > General.Map.FormatInterface.MaxTag)
					return "Linedef tag must be between " + General.Map.FormatInterface.MinTag + " and " + General.Map.FormatInterface.MaxTag + ".";
			}
			if(action < General.Map.FormatInterface.MinAction || action > General.Map.FormatInterface.MaxAction)
				return "Linedef action must be between " + General.Map.FormatInterface.MinAction + " and " + General.Map.FormatInterface.MaxAction + ".";
			return null;
		}

		/// <summary>
		/// OK. <paramref name="frontexists"/>/<paramref name="backexists"/> are what the side check boxes say (null = leave as is);
		/// <paramref name="frontsector"/>/<paramref name="backsector"/> the sector boxes.
		/// </summary>
		public void Apply(TagSelectorModel tag, LinedefActivateInfo activation, int action, bool actionempty, string moretags,
			bool? frontexists, NumericInput frontsector, bool? backexists, NumericInput backsector,
			FieldsEditorModel linefields, FieldsEditorModel frontfields, FieldsEditorModel backfields)
		{
			MakeUndo();

			int offset = 0;
			List<int> extra = UDMF && moretags != null && (MoreTagsDiffer ? moretags.Length > 0 : moretags != MoreTags) ? SectorEditModel.ParseTags(moretags) : null;
			foreach(Linedef l in lines)
			{
				if(activation != null) l.Activate = activation.Index;

				if(HasTag) l.Tag = General.Clamp(tag.GetSmartTag(l.Tag, offset), General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(extra != null)
				{
					var all = new List<int> { l.Tag };
					all.AddRange(extra.Where(t => t != l.Tag));
					l.Tags = all;
				}
				if(!actionempty) l.Action = action;
				Arguments.Apply(l, offset);

				if(UDMF)
				{
					if(linefields != null) linefields.Apply(l.Fields);
				}

				ApplySide(l, true, frontexists, frontsector, frontfields);
				ApplySide(l, false, backexists, backsector, backfields);
				offset++;
			}

			General.Map.Data.UpdateUsedTextures();
			Changed();
		}

		private void ApplySide(Linedef l, bool front, bool? exists, NumericInput sectorinput, FieldsEditorModel fields)
		{
			Sidedef s = front ? l.Front : l.Back;

			// Remove the side?
			if(s != null && exists == false) { s.Dispose(); return; }

			// Create or modify it?
			if(exists == true)
			{
				// A valid sector (a new one when the index is the next free one)
				int index = s != null ? s.Sector.Index : -1;
				index = sectorinput.GetResult(index);
				if(index > -1 && index < General.Map.Map.Sectors.Count)
				{
					Sector sec = General.Map.Map.GetSectorByIndex(index) ?? General.Map.Map.CreateSector();
					if(sec != null)
					{
						if(s == null) General.Map.Map.CreateSidedef(l, front, sec);
						s = front ? l.Front : l.Back;
						if(s != null && s.Sector != sec) s.SetSector(sec);
					}
				}
			}

			if(UDMF && s != null && fields != null) fields.Apply(s.Fields);
		}

		public void Cancel()
		{
			if(!undocreated) return;
			General.Map.UndoRedo.WithdrawUndo();
			if(General.Map.IsChanged && !oldmapischanged) General.Map.ForceMapIsChangedFalse();
		}

		#endregion
	}
}
