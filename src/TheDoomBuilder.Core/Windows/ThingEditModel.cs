#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The logic of UDB's thing dialogs (ThingEditForm and its UDMF version) without any UI. Position, angle and type change the
	/// things as they are edited, under one undo level; flags, action, tag and arguments are written on OK. <see cref="Cancel"/>
	/// withdraws what was done.
	/// </summary>
	internal sealed class ThingEditModel
	{
		private struct Original
		{
			public readonly int AngleDoom;
			public readonly double X, Y, Z;
			public Original(Thing t) { AngleDoom = t.AngleDoom; X = t.Position.x; Y = t.Position.y; Z = t.Position.z; }
		}

		// Whether the height box shows the height above the floor (false) or the absolute height (true); remembered between dialogs
		private static bool useabsoluteheight;

		private readonly List<Thing> things;
		private readonly List<Original> originals = new List<Original>();
		private readonly bool oldmapischanged;
		private bool undocreated;

		public event EventHandler ValuesChanged;

		public string Title { get; private set; }
		public bool UDMF { get { return General.Map.UDMF; } }
		public ICollection<Thing> Things { get { return things; } }

		public bool HasAction { get { return General.Map.FormatInterface.HasThingAction; } }
		public bool HasTag { get { return General.Map.FormatInterface.HasThingTag; } }
		public bool HasHeight { get { return General.Map.FormatInterface.HasThingHeight; } }
		public bool AllowDecimals { get { return General.Map.FormatInterface.VertexDecimals > 0; } }
		public bool DoomAngleClamping { get { return General.Map.Config.DoomThingRotationAngles; } }

		public bool UseAbsoluteHeight { get { return useabsoluteheight; } }
		public string HeightLabel { get { return useabsoluteheight ? "Z:" : "Height:"; } }

		// What the boxes start with (empty when the things differ)
		public string X { get; private set; }
		public string Y { get; private set; }
		public string Z { get; private set; }
		public string Angle { get; private set; }
		public int Action { get; private set; }
		public bool ActionEmpty { get; private set; }
		public int Tag { get; private set; }
		public bool TagsDiffer { get; private set; }

		/// <summary>The type shared by all things, or null when they differ.</summary>
		public int? Type { get; private set; }
		public int[] Types { get { return things.Select(t => t.Type).Distinct().ToArray(); } }

		public FlagSetModel Flags { get; private set; }
		/// <summary>The arguments the dialog's arguments box edits (set by the dialog).</summary>
		public ArgumentsModel Arguments { get; set; }

		public ThingEditModel(ICollection<Thing> things)
		{
			this.things = things.ToList();
			oldmapischanged = General.Map.IsChanged;
			Title = this.things.Count > 1 ? "Edit Things (" + this.things.Count + ")" : "Edit Thing";

			Thing ft = this.things[0];
			ft.DetermineSector();
			double floorheight = ft.Sector != null ? Sector.GetFloorPlane(ft.Sector).GetZ(ft.Position) : 0;
			X = ((int)ft.Position.x).ToString();
			Y = ((int)ft.Position.y).ToString();
			Z = useabsoluteheight ? ((int)Math.Round(ft.Position.z + floorheight)).ToString() : ((int)ft.Position.z).ToString();
			Angle = ft.AngleDoom.ToString();
			Action = ft.Action;
			Tag = ft.Tag;
			Type = ft.Type;

			Flags = new FlagSetModel(General.Map.Config.ThingFlags.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
			Flags.Load(this.things, (t, key) => t.IsFlagSet(key));

			foreach(Thing t in this.things)
			{
				t.DetermineSector();
				if(t.Type != Type) Type = null;
				if(t.AngleDoom.ToString() != Angle) Angle = "";
				if(((int)t.Position.x).ToString() != X) X = "";
				if(((int)t.Position.y).ToString() != Y) Y = "";
				if(HeightOf(t) != Z) Z = "";
				if(t.Action != Action) ActionEmpty = true;
				if(t.Tag != Tag) TagsDiffer = true;
				originals.Add(new Original(t));
			}
		}

		private static string HeightOf(Thing t)
		{
			if(useabsoluteheight && t.Sector != null)
				return ((int)Math.Round(Sector.GetFloorPlane(t.Sector).GetZ(t.Position) + t.Position.z)).ToString();
			return ((int)t.Position.z).ToString();
		}

		/// <summary>The type info of the first thing (null when the types differ).</summary>
		public ThingTypeInfo TypeInfo { get { return Type.HasValue ? General.Map.Data.GetThingInfoEx(Type.Value) : null; } }

		#region ================== Applying

		private void MakeUndo()
		{
			if(undocreated) return;
			undocreated = true;
			General.Map.UndoRedo.CreateUndo("Edit " + (things.Count > 1 ? things.Count + " things" : "thing"));
			if(UDMF) foreach(Thing t in things) t.Fields.BeforeFieldsChange();
		}

		private void Changed()
		{
			General.Map.IsChanged = true;
			if(ValuesChanged != null) ValuesChanged(this, EventArgs.Empty);
		}

		public void SetX(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Thing t in things) t.Move(new Vector2D(input.GetResultFloat(originals[i++].X), t.Position.y));
			Changed();
		}

		public void SetY(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Thing t in things) t.Move(new Vector2D(t.Position.x, input.GetResultFloat(originals[i++].Y)));
			Changed();
		}

		public void SetZ(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Thing t in things)
			{
				if(string.IsNullOrEmpty(input.Text)) { t.Move(new Vector3D(t.Position.x, t.Position.y, originals[i++].Z)); continue; }

				double z = input.GetResultFloat(originals[i++].Z);
				if(useabsoluteheight && !input.IsRelative && t.Sector != null)
					z -= Math.Round(Sector.GetFloorPlane(t.Sector).GetZ(t.Position.x, t.Position.y), General.Map.FormatInterface.VertexDecimals);
				t.Move(new Vector3D(t.Position.x, t.Position.y, z));
			}
			Changed();
		}

		/// <summary>The "absolute height" box was toggled. Returns the text for the height box (empty when the things differ).</summary>
		public string SetAbsoluteHeight(bool absolute)
		{
			MakeUndo();
			useabsoluteheight = absolute;

			string text = null;
			foreach(Thing t in things)
			{
				double z = t.Position.z;
				if(useabsoluteheight && t.Sector != null) z += Sector.GetFloorPlane(t.Sector).GetZ(t.Position);
				string ztext = Math.Round(z, General.Map.FormatInterface.VertexDecimals).ToString();
				if(text == null) text = ztext;
				else if(text != ztext) return "";
			}
			return text ?? "";
		}

		/// <summary>The angle box (or the dial) changed.</summary>
		public void SetAngle(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Thing t in things)
			{
				int original = originals[i++].AngleDoom;
				t.Rotate(string.IsNullOrEmpty(input.Text) ? original : input.GetResult(original));
			}
			Changed();
		}

		/// <summary>The type changed: <paramref name="getresult"/> gives the type for a thing (given its own), so a selection of several types picks one at random per thing.</summary>
		public void SetTypes(Func<int, int> getresult)
		{
			MakeUndo();
			foreach(Thing t in things)
			{
				t.Type = getresult(t.Type);
				t.UpdateConfiguration();
			}
			Changed();
		}

		/// <summary>Whether a thing type number is acceptable.</summary>
		public static bool IsValidType(int type)
		{
			return type >= General.Map.FormatInterface.MinThingType && type <= General.Map.FormatInterface.MaxThingType;
		}

		/// <summary>The flags that cannot work together as they are set (shown as a warning), empty when all is well.</summary>
		public List<string> FlagWarnings()
		{
			var active = new HashSet<string>(Flags.Items.Where(f => f.Value != false).Select(f => f.Key));
			return ThingFlagsCompare.CheckFlags(active);
		}

		#endregion

		#region ================== OK / Cancel

		/// <summary>Why OK cannot be accepted, or null.</summary>
		public string Validate(TagSelectorModel tag, string typetext, int typevalue, int action)
		{
			if(HasTag)
			{
				tag.ValidateTag();
				int t = tag.GetTag(0);
				if(t < General.Map.FormatInterface.MinTag || t > General.Map.FormatInterface.MaxTag)
					return "Thing tag must be between " + General.Map.FormatInterface.MinTag + " and " + General.Map.FormatInterface.MaxTag + ".";
			}
			if(!string.IsNullOrEmpty(typetext) && !IsValidType(typevalue))
				return "Thing type must be between " + General.Map.FormatInterface.MinThingType + " and " + General.Map.FormatInterface.MaxThingType + ".";
			if(HasAction && !ActionEmpty && (action < General.Map.FormatInterface.MinAction || action > General.Map.FormatInterface.MaxAction))
				return "Thing action must be between " + General.Map.FormatInterface.MinAction + " and " + General.Map.FormatInterface.MaxAction + ".";
			return null;
		}

		/// <summary>OK. <paramref name="randomangle"/> gives each thing a random angle.</summary>
		public void Apply(TagSelectorModel tag, int action, bool actionempty, bool randomangle, int defaulttype, int defaultangle, FieldsEditorModel fields)
		{
			MakeUndo();

			int offset = 0;
			foreach(Thing t in things)
			{
				if(randomangle)
				{
					int newangle = General.Random(0, 359);
					if(DoomAngleClamping) newangle = newangle / 45 * 45;
					t.Rotate(newangle);
				}

				// Keep inside the map boundaries
				double px = General.Clamp(t.Position.x, General.Map.Config.LeftBoundary, General.Map.Config.RightBoundary);
				double py = General.Clamp(t.Position.y, General.Map.Config.BottomBoundary, General.Map.Config.TopBoundary);
				if(t.Position.x != px || t.Position.y != py) t.Move(new Vector2D(px, py));

				Flags.Apply((key, value) => t.SetFlag(key, value));

				if(HasTag) t.Tag = General.Clamp(tag.GetSmartTag(t.Tag, offset), General.Map.FormatInterface.MinTag, General.Map.FormatInterface.MaxTag);
				if(!actionempty) t.Action = action;
				Arguments.Apply(t, offset);
				if(UDMF && fields != null) fields.Apply(t.Fields);

				t.UpdateConfiguration();
				offset++;
			}

			// What the next thing starts with
			var defaultflags = Flags.Items.Where(f => f.Value == true).Select(f => f.Key).ToList();
			General.Settings.DefaultThingType = defaulttype;
			General.Settings.DefaultThingAngle = Angle2D.DegToRad(defaultangle);
			General.Settings.SetDefaultThingFlags(defaultflags);

			Changed();
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
