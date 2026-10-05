#region ================== Namespaces

using System;
using System.Collections.Generic;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The logic of UDB's vertex dialog (VertexEditForm) without any UI. Changes are applied to the vertices as they are made (the
	/// map shows them live) under one undo level; <see cref="Cancel"/> withdraws them.
	/// </summary>
	internal sealed class VertexEditModel
	{
		/// <summary>What the height boxes show for "no offset".</summary>
		public const string ClearValue = "Unused";

		private struct Original
		{
			public readonly double X, Y, ZCeiling, ZFloor;
			public Original(Vertex v) { X = v.Position.x; Y = v.Position.y; ZCeiling = v.ZCeiling; ZFloor = v.ZFloor; }
		}

		private readonly ICollection<Vertex> vertices;
		private readonly List<Original> originals = new List<Original>();
		private readonly bool oldmapischanged;
		private bool undocreated;

		/// <summary>Raised after the vertices changed (the display must redraw).</summary>
		public event EventHandler ValuesChanged;

		public string Title { get; private set; }
		public bool AllowPositionChange { get; private set; }
		public bool HasCustomFields { get { return General.Map.FormatInterface.HasCustomFields; } }
		public bool HeightSupported { get { return General.Map.Config.VertexHeightSupport; } }
		public bool AllowDecimals { get { return General.Map.FormatInterface.VertexDecimals > 0; } }

		// The values to show; empty when the vertices differ
		public string X { get; private set; }
		public string Y { get; private set; }
		public string ZCeiling { get; private set; }
		public string ZFloor { get; private set; }

		public ICollection<Vertex> Vertices { get { return vertices; } }

		public VertexEditModel(ICollection<Vertex> vertices, bool allowpositionchange)
		{
			this.vertices = vertices;
			AllowPositionChange = allowpositionchange;
			oldmapischanged = General.Map.IsChanged;
			Title = vertices.Count > 1 ? "Edit Vertices (" + vertices.Count + ")" : "Edit Vertex";

			Vertex first = General.GetByIndex(vertices, 0);
			X = first.Position.x.ToString();
			Y = first.Position.y.ToString();
			foreach(Vertex v in vertices)
			{
				if(X != v.Position.x.ToString()) X = "";
				if(Y != v.Position.y.ToString()) Y = "";
				originals.Add(new Original(v));
			}

			if(General.Map.UDMF)
			{
				ZCeiling = HeightText(first.ZCeiling);
				ZFloor = HeightText(first.ZFloor);
				foreach(Vertex v in vertices)
				{
					if(ZCeiling != HeightText(v.ZCeiling)) ZCeiling = "";
					if(ZFloor != HeightText(v.ZFloor)) ZFloor = "";
				}
			}
			else { ZCeiling = ""; ZFloor = ""; }
		}

		private static string HeightText(double z) { return double.IsNaN(z) ? ClearValue : z.ToString(); }

		private void MakeUndo()
		{
			if(undocreated) return;
			undocreated = true;
			General.Map.UndoRedo.CreateUndo("Edit " + (vertices.Count > 1 ? vertices.Count + " vertices" : "vertex"));
			if(HasCustomFields)
				foreach(Vertex v in vertices) v.Fields.BeforeFieldsChange();
		}

		private void Changed()
		{
			General.Map.IsChanged = true;
			if(ValuesChanged != null) ValuesChanged(this, EventArgs.Empty);
		}

		private double Clamp(double v) { return Math.Max(General.Map.FormatInterface.MinCoordinate, Math.Min(General.Map.FormatInterface.MaxCoordinate, v)); }

		/// <summary>The X box changed: moves the vertices (an empty box puts them back).</summary>
		public void SetX(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Vertex v in vertices)
			{
				double original = originals[i++].X;
				double px = string.IsNullOrEmpty(input.Text) ? original : Clamp(input.GetResultFloat(original));
				v.Move(new Vector2D(px, v.Position.y));
			}
			Changed();
		}

		public void SetY(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Vertex v in vertices)
			{
				double original = originals[i++].Y;
				double py = string.IsNullOrEmpty(input.Text) ? original : Clamp(input.GetResultFloat(original));
				v.Move(new Vector2D(v.Position.x, py));
			}
			Changed();
		}

		public void SetZCeiling(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Vertex v in vertices)
			{
				double original = originals[i++].ZCeiling;
				if(string.IsNullOrEmpty(input.Text)) v.ZCeiling = original;
				else if(input.Text == ClearValue) v.ZCeiling = float.NaN;
				else v.ZCeiling = input.GetResultFloat(original);
			}
			Changed();
		}

		public void SetZFloor(NumericInput input)
		{
			MakeUndo();
			input.ResetIncrementStep();
			int i = 0;
			foreach(Vertex v in vertices)
			{
				double original = originals[i++].ZFloor;
				if(string.IsNullOrEmpty(input.Text)) v.ZFloor = original;
				else if(input.Text == ClearValue) v.ZFloor = float.NaN;
				else v.ZFloor = input.GetResultFloat(original);
			}
			Changed();
		}

		/// <summary>OK: also writes the custom fields.</summary>
		public void Apply(FieldsEditorModel fields)
		{
			MakeUndo();
			if(HasCustomFields && fields != null)
				foreach(Vertex v in vertices) fields.Apply(v.Fields);
			Changed();
		}

		/// <summary>Cancel: withdraws everything done since the dialog opened.</summary>
		public void Cancel()
		{
			if(!undocreated) return;
			General.Map.UndoRedo.WithdrawUndo();

			// Moving a vertex marks the map as changed; when it was not before, it is not now
			if(General.Map.IsChanged && !oldmapischanged) General.Map.ForceMapIsChangedFalse();
		}
	}
}
