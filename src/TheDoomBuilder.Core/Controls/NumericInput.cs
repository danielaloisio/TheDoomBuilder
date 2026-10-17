using System;
using System.Data;
using System.Globalization;

namespace CodeImp.DoomBuilder.Controls
{
	/// <summary>
	/// What UDB's NumericTextbox understands as a number, without the textbox: plain values, expressions "(128+64)*2.5", and
	/// relative values with the prefixes ++ / -- (add / subtract), +++ / --- (add / subtract, growing each time it is read for the
	/// next element), * and / (multiply / divide).
	/// </summary>
	public sealed class NumericInput
	{
		private const int RoundingPrecision = 4;
		private static readonly DataTable datatable = new DataTable();

		private int incrementstep = 1;

		public string Text { get; set; }
		public bool AllowNegative { get; set; }
		public bool AllowRelative { get; set; }
		public bool AllowDecimal { get; set; }
		public bool AllowExpressions { get; set; }

		public NumericInput(string text = "") { Text = text ?? ""; }

		/// <summary>Prefixed with +++, ---, ++, --, * or /.</summary>
		public bool IsRelative
		{
			get
			{
				string t = Text ?? "";
				return (t.Length > 3 && (t.StartsWith("+++") || t.StartsWith("---"))) ||
					   (t.Length > 2 && (t.StartsWith("++") || t.StartsWith("--"))) ||
					   (t.Length > 1 && (t.StartsWith("*") || t.StartsWith("/")));
			}
		}

		/// <summary>False for text that is neither a number nor (with expressions) a valid expression; "++" and "--" alone are fine.</summary>
		public bool IsValid
		{
			get
			{
				string t = Text ?? "";
				if(t.Length == 0) return true;
				if(AllowRelative && (t == "++" || t == "--")) return true;
				double unused;
				return TryGetValue(StripPrefixes(t), out unused);
			}
		}

		/// <summary>Starts the +++/--- steps over (call before reading the value for the first element).</summary>
		public void ResetIncrementStep() { incrementstep = 1; }

		public int GetResult(int original) { return GetResult(original, incrementstep++); }
		public int GetResult(int original, int step) { return (int)Math.Round(GetResultFloat(original, step)); }
		public double GetResultFloat(double original) { return GetResultFloat(original, incrementstep++); }

		public double GetResultFloat(double original, int step)
		{
			string t = Text ?? "";
			string part = StripPrefixes(t);
			if(part.Length == 0) return original;

			double result;
			if(AllowRelative)
			{
				if(t.StartsWith("+++")) return TryGetValue(part, out result) ? original + result * step : original;
				if(t.StartsWith("---"))
				{
					if(!TryGetValue(part, out result)) return original;
					double v = original - result * step;
					return (!AllowNegative && v < 0) ? original : v;
				}
				if(t.StartsWith("++")) return TryGetValue(part, out result) ? original + result : original;
				if(t.StartsWith("--"))
				{
					if(!TryGetValue(part, out result)) return original;
					double v = original - result;
					return (!AllowNegative && v < 0) ? original : v;
				}
				if(t.StartsWith("*"))
				{
					if(!TryGetValue(part, out result)) return original;
					double v = Math.Round(original * result, RoundingPrecision);
					return (!AllowNegative && v < 0) ? original : v;
				}
				if(t.StartsWith("/"))
				{
					if(!TryGetValue(part, out result) || result == 0.0) return original;
					double v = Math.Round(original / result, RoundingPrecision);
					return (!AllowNegative && v < 0) ? original : v;
				}
			}

			if(TryGetValue(part, out result)) return (!AllowNegative && result < 0) ? original : result;
			return original;
		}

		private string StripPrefixes(string input)
		{
			if(AllowRelative)
			{
				if(input.StartsWith("+++") || input.StartsWith("---")) return input.Substring(3);
				if(input.StartsWith("++") || input.StartsWith("--")) return input.Substring(2);
				if(input.StartsWith("*") || input.StartsWith("/")) return input.Substring(1);
			}
			return input;
		}

		private bool TryGetValue(string expression, out double value)
		{
			if(AllowExpressions)
			{
				try { expression = Convert.ToString(datatable.Compute(expression, null), CultureInfo.InvariantCulture); }
				catch(Exception) { value = 0; return false; }
			}
			return double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}
	}
}
