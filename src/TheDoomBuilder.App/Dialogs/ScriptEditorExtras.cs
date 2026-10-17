using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Rendering;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// Indentation of a new line (UDB's InsertCheck/CharAdded logic): like the line above, one more after an opening block character, a closing
/// character between a block's braces goes to a line of its own, and in Allman style the opening brace moves to its own line.
/// </summary>
public sealed class ScriptIndentationStrategy : IIndentationStrategy
{
    private readonly ScriptConfiguration config;
    private readonly TextArea area;

    public ScriptIndentationStrategy(ScriptConfiguration config, TextArea area) { this.config = config; this.area = area; }

    private static string IndentOf(string text) => new string(text.TakeWhile(c => c == ' ' || c == '\t').ToArray());

    private string OneIndent()
    {
        int width = Math.Max(1, General.Settings.ScriptTabWidth);
        return General.Settings.ScriptUseTabs ? "\t" : new string(' ', width);
    }

    public void IndentLine(TextDocument document, DocumentLine line)
    {
        DocumentLine previous = line.PreviousLine;
        if(previous == null) return;

        string prevtext = document.GetText(previous.Offset, previous.Length);
        string indent = IndentOf(prevtext);
        string open = config.CodeBlockOpen, close = config.CodeBlockClose;
        bool hasblock = !string.IsNullOrEmpty(open) && !string.IsNullOrEmpty(close);
        string rest = document.GetText(line.Offset, line.Length);      // what was after the caret when Enter was pressed
        string trimmedprev = prevtext.TrimEnd();

        // Allman style: "if(x) {" + Enter puts the brace on its own line
        int caretline = line.LineNumber;
        if(hasblock && General.Settings.ScriptAllmanStyle && trimmedprev.EndsWith(open, StringComparison.Ordinal))
        {
            string before = trimmedprev.Substring(0, trimmedprev.Length - open.Length);
            if(before.Trim().Length > 0)
            {
                // Rewrite the previous line without the brace and put the brace on the line in between
                document.Replace(previous.Offset, previous.Length, before.TrimEnd() + Environment.NewLine + indent + open);
                previous = line.PreviousLine;       // the line object follows the edit: it is now one line further down
                prevtext = document.GetText(previous.Offset, previous.Length);
                trimmedprev = prevtext.TrimEnd();
            }
        }

        bool opened = hasblock && trimmedprev.EndsWith(open, StringComparison.Ordinal);
        string lineindent = indent + (opened ? OneIndent() : "");
        int start = line.Offset;
        document.Replace(start, 0, lineindent);
        int caret = start + lineindent.Length;

        // "{|}" + Enter: the closing character moves down, the caret stays on the indented line in between
        if(opened && rest.TrimStart().StartsWith(close, StringComparison.Ordinal))
        {
            int closeoffset = caret + (rest.Length - rest.TrimStart().Length);
            document.Replace(caret, closeoffset - caret, Environment.NewLine + indent);
        }
        area.Caret.Offset = caret;
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        for(int number = beginLine; number <= endLine; number++)
        {
            DocumentLine line = document.GetLineByNumber(number);
            string text = document.GetText(line.Offset, line.Length);
            int have = text.Length - text.TrimStart(' ', '\t').Length;
            DocumentLine previous = line.PreviousLine;
            string indent = previous == null ? "" : IndentOf(document.GetText(previous.Offset, previous.Length));
            document.Replace(line.Offset, have, indent);
        }
    }
}

/// <summary>Draws the pair of braces at the caret (or a lone brace in the "bad" color), and the other uses of the selected word.</summary>
public sealed class ScriptHighlightRenderer : IBackgroundRenderer
{
    private readonly ScriptConfiguration config;
    private readonly TextArea area;
    private int first = -1, second = -1;       // the matching braces; second = -2 when there is no match
    private string word;
    private readonly List<(int Offset, int Length)> words = new List<(int, int)>();

    public ScriptHighlightRenderer(ScriptConfiguration config, TextArea area) { this.config = config; this.area = area; }

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>The brace positions found for the caret (for tests).</summary>
    public (int First, int Second) Braces => (first, second);
    public IReadOnlyList<(int Offset, int Length)> WordMatches => words;

    private static bool Matches(char a, char b) => (a == '(' && b == ')') || (a == '[' && b == ']') || (a == '{' && b == '}')
                                                  || (a == ')' && b == '(') || (a == ']' && b == '[') || (a == '}' && b == '{');

