#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	public enum FieldRowType
	{
		/// <summary>A field of the game configuration: cannot be deleted (delete resets it) nor change type.</summary>
		Fixed,
		/// <summary>A field the user entered: can be deleted and change type.</summary>
		Dynamic,
		/// <summary>A user variable defined by the actor (DECORATE/ZScript): cannot be deleted but can change type.</summary>
		UserVar,
	}

	/// <summary>One row of the custom fields list: name, type and value, and whether the field is defined (not just listed).</summary>
	public sealed class FieldRow
	{
		private TypeHandler handler;
		private string text;

		public FieldRowType RowType { get; private set; }
		public UniversalFieldInfo Info { get; private set; }
		public string Name { get; internal set; }
		public bool IsDefined { get; private set; }
		public TypeHandler TypeHandler { get { return handler; } }

		/// <summary>The value as shown. Empty when the selected elements disagree on it.</summary>
		public string Text { get { return text; } }
		public bool IsEmpty { get { return string.IsNullOrEmpty(text); } }
		public string TypeName { get { return handler.GetDisplayType().ToString(); } }
		public bool TypeCanChange { get { return RowType != FieldRowType.Fixed; } }
		public bool NameCanChange { get { return RowType == FieldRowType.Dynamic; } }

		// Fixed, undefined field
		internal FieldRow(UniversalFieldInfo fixedfield)
		{
			Info = fixedfield;
			Name = fixedfield.Name;
			RowType = FieldRowType.Fixed;
			handler = General.Types.GetFieldHandler(fixedfield);
			text = handler.GetStringValue();
		}

		// Dynamic, defined field; or a user variable (not defined until it differs from its default)
		internal FieldRow(string name, int type, object value, bool isuservar)
		{
			Name = name;
			RowType = isuservar ? FieldRowType.UserVar : FieldRowType.Dynamic;
			handler = General.Types.GetFieldHandler(type, value);
			if(isuservar) Info = new UniversalFieldInfo(name, type, value);
			IsDefined = !isuservar;
			text = handler.GetStringValue();
		}

		/// <summary>The user typed a new value (or picked one): validates it and (un)defines the field accordingly.</summary>
		public void SetText(string value)
		{
			text = value ?? "";
			if(text.Length > 0)
			{
				handler.SetValue(text);
				text = handler.GetStringValue();
				// A fixed field with its default value is just not defined
				if(RowType == FieldRowType.Fixed && handler.GetValue().Equals(Info.Default)) Undefine();
				else Mark(true);
			}
			else if(RowType == FieldRowType.Fixed) Undefine();
		}

		// A fixed or user var field back to its default and undefined state
		public void Undefine()
		{
			if(RowType == FieldRowType.Dynamic) throw new InvalidOperationException();
			if(RowType == FieldRowType.UserVar) handler.ApplyDefaultValue(); else handler.SetValue(Info.Default);
			text = handler.GetStringValue();
			IsDefined = false;
		}

		public void Define(object value)
		{
			handler.SetValue(value);
			text = handler.GetStringValue();
			// The default value does not count as defined
			if(value.ToString() == handler.GetDefaultValue().ToString()) return;
			IsDefined = true;
		}

		private void Mark(bool defined) { IsDefined = defined; }

		public void ChangeType(int typeindex)
		{
			if(RowType == FieldRowType.Fixed) throw new InvalidOperationException();
			if(typeindex == handler.Index) return;
			handler = General.Types.GetFieldHandler(typeindex, text);
		}

		/// <summary>Changes the type by its displayed name (what the type column offers).</summary>
		public void ChangeTypeByName(string typename)
		{
			TypeHandlerAttribute attrib = General.Types.GetNamedAttribute(typename);
			if(attrib != null && attrib.Index != handler.Index)
			{
				ChangeType(attrib.Index);
				if(text.Length > 0) SetText(text);   // re-validate under the new type
			}
		}

		/// <summary>Leaves the value empty: the elements being edited have different values.</summary>
		public void Clear() { text = ""; }

		/// <summary>The value to apply: the typed one, or <paramref name="value"/> (the old one) when empty.</summary>
		public object GetResult(object value)
		{
			if(text.Length == 0) return value;
			handler.SetValue(text);
			return handler.GetValue();
		}

		public bool IsEnumerable { get { return handler.IsEnumerable; } }
		public bool IsLimitedToEnums { get { return handler.IsLimitedToEnums; } }
		public bool IsBrowseable { get { return handler.IsBrowseable && !handler.IsEnumerable; } }
		public IEnumerable<EnumItem> EnumItems { get { return handler.GetEnumList(); } }

		/// <summary>Runs the type's own browser (colors, textures, ...) and takes the result.</summary>
		public void Browse(System.Windows.Forms.IWin32Window parent)
		{
			handler.Browse(parent);
			if(RowType == FieldRowType.Fixed && handler.GetValue().Equals(Info.Default)) Undefine();
			else Define(handler.GetValue());
		}

		public override string ToString() { return Name; }
	}

	/// <summary>
	/// The logic of UDB's custom fields editor (FieldsEditorControl) without any UI: the fixed fields of the configuration plus the
	/// user's own, with their types and values, merged over several elements (a value that differs is left empty) and written back.
	/// </summary>
	public sealed class FieldsEditorModel
	{
		public const string FieldPrefixSuggestion = "user_";

		private readonly List<FieldRow> rows = new List<FieldRow>();
		private string elementname;
		private bool showmanaged;
		private Dictionary<string, UniversalType> uifields = new Dictionary<string, UniversalType>();

		public event Action<string> FieldInserted;
		public event Action<string, string> FieldNameChanged;
		public event Action<string> FieldValueChanged;
		public event Action<string> FieldTypeChanged;
		public event Action<string> FieldDeleted;
		public event Action<string> FieldUndefined;

		/// <summary>Raised when rows come or go (the view must rebuild its list).</summary>
		public event Action RowsChanged;

		public IList<FieldRow> Rows { get { return rows; } }
		public bool AllowInsert { get; set; }
		public bool AutoInsertUserPrefix { get; set; }
		public bool ShowFixedFields { get; set; }

		/// <summary>The types a field can have (for the type column).</summary>
		public TypeHandlerAttribute[] CustomTypes { get { return General.Types.GetCustomUseAttributes(); } }

		public FieldsEditorModel()
		{
			AllowInsert = true;
			AutoInsertUserPrefix = true;
			ShowFixedFields = true;
		}

		#region ================== Setup / Apply

		/// <summary>Prepares the editor for an element type: "thing", "linedef", "sidedef", "sector" or "vertex".</summary>
		/// <param name="showmanaged">
		/// Lists the fields that UDB's own dialog edits with dedicated widgets too (colors, offsets, light...). A dialog that has not
		/// got those widgets yet shows them here, so they can still be edited.
		/// </param>
		public void Setup(string elementname, bool showmanaged = false)
		{
			this.elementname = elementname;
			this.showmanaged = showmanaged;
			uifields = showmanaged ? new Dictionary<string, UniversalType>()
				: General.Map.FormatInterface.UIFields[General.Map.FormatInterface.GetElementType(elementname)];
		}

		/// <summary>Adds the fixed fields (undefined) that are not managed by the UI.</summary>
		public void ListFixedFields(IEnumerable<UniversalFieldInfo> list)
		{
			foreach(UniversalFieldInfo uf in list)
				if(showmanaged || !uf.Managed) rows.Add(new FieldRow(uf));
			Sort();
		}

		public void ClearFields()
		{
			rows.Clear();
			Changed();
		}

		/// <summary>
		/// Takes the fields of an element. With <paramref name="first"/> the values are taken as they are; otherwise a row whose
		/// value differs is cleared (several elements being edited).
		/// </summary>
		public void SetValues(UniFields fromfields, bool first)
		{
			foreach(KeyValuePair<string, UniValue> f in fromfields)
			{
				if(uifields.ContainsKey(f.Key)) continue;

				FieldRow row = rows.FirstOrDefault(r => r.Name == f.Key);
				if(row != null && row.RowType == FieldRowType.UserVar) continue;   // user vars are set separately

				if(row != null)
				{
					if(first)
					{
						if(row.RowType == FieldRowType.Dynamic) row.ChangeType(f.Value.Type);
						row.Define(f.Value.Value);
					}
					else if(!row.TypeHandler.GetValue().Equals(f.Value.Value))
					{
						row.Define(f.Value.Value);
						row.Clear();
					}
				}
				else
				{
					var created = new FieldRow(f.Key, f.Value.Type, f.Value.Value, false);
					rows.Add(created);
					// When not the first, the others did not define this one
					if(!first) created.Clear();
				}
			}

			// Rows these fields do not have are cleared
			foreach(FieldRow row in rows)
				if(row.IsDefined && !fromfields.ContainsKey(row.Name)) row.Clear();

			Sort();
		}

		/// <summary>Takes the user variables of the actor type(s) with their defaults (ZScript/DECORATE).</summary>
		public void SetUserVars(Dictionary<string, UniversalType> vars, Dictionary<string, object> defaults, UniFields fromfields, bool first)
		{
			foreach(KeyValuePair<string, UniversalType> group in vars)
			{
				TypeHandler vartype = General.Types.GetFieldHandler((int)group.Value, 0);
				object defaultvalue = defaults.ContainsKey(group.Key) ? defaults[group.Key] : vartype.GetDefaultValue();
				object value = fromfields.ContainsKey(group.Key) ? fromfields[group.Key].Value : defaultvalue;

				FieldRow row = rows.FirstOrDefault(r => r.RowType == FieldRowType.UserVar && r.Name == group.Key);
				if(row != null)
				{
					if(first) row.Define(value);
					else if(!row.TypeHandler.GetValue().Equals(value)) { row.Define(value); row.Clear(); }
				}
				else
				{
					row = new FieldRow(group.Key, (int)group.Value, defaultvalue, true);
					if(!value.Equals(defaultvalue)) row.Define(value);
					rows.Add(row);
				}
			}

			// User var rows of other actor types are left alone; any other defined row the fields do not have is undefined
			foreach(FieldRow row in rows)
				if(row.RowType != FieldRowType.UserVar && !vars.ContainsKey(row.Name) && row.IsDefined && !fromfields.ContainsKey(row.Name))
					if(row.RowType != FieldRowType.Dynamic) row.Undefine();

			Sort();
		}

		/// <summary>Writes the fields back: removes what was undefined and sets what is defined and not empty.</summary>
		public void Apply(UniFields tofields)
		{
			tofields.BeforeFieldsChange();

			foreach(KeyValuePair<string, UniValue> f in new UniFields(tofields))
			{
				if(uifields.ContainsKey(f.Key)) continue;
				FieldRow row = rows.FirstOrDefault(r => r.Name == f.Key);
				if(row != null && row.RowType == FieldRowType.UserVar) continue;   // stored separately
				if(row == null || !row.IsDefined) tofields.Remove(f.Key);
			}

			foreach(FieldRow row in rows)
			{
				if(row.RowType == FieldRowType.UserVar || !row.IsDefined || row.IsEmpty) continue;

				object oldvalue = tofields.ContainsKey(row.Name) ? tofields[row.Name].Value : null;
				tofields[row.Name] = new UniValue(row.TypeHandler.Index, row.GetResult(oldvalue));

				// A custom field remembers its type in the map options
				if(row.RowType == FieldRowType.Dynamic)
					General.Map.Options.SetUniversalFieldType(elementname, row.Name, row.TypeHandler.Index);
			}
		}

		public void ApplyUserVars(Dictionary<string, UniversalType> vars, Dictionary<string, object> vardefaults, UniFields tofields)
		{
			foreach(FieldRow row in rows)
			{
				if(row.RowType != FieldRowType.UserVar || !vars.ContainsKey(row.Name)) continue;

				object oldvalue = tofields.ContainsKey(row.Name) ? tofields[row.Name].Value : null;
				object newvalue = row.GetResult(oldvalue);
				if(newvalue == null) continue;   // mixed values

				object typedefault = row.TypeHandler.GetDefaultValue();
				object userdefault = vardefaults.ContainsKey(row.Name) ? vardefaults[row.Name] : typedefault;

				// Not stored when it is the default, but only if the type's default and the user var's default agree
				if(newvalue.Equals(typedefault) && typedefault.Equals(userdefault))
				{
					if(tofields.ContainsKey(row.Name)) tofields.Remove(row.Name);
				}
				else if(!newvalue.Equals(oldvalue))
					tofields[row.Name] = new UniValue(row.TypeHandler.Index, newvalue);
			}
		}

		#endregion

		#region ================== Editing

		/// <summary>Sets the value of a row as the user typed or picked it.</summary>
		public void SetValue(FieldRow row, string value)
		{
			if(value != null && (row.RowType != FieldRowType.Fixed || !row.Info.Default.Equals(value)))
			{
				row.SetText(value);
				if(row.RowType != FieldRowType.Fixed && value.Length > 0) row.Define(row.TypeHandler.GetValue());
			}
			else if(row.RowType == FieldRowType.Fixed) row.Undefine();
			if(FieldValueChanged != null) FieldValueChanged(row.Name);
		}

		public void SetType(FieldRow row, string typename)
		{
			row.ChangeTypeByName(typename);
			if(FieldTypeChanged != null) FieldTypeChanged(row.Name);
		}

		/// <summary>
		/// Adds a field of the user's. Returns null on success; otherwise why it was refused (the editor shows it).
		/// An empty or only-prefix name is not an error: nothing is added.
		/// </summary>
		public string AddField(string name, out FieldRow added)
		{
			added = null;
			if(string.IsNullOrEmpty(name) || name == FieldPrefixSuggestion) return null;

			string validname = UniValue.ValidateName(name);
			if(validname.Length == 0) return null;
			if(uifields.ContainsKey(validname)) return "Please set this field's value via user interface.";
			if(rows.Any(r => r.Name.ToLowerInvariant() == validname)) return "Fields must have unique names!";

			// The type is remembered in the map options
			int type = General.Map.Options.GetUniversalFieldType(elementname, validname, 0);
			added = new FieldRow(validname, type, null, false);
			rows.Add(added);
			Sort();
			Changed();
			if(FieldInserted != null) FieldInserted(validname);
			return null;
		}

		/// <summary>Renames a custom field. Returns null on success, else why not (the old name stays).</summary>
		public string Rename(FieldRow row, string newname)
		{
			if(!row.NameCanChange || string.IsNullOrEmpty(newname)) return null;
			string validname = UniValue.ValidateName(newname);
			if(validname.Length == 0 || uifields.ContainsKey(validname)) return null;
			if(validname == row.Name) return null;
			if(rows.Any(r => r != row && r.Name.ToLowerInvariant() == validname)) return "Fields must have unique names!";

			string oldname = row.Name;
			int type = General.Map.Options.GetUniversalFieldType(elementname, validname, -1);
			row.Name = validname;
			if(type != -1) row.ChangeType(type);
			if(FieldNameChanged != null) FieldNameChanged(oldname, validname);
			if(FieldTypeChanged != null) FieldTypeChanged(validname);
			Changed();
			return null;
		}

		/// <summary>Delete on a row: fixed and user var fields are only undefined, custom fields go away.</summary>
		public void Delete(FieldRow row)
		{
			if(row.RowType == FieldRowType.Dynamic)
			{
				rows.Remove(row);
				Changed();
				if(FieldDeleted != null) FieldDeleted(row.Name);
			}
			else
			{
				row.Undefine();
				if(FieldUndefined != null) FieldUndefined(row.Name);
			}
		}

		/// <summary>Removes a row by field name (a field the UI manages elsewhere).</summary>
		public void RemoveField(string name)
		{
			rows.RemoveAll(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
			Changed();
		}

		public void RemoveUserVarsWithDefaultValue()
		{
			rows.RemoveAll(r => r.RowType == FieldRowType.UserVar && r.Info != null && r.TypeHandler.GetValue().Equals(r.Info.Default));
			Changed();
		}

		/// <summary>The rows to show (fixed fields can be hidden).</summary>
		public IEnumerable<FieldRow> VisibleRows { get { return rows.Where(r => ShowFixedFields || r.RowType != FieldRowType.Fixed); } }

		private void Sort()
		{
			// By name; the last sort column the user chose is a view concern
			rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			Changed();
		}

		private void Changed() { if(RowsChanged != null) RowsChanged(); }

		#endregion
	}
}
