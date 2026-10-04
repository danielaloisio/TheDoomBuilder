using System.Text;
using System.Text.RegularExpressions;

namespace DoomBuilder.App.Shell;

/// <summary>
/// UDB's edit-mode hints are written as RTF (they were shown in a RichTextBox). This keeps the text and drops the formatting,
/// which is enough for the status line; a rich hints panel can come with the dockers.
/// </summary>
public static class RtfText
{
    // \par, \line: line breaks. Other control words (\b, \i0, \fs20 ...) are formatting. \'xx is a code page character.
    private static readonly Regex HexEscape = new Regex(@"\\'([0-9a-fA-F]{2})", RegexOptions.Compiled);
    private static readonly Regex Breaks = new Regex(@"\\(par|line)\b ?", RegexOptions.Compiled);
    private static readonly Regex ControlWord = new Regex(@"\\[a-zA-Z]+-?\d* ?", RegexOptions.Compiled);
    private static readonly Regex Groups = new Regex(@"[{}]", RegexOptions.Compiled);

    public static string ToPlain(string rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return string.Empty;
        if (!rtf.TrimStart().StartsWith("{\\rtf")) return rtf;   // already plain text

        string text = HexEscape.Replace(rtf, m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        text = text.Replace("\\\\", "\u0001").Replace("\\{", "\u0002").Replace("\\}", "\u0003");   // escaped characters survive
        text = Breaks.Replace(text, "\n");
        text = ControlWord.Replace(text, string.Empty);
        text = Groups.Replace(text, string.Empty);
        text = text.Replace("\u0001", "\\").Replace("\u0002", "{").Replace("\u0003", "}");

        return new StringBuilder(text).Replace("\r", string.Empty).ToString().Trim();
    }
}