    /// <summary>Looks at the caret: a brace before or after it, and a selected whole word.</summary>
    public void Update()
    {
        first = second = -1;
        TextDocument doc = area.Document;
        int caret = area.Caret.Offset;
        if(doc != null && config.BraceChars.Count > 0)
        {
            int pos = -1;
            if(caret > 0 && config.BraceChars.Contains(doc.GetCharAt(caret - 1))) pos = caret - 1;
            else if(caret < doc.TextLength && config.BraceChars.Contains(doc.GetCharAt(caret))) pos = caret;
            if(pos >= 0)
            {
                first = pos;
                second = FindMatch(doc, pos);
                if(second < 0) second = -2;
            }
        }

        // The other places the selected word is used
        words.Clear();
        word = null;
        if(doc != null && area.Selection.Length > 0 && !area.Selection.IsMultiline)
        {
            string selected = area.Selection.GetText();
            int start = area.Selection.SurroundingSegment.Offset;
            if(selected.Length > 0 && selected.All(c => char.IsLetterOrDigit(c) || c == '_') && IsWholeWord(doc, start, selected.Length))
            {
                word = selected;
                string text = doc.Text;
                int at = 0;
                while((at = text.IndexOf(selected, at, StringComparison.Ordinal)) >= 0)
                {
                    if(at != start && IsWholeWord(doc, at, selected.Length)) words.Add((at, selected.Length));
                    at += selected.Length;
                }
            }
        }
    }

    private static bool IsWholeWord(TextDocument doc, int start, int length)
    {
        bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        if(start > 0 && Word(doc.GetCharAt(start - 1))) return false;
        if(start + length < doc.TextLength && Word(doc.GetCharAt(start + length))) return false;
        return true;
    }

    // The brace that closes (or opens) this one, skipping nested pairs; -1 when there is none
    private static int FindMatch(TextDocument doc, int pos)
    {
        char brace = doc.GetCharAt(pos);
        bool forward = brace == '(' || brace == '[' || brace == '{';
        int depth = 0;
        for(int i = pos; i >= 0 && i < doc.TextLength; i += forward ? 1 : -1)
        {
            char c = doc.GetCharAt(i);
            if(c == brace) depth++;
            else if(Matches(brace, c)) { depth--; if(depth == 0) return i; }
        }
        return -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if(textView.Document == null) return;
        ColorCollection colors = General.Colors;
        IBrush Fill(PixelColor c, byte alpha = 255) => new SolidColorBrush(Color.FromArgb(alpha, c.r, c.g, c.b));

        void Mark(int offset, int length, IBrush brush)
        {
            var segment = new TextSegment { StartOffset = offset, Length = length };
            foreach(Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                drawingContext.FillRectangle(brush, rect);
        }

        if(first >= 0)
        {
            IBrush brush = Fill(second >= 0 ? colors.ScriptBraceHighlight : colors.ScriptBadBraceHighlight);
            Mark(first, 1, brush);
            if(second >= 0) Mark(second, 1, brush);
        }
        if(word != null)
        {
            IBrush brush = Fill(colors.ScriptIndicator, 70);
            foreach(var (offset, length) in words) Mark(offset, length, brush);
        }
    }
}

/// <summary>Folds code blocks (the block open/close characters of the script type, outside comments and strings) and #region ... #endregion.</summary>
public static class ScriptFolding
{
    public static IEnumerable<NewFolding> Find(string text, ScriptConfiguration config)
    {
        var result = new List<NewFolding>();
        string open = string.IsNullOrEmpty(config.CodeBlockOpen) ? "{" : config.CodeBlockOpen;
        string close = string.IsNullOrEmpty(config.CodeBlockClose) ? "}" : config.CodeBlockClose;
        var blocks = new Stack<int>();
        var regions = new Stack<int>();
        bool linecomment = false, blockcomment = false, instring = false;

        for(int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if(c == '\n') { linecomment = false; instring = false; }
            if(linecomment) continue;
            if(blockcomment) { if(c == '*' && i + 1 < text.Length && text[i + 1] == '/') { blockcomment = false; i++; } continue; }
            if(instring) { if(c == '\\') i++; else if(c == '"') instring = false; continue; }

            if(c == '/' && i + 1 < text.Length && text[i + 1] == '/') { linecomment = true; continue; }
            if(c == '/' && i + 1 < text.Length && text[i + 1] == '*') { blockcomment = true; i++; continue; }
            if(c == '"') { instring = true; continue; }

            if(string.CompareOrdinal(text, i, open, 0, open.Length) == 0) { blocks.Push(i); continue; }
            if(string.CompareOrdinal(text, i, close, 0, close.Length) == 0 && blocks.Count > 0)
            {
                int start = blocks.Pop();
                // Only blocks over more than one line can be folded
                if(text.IndexOf('\n', start, i - start) >= 0) result.Add(new NewFolding(start, i + close.Length) { Name = "..." });
                continue;
            }

            // #region / #endregion at the start of a line
            if(c == '#' && IsLineStart(text, i))
            {
                if(string.CompareOrdinal(text, i, "#region", 0, 7) == 0) regions.Push(i);
                else if(string.CompareOrdinal(text, i, "#endregion", 0, 10) == 0 && regions.Count > 0)
                {
                    int start = regions.Pop();
                    int end = text.IndexOf('\n', i);
                    result.Add(new NewFolding(start, end < 0 ? text.Length : end) { Name = "#region" });
                }
            }
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }

    private static bool IsLineStart(string text, int i)
    {
        for(int k = i - 1; k >= 0 && text[k] != '\n'; k--) if(text[k] != ' ' && text[k] != '\t') return false;
        return true;
    }
}
