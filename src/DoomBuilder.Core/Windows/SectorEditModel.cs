#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The logic of UDB's sector dialogs (SectorEditForm and its UDMF version) without any UI. Properties are applied to the
	/// sectors as they change, under one undo level; <see cref="Cancel"/> withdraws them. The UI hands in the box contents
	/// (<see cref="NumericInput"/>, texture names) and shows what the model reports.
	/// </summary>
	internal sealed class SectorEditModel
	{
		private struct Original
		{
			public readonly int Brightness, FloorHeight, CeilHeight;
			public readonly string FloorTexture, CeilTexture;
			public Original(Sector s) { Brightness = s.Brightness; FloorHeight = s.FloorHeight; CeilHeight = s.CeilHeight; FloorTexture = s.FloorTexture; CeilTexture = s.CeilTexture; }
		}

		private readonly List<Sector> sectors;
		private readonly List<Original> originals = new List<Original>();
		private readonly bool oldmapischanged;
		private bool undocreated;

		public event EventHandler ValuesChanged;

		public string Title { get; private set; }
		public bool UDMF { get { return General.Map.UDMF; } }
		public ICollection<Sector> Sectors { get { return sectors; } }

		// What the boxes start with (empty when the sectors differ)
		public int Effect { get; private set; }
		public bool EffectEmpty { get; private set; }
		public string Brightness { get; private set; }
		public string FloorHeight { get; private set; }
		public string CeilingHeight { get; private set; }
		public string FloorTexture { get; private set; }
		public string CeilingTexture { get; private set; }
		public bool FloorTexturesDiffer { get; private set; }
		public bool CeilingTexturesDiffer { get; private set; }
		public int Tag { get; private set; }
		public bool TagsDiffer { get; private set; }

		/// <summary>UDMF only: the tags after the first one, as text "2, 3" (empty when the sectors differ).</summary>
		public string MoreTags { get; private set; }
		public bool MoreTagsDiffer { get; private set; }

		/// <summary>UDMF only: the sector flags.</summary>
		public FlagSetModel Flags { get; private set; }

		public SectorEditModel(ICollection<Sector> sectors)
		{
			this.sectors = sectors.ToList();
			oldmapischanged = General.Map.IsChanged;
			Title = this.sectors.Count > 1 ? "Edit Sectors (" + this.sectors.Count + ")" : "Edit Sector";

			Sector sc = this.sectors[0];
			Effect = sc.Effect;
			Brightness = sc.Brightness.ToString();
			FloorHeight = sc.FloorHeight.ToString();
			CeilingHeight = sc.CeilHeight.ToString();
			FloorTexture = sc.FloorTexture;
			CeilingTexture = sc.CeilTexture;
			Tag = sc.Tag;
			MoreTags = MoreTagsOf(sc);

			foreach(Sector s in this.sectors)
			{
				if(s.Effect != Effect) EffectEmpty = true;
				if(s.Brightness.ToString() != Brightness) Brightness = "";
				if(s.FloorHeight.ToString() != FloorHeight) FloorHeight = "";
				if(s.CeilHeight.ToString() != CeilingHeight) CeilingHeight = "";
				if(s.FloorTexture != FloorTexture) { FloorTexturesDiffer = true; FloorTexture = ""; }
				if(s.CeilTexture != CeilingTexture) { CeilingTexturesDiffer = true; CeilingTexture = ""; }
				if(s.Tag != sc.Tag) TagsDiffer = true;
				if(MoreTagsOf(s) != MoreTagsOf(sc)) { MoreTagsDiffer = true; MoreTags = ""; }
				originals.Add(new Original(s));
			}

			if(UDMF)
			{
				Flags = new FlagSetModel(General.Map.Config.SectorFlags);
				Flags.Load(this.sectors, (s, key) => s.IsFlagSet(key));
			}
		}

		private static string MoreTagsOf(Sector s) { return string.Join(", ", s.Tags.Skip(1).Select(t => t.ToString())); }

		#region ================== Applying

		private void MakeUndo()
		{
			if(undocreated) return;
			undocreated = true;
			General.Map.UndoRedo.CreateUndo("Edit " + (sectors.Count > 1 ? sectors.Count + " sectors" : "sector"));
			if(UDMF) foreach(Sector s in sectors) s.Fields.BeforeFieldsChange();
		}

		private void Changed()
		{
			General.Map.IsChanged = true;
			if(ValuesChanged != null) ValuesChanged(this, EventArgs.Empty);
		}

		/// <summary>The ceiling box changed.</summary>
		public void SetCeilingHeight(NumericInput ceiling, NumericInput offset)
		{
			MakeUndo();
			UpdateCeiling(ceiling, offset);
			Changed();
		}

		public void SetFloorHeight(NumericInput floor, NumericInput offset)
		{
			MakeUndo();
			UpdateFloor(floor, offset);
			Changed();
		}

		/// <summary>The height offset box changed: moves both floor and ceiling.</summary>
		public void SetHeightOffset(NumericInput floor, NumericInput ceiling, NumericInput offset)
		{
			MakeUndo();
			UpdateFloor(floor, offset);
			UpdateCeiling(ceiling, offset);
			Changed();
		}

		// "++" / "--" alone raise or lower each sector by its own height; otherwise the box value plus the offset
		private void UpdateCeiling(NumericInput ceiling, NumericInput offset)
		{
			int i = 0;
			if(offset.Text == "++" || offset.Text == "--")
			{
				int sign = offset.Text == "++" ? 1 : -1;
				foreach(Sector s in sectors)
				{
					s.CeilHeight += (originals[i].CeilHeight - originals[i].FloorHeight) * sign;
					i++;
				}
				return;
			}

			// Reset the +++/--- steps, otherwise they keep counting
			offset.ResetIncrementStep();
			foreach(Sector s in sectors)
			{
				// The offset is read again for each sector so that +++/--- grow from sector to sector
				int off = offset.GetResult(0);
				s.CeilHeight = (string.IsNullOrEmpty(ceiling.Text) ? originals[i].CeilHeight : ceiling.GetResult(originals[i].CeilHeight)) + off;
				i++;
			}
		}

		private void UpdateFloor(NumericInput floor, NumericInput offset)
		{
			int i = 0;
			if(offset.Text == "++" || offset.Text == "--")
			{
				int sign = offset.Text == "++" ? 1 : -1;
				foreach(Sector s in sectors)
				{
					s.FloorHeight += (originals[i].CeilHeight - originals[i].FloorHeight) * sign;
					i++;
				}
				return;
			}

			offset.ResetIncrementStep();
			foreach(Sector s in sectors)
			{
				int off = offset.GetResult(0);
				s.FloorHeight = (string.IsNullOrEmpty(floor.Text) ? originals[i].FloorHeight : floor.GetResult(originals[i].FloorHeight)) + off;
				i++;
			}
		}

		/// <summary>
		/// The height of the sectors (ceiling - floor, as the boxes would make it) when all sectors have the same, else null (the
		/// field is hidden).
		/// </summary>
		public string SectorHeight(NumericInput floor, NumericInput ceiling)
		{
			int delta = sectors[0].CeilHeight - sectors[0].FloorHeight;
			if(sectors.Any(s => s.CeilHeight - s.FloorHeight != delta)) return null;
			int fh = floor.GetResult(originals[0].FloorHeight, 1);
			int ch = ceiling.GetResult(originals[0].CeilHeight, 1);
			return (ch - fh).ToString(CultureInfo.InvariantCulture);
		}

		public void SetFloorTexture(string name)
		{
			MakeUndo();
			int i = 0;
			foreach(Sector s in sectors)
				s.SetFloorTexture(string.IsNullOrEmpty(name) ? originals[i++].FloorTexture : name);
			General.Map.Data.UpdateUsedTextures();
			Changed();
		}

		public void SetCeilingTexture(string name)
		{
			MakeUndo();
			int i = 0;
			foreach(Sector s in sectors)
				s.SetCeilTexture(string.IsNullOrEmpty(name) ? originals[i++].CeilTexture : name);
			General.Map.Data.UpdateUsedTextures();
			Changed();
		}

		public void SetBrightness(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Sector s in sectors)
			{
				int original = originals[i++].Brightness;
				s.Brightness = string.IsNullOrEmpty(input.Text) ? original
					: General.Clamp(input.GetResult(original), General.Map.FormatInterface.MinBrightness, General.Map.FormatInterface.MaxBrightness);
			}
			Changed();
		}

		/// <summary>A flag was clicked (UDMF).</summary>
		public void SetFlag(string key, bool value)
		{
			MakeUndo();
			foreach(Sector s in sectors) s.SetFlag(key, value);
			Changed();
		}

		#endregion

		#region ================== OK / Cancel

		/// <summary>Why OK cannot be accepted (the tag or effect is out of range), or null.</summary>
		public string Validate(TagSelectorModel tag, int effect, bool effectempty)
		{
			tag.ValidateTag();
			int min = General.Map.FormatInterface.MinTag, max = General.Map.FormatInterface.MaxTag;
			// The first sector's tag decides (a smart tag may still give others different values)
			int t = tag.GetTag(0);
			if(t < min || t > max) return "Sector tag must be between " + min + " and " + max + ".";
			if(!effectempty && (effect < General.Map.FormatInterface.MinEffect || effect > General.Map.FormatInterface.MaxEffect))
				return "Sector effect must be between " + General.Map.FormatInterface.MinEffect + " and " + General.Map.FormatInterface.MaxEffect + ".";
			return null;
		}

		/// <summary>OK: writes the effect, the tags and (UDMF) the custom fields.</summary>
		public void Apply(TagSelectorModel tag, int effect, bool effectempty, string moretags, FieldsEditorModel fields)
		{
			MakeUndo();

			int tagoffset = 0;
			List<int> extra = UDMF && moretags != null && (MoreTagsDiffer ? moretags.Length > 0 : moretags != MoreTags) ? ParseTags(moretags) : null;
			foreach(Sector s in sectors)
			{
				if(!effectempty) s.Effect = effect;
				s.Tag = General.Clamp(tag.GetSmartTag(s.Tag, tagoffset++), General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(extra != null)
				{
					List<int> all = new List<int> { s.Tag };
					all.AddRange(extra.Where(t => t != s.Tag));
					s.Tags = all;
				}
				if(UDMF && fields != null) fields.Apply(s.Fields);
			}

			Changed();
		}

		/// <summary>"2, 3 4" -> 2, 3, 4 (invalid parts are ignored, out-of-range tags clamped, zeros dropped).</summary>
		public static List<int> ParseTags(string text)
		{
			var result = new List<int>();
			foreach(string part in (text ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
			{
				int t;
				if(!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) continue;
				t = General.Clamp(t, General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(t != 0 && !result.Contains(t)) result.Add(t);
			}
			return result;
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
