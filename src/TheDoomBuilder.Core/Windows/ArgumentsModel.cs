#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.GZBuilder;
using CodeImp.DoomBuilder.GZBuilder.Data;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// One argument box (UDB's ArgumentBox without the UI): a number, with the type's own choices (enumerations), browser, or just
	/// plus/minus steps. The text may be relative: "++5"/"--5" (add/subtract), "+++5"/"---5" (grow with each element), "&lt;5"/"&gt;5"
	/// (consecutive values).
	/// </summary>
	public sealed class ArgumentModel
	{
		private TypeHandler handler;

		public string Text { get; set; }

		public ArgumentModel() { Text = ""; }

		public bool IsEnumerable { get { return handler != null && handler.IsEnumerable; } }
		public bool IsBrowseable { get { return handler != null && handler.IsBrowseable && !handler.IsEnumerable; } }
		public bool HasSteps { get { return !IsEnumerable && !IsBrowseable; } }
		public IEnumerable<EnumItem> EnumItems { get { return IsEnumerable ? handler.GetEnumList() : Enumerable.Empty<EnumItem>(); } }

		/// <summary>Sets the box up for the argument the action or thing describes, keeping the number when something is in the box.</summary>
		public void Setup(ArgumentInfo info)
		{
			int oldvalue = handler != null ? handler.GetIntValue() : 0;
			handler = General.Types.GetArgumentHandler(info);
			if(!string.IsNullOrEmpty(Text)) SetValue(oldvalue);
		}

		public void SetValue(int value)
		{
			handler.SetValue(value);
			Text = handler.GetStringValue();
			Commit();
		}

		public void SetDefaultValue()
		{
			handler.ApplyDefaultValue();
			Text = handler.GetStringValue();
			Commit();
		}

		/// <summary>The elements disagree: the box is left empty.</summary>
		public void ClearValue()
		{
			handler.SetValue("");
			Text = "";
		}

		public bool IsRelative
		{
			get
			{
				string s = (Text ?? "").Trim();
				return s.StartsWith("+++") || s.StartsWith("---") || s.StartsWith("++") || s.StartsWith("--") || s.StartsWith("<") || s.StartsWith(">");
			}
		}

		/// <summary>Called when the user leaves the box: the type validates the text (an invalid relative number empties the box).</summary>
		public void Commit()
		{
			string s = (Text ?? "").Trim().ToLowerInvariant().TrimStart('+', '-', '<', '>');
			if((Text ?? "").Trim().Length == 0) return;

			if(IsRelative)
			{
				int num;
				if(!int.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out num)) Text = "";
			}
			else
			{
				handler.SetValue(Text);
				Text = handler.GetStringValue();
			}
		}

		/// <summary>One more (direction +1) or one less (-1).</summary>
		public void Step(int direction)
		{
			if(IsRelative) return;
			handler.SetValue(Text);
			int value = GetResult(0) + direction;
			if(value < 0) value = 0;
			Text = value.ToString();
			Commit();
		}

		/// <summary>The type's browser (colors, flats, ...).</summary>
		public void Browse(System.Windows.Forms.IWin32Window parent)
		{
			handler.Browse(parent);
			Text = handler.GetStringValue();
		}

		public int GetResult(int original) { return GetResult(original, 0); }

		public int GetResult(int original, int step)
		{
			string str = (Text ?? "").Trim().ToLowerInvariant();
			string numstr = str.TrimStart('+', '-', '<', '>');
			int result;
			if(numstr.Length > 0)
			{
				int num;
				if(str.StartsWith("+++")) { if(!int.TryParse(numstr, out num)) num = 0; result = original + num * step; }
				else if(str.StartsWith("---")) { if(!int.TryParse(numstr, out num)) num = 0; result = original - num * step; }
				else if(str.StartsWith("<")) { if(!int.TryParse(numstr, out num)) num = 0; result = num - step; }
				else if(str.StartsWith(">")) { if(!int.TryParse(numstr, out num)) num = 0; result = num + step; }
				else if(str.StartsWith("++")) { if(!int.TryParse(numstr, out num)) num = 0; result = original + num; }
				else if(str.StartsWith("--")) { if(!int.TryParse(numstr, out num)) num = 0; result = original - num; }
				else result = handler.GetIntValue();
			}
			else result = original;

			return General.Clamp(result, General.Map.FormatInterface.MinArgument, General.Map.FormatInterface.MaxArgument);
		}
	}

	/// <summary>A numbered or named script a script action can point at.</summary>
	public sealed class ScriptChoice
	{
		internal ScriptItem Item;
		public string Text { get { return Item.ToString(); } }
		public bool IsInclude { get { return Item.IsInclude; } }
		public int Index { get { return Item.Index; } }
		public string Name { get { return Item.Name; } }
		public override string ToString() { return Text; }
	}

	/// <summary>The caption of an argument: its title, whether it is used, and its tooltip.</summary>
	public sealed class ArgumentLabel
	{
		public string Text;
		public bool Enabled;
		public string ToolTip;
	}

	/// <summary>How the first argument is entered.</summary>
	public enum ArgZeroMode { Default, Int, String }

	/// <summary>
	/// UDB's ArgumentsControl without the UI: the five arguments of an action (or thing), what each is called for the action, and
	/// the script number / name handling of the first argument for script actions. Values merge over several elements
	/// (<see cref="SetValue"/>) and are written back with <see cref="Apply"/>.
	/// </summary>
	public sealed class ArgumentsModel
	{
		private string arg0strval = "";
		private bool havearg0str;
		private int action;
		private ArgumentInfo[] arginfo;

		public readonly ArgumentModel[] Args = { new ArgumentModel(), new ArgumentModel(), new ArgumentModel(), new ArgumentModel(), new ArgumentModel() };
		public readonly ArgumentLabel[] Labels = { new ArgumentLabel(), new ArgumentLabel(), new ArgumentLabel(), new ArgumentLabel(), new ArgumentLabel() };

		/// <summary>The script number or name the first argument holds (what the script combos show).</summary>
		public string ScriptNumberText { get; set; }
		public string ScriptNameText { get; set; }
		public string ScriptStringText { get; set; }

		public ArgZeroMode Arg0Mode { get; private set; }

		/// <summary>The "use a string" box (UDMF script actions): the first argument is a name.</summary>
		public bool UseArgString { get; set; }

		public ArgumentsModel() { ScriptNumberText = ScriptNameText = ScriptStringText = ""; }

		/// <summary>Raised when the captions or the visible controls of the first argument changed.</summary>
		public event Action Changed;

		private bool IsAcs { get { return Array.IndexOf(GZGeneral.ACS_SPECIALS, action) != -1; } }

		#region ================== Reading from elements

		public void SetValue(Linedef l, bool first) { SetValue(l.Fields, l.Args, first); }
		public void SetValue(Thing t, bool first) { SetValue(t.Fields, t.Args, first); }

		private void SetValue(UniFields fields, int[] args, bool first)
		{
			if(first)
			{
				if(General.Map.UDMF)
				{
					arg0strval = fields.GetValue("arg0str", string.Empty);
					havearg0str = !string.IsNullOrEmpty(arg0strval);
				}
				for(int i = 0; i < 5; i++) Args[i].SetValue(args[i]);
			}
			else
			{
				if(General.Map.UDMF && arg0strval != fields.GetValue("arg0str", string.Empty))
				{
					havearg0str = true;
					arg0strval = string.Empty;
				}
				for(int i = 0; i < 5; i++)
					if(!string.IsNullOrEmpty(Args[i].Text) && args[i] != Args[i].GetResult(int.MinValue)) Args[i].ClearValue();
			}
		}

		#endregion

		#region ================== Writing to elements

		public void Apply(Linedef l, int step) { Apply(l.Fields, l.Args, step); }
		public void Apply(Thing t, int step) { Apply(t.Fields, t.Args, step); }

		private void Apply(UniFields fields, int[] args, int step)
		{
			bool isacs = IsAcs;
			switch(Arg0Mode)
			{
				case ArgZeroMode.String:
					string name = isacs ? ScriptNameText : ScriptStringText;
					if(!string.IsNullOrEmpty(name)) fields["arg0str"] = new UniValue(UniversalType.String, name);
					break;

				case ArgZeroMode.Int:
					if(!isacs) goto default;
					if(!string.IsNullOrEmpty(ScriptNumberText))
					{
						ScriptChoice choice = Scripts().FirstOrDefault(s => s.Text == ScriptNumberText);
						if(choice != null) args[0] = choice.Index;
						else if(!int.TryParse(ScriptNumberText.Trim(), out args[0])) args[0] = 0;
						if(fields.ContainsKey("arg0str")) fields.Remove("arg0str");
					}
					break;

				default:
					args[0] = Args[0].GetResult(args[0], step);
					if(fields.ContainsKey("arg0str")) fields.Remove("arg0str");
					break;
			}

			for(int i = 1; i < 5; i++) args[i] = Args[i].GetResult(args[i], step);
		}

		#endregion

		#region ================== The action

		/// <summary>The numbered scripts of the map (when the action is an ACS special).</summary>
		public IEnumerable<ScriptChoice> NumberedScripts()
		{
			return IsAcs ? General.Map.NumberedScripts.Values.Select(s => new ScriptChoice { Item = s }) : Enumerable.Empty<ScriptChoice>();
		}

		public IEnumerable<ScriptChoice> NamedScripts()
		{
			return IsAcs && General.Map.UDMF ? General.Map.NamedScripts.Values.Select(s => new ScriptChoice { Item = s }) : Enumerable.Empty<ScriptChoice>();
		}

		private IEnumerable<ScriptChoice> Scripts() { return NumberedScripts().Concat(NamedScripts()); }

		public void UpdateAction(int newaction, bool setuponly) { UpdateAction(newaction, setuponly, null); }

		/// <summary>Takes the argument descriptions of the action (or, with no known action, of the thing type).</summary>
		public void UpdateAction(int newaction, bool setuponly, ThingTypeInfo info)
		{
			int showaction = General.Map.Config.LinedefActions.ContainsKey(newaction) ? newaction : 0;
			ArgumentInfo[] oldarginfo = arginfo != null ? (ArgumentInfo[])arginfo.Clone() : null;

			arginfo = (showaction == 0 && info != null) ? info.Args : General.Map.Config.LinedefActions[showaction].Args;

			// Changing the thing type must not reset the arguments of an action
			if(info != null && showaction != 0 && action == showaction) return;

			// Same descriptions: nothing to do
			if(arginfo != null && oldarginfo != null && ArgumentInfosMatch(arginfo, oldarginfo)) return;

			for(int i = 0; i < 5; i++)
			{
				Labels[i].Text = arginfo[i].Title + ":";
				Labels[i].Enabled = arginfo[i].Used;
				Labels[i].ToolTip = arginfo[i].Used && !string.IsNullOrEmpty(arginfo[i].ToolTip) ? arginfo[i].ToolTip : null;
				Args[i].Setup(arginfo[i]);
			}

			if(!setuponly)
			{
				// The action's or thing's default arguments, or zeros
				if(showaction != 0 || info != null) foreach(ArgumentModel a in Args) a.SetDefaultValue();
				else foreach(ArgumentModel a in Args) a.SetValue(0);
				// arg0str currently can't have any default
				arg0strval = " ";
				ScriptNameText = ScriptStringText = " ";
			}

			action = showaction;
			if(Changed != null) Changed();
		}

		/// <summary>Sets up the first argument (number, script number, script name or string) for the action; call after <see cref="UpdateAction(int,bool,ThingTypeInfo)"/>.</summary>
		public void UpdateScriptControls()
		{
			if(arginfo == null) return;

			if(arginfo[0].Str)
			{
				bool isacs = IsAcs;
				bool showarg0str = General.Map.UDMF && havearg0str;
				UseArgString = showarg0str;
				if(showarg0str)
				{
					Arg0Mode = ArgZeroMode.String;
					Labels[0].Text = arginfo[0].TitleStr + ":";
					ScriptStringText = ScriptNameText = arg0strval;
					if(isacs && General.Map.NamedScripts.ContainsKey(arg0strval)) UpdateScriptArguments(General.Map.NamedScripts[arg0strval]);
				}
				else if(isacs)
				{
					Arg0Mode = ArgZeroMode.Int;
					Labels[0].Text = arginfo[0].Title + ":";
					int a0 = Args[0].GetResult(0);
					ScriptItem item;
					if(General.Map.NumberedScripts.TryGetValue(a0, out item))
					{
						ScriptNumberText = new ScriptChoice { Item = item }.Text;
						UpdateScriptArguments(item);
					}
					else ScriptNumberText = a0.ToString();
				}
				else Arg0Mode = ArgZeroMode.Default;
			}
			else
			{
				UseArgString = false;
				Arg0Mode = ArgZeroMode.Default;
			}

			if(Changed != null) Changed();
		}

		/// <summary>The "use a string" box was clicked.</summary>
		public void SetUseArgString(bool use)
		{
			UseArgString = use;
			Arg0Mode = use ? ArgZeroMode.String : (IsAcs ? ArgZeroMode.Int : ArgZeroMode.Default);
			if(Changed != null) Changed();
		}

		/// <summary>A script number or name was picked or typed: the other arguments take the script's own descriptions.</summary>
		public void ScriptChanged(string text, bool named)
		{
			if(string.IsNullOrEmpty(text)) return;
			ScriptItem item = null;
			ScriptChoice choice = (named ? NamedScripts() : NumberedScripts()).FirstOrDefault(s => s.Text == text);
			if(choice != null) item = choice.Item;
			else if(named)
			{
				string name = text.Trim().ToLowerInvariant();
				if(General.Map.NamedScripts.ContainsKey(name)) item = General.Map.NamedScripts[name];
			}
			else
			{
				int index;
				if(int.TryParse(text, out index) && General.Map.NumberedScripts.ContainsKey(index)) item = General.Map.NumberedScripts[index];
			}
			UpdateScriptArguments(item);
			if(Changed != null) Changed();
		}

		private void UpdateScriptArguments(ScriptItem item)
		{
			if(item != null)
			{
				int first;
				string[] names = item.GetArgumentsDescriptions(action, out first);
				for(int i = first; i < 5; i++)
				{
					if(!string.IsNullOrEmpty(names[i]))
					{
						Labels[i].Text = names[i] + ":";
						Labels[i].Enabled = true;
						Labels[i].ToolTip = null;
					}
					else SetLabel(i);
				}
			}
			else for(int i = 1; i < 5; i++) SetLabel(i);
		}

		private void SetLabel(int i)
		{
			Labels[i].Text = arginfo[i].Title + ":";
			Labels[i].Enabled = arginfo[i].Used;
			Labels[i].ToolTip = arginfo[i].Used && !string.IsNullOrEmpty(arginfo[i].ToolTip) ? arginfo[i].ToolTip : null;
		}

		private static bool ArgumentInfosMatch(ArgumentInfo[] a, ArgumentInfo[] b)
		{
			if(a.Length != b.Length) return false;
			bool haveusedargs = false;   // arguments are still reset when none is used
			for(int i = 0; i < a.Length; i++)
			{
				if(a[i].Used != b[i].Used || a[i].Type != b[i].Type || a[i].Title.ToUpperInvariant() != b[i].Title.ToUpperInvariant()) return false;
				haveusedargs |= a[i].Used;
			}
			return haveusedargs;
		}

		#endregion
	}
}
