using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// Syntax highlighting for the script editor (ACS, DECORATE, ZScript, MODELDEF...). The words come from the script configuration
/// (keywords, constants, properties); comments, strings, numbers and preprocessor lines are C-like, which all of these languages are.
/// </summary>
public sealed class ScriptSyntaxColorizer : DocumentColorizingTransformer
{
    private readonly ScriptConfiguration config;
    private readonly StringComparer comparer;
    private readonly HashSet<string> keywords, constants, properties;
    // For each line: does it start inside a /* */ comment? Rebuilt when the text changes.
    private List<bool> startsincomment = new List<bool>();
    private bool dirty = true;

    // The colors are the program's (Preferences > Script editor), like UDB's styles; refreshed with RefreshColors
    private IBrush plain, keyword, constant, property, comment, str, literal, include;

    // 0 = none (plain text), 1 = C-like (comments // and /* */, #directives), 2 = Perl-like (# comments)
    private readonly int family;

    public ScriptSyntaxColorizer(ScriptConfiguration config)
    {
        this.config = config;
        comparer = config.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        keywords = new HashSet<string>(config.Keywords, comparer);
        constants = new HashSet<string>(config.Constants, comparer);
        properties = new HashSet<string>(config.Properties, comparer);
        int lexer = (int)config.Lexer;
        family = lexer == 1 || lexer == 0 ? 0 : lexer == 6 ? 2 : 1;
        RefreshColors();
    }

    public void Invalidate() { dirty = true; }

    public void RefreshColors()
    {
        ColorCollection c = General.Colors;
        plain = Brush(c.PlainText); keyword = Brush(c.Keywords); constant = Brush(c.Constants); property = Brush(c.Properties);
        comment = Brush(c.Comments); str = Brush(c.Strings); literal = Brush(c.Literals); include = Brush(c.Includes);
    }

    private static IBrush Brush(PixelColor color) => new SolidColorBrush(Color.FromArgb(255, color.r, color.g, color.b));

    private void Rebuild()
    {
        dirty = false;
        startsincomment = new List<bool>();
        if(CurrentContext == null) return;
        bool incomment = false;
        foreach(DocumentLine line in CurrentContext.Document.Lines)
        {
            startsincomment.Add(incomment);
            string text = CurrentContext.Document.GetText(line);
            incomment = family == 1 && EndsInComment(text, incomment);
        }
    }

    private static bool EndsInComment(string text, bool incomment)
    {
        int i = 0;
        while(i < text.Length)
        {
            if(incomment)
            {
                int end = text.IndexOf("*/", i, StringComparison.Ordinal);
                if(end < 0) return true;
                i = end + 2;
                incomment = false;
            }
            else
            {
                char c = text[i];
                if(c == '"') { i = SkipString(text, i); continue; }
                if(c == '/' && i + 1 < text.Length)
                {
                    if(text[i + 1] == '/') return false;
                    if(text[i + 1] == '*') { incomment = true; i += 2; continue; }
                }
                i++;
            }
        }
        return incomment;
    }

    private static int SkipString(string text, int i)
    {
        i++;
        while(i < text.Length && text[i] != '"') { if(text[i] == '\\') i++; i++; }
        return Math.Min(i + 1, text.Length);
    }

    private static bool IsWordChar(char c, string extra) => char.IsLetterOrDigit(c) || c == '_' || (extra != null && extra.IndexOf(c) >= 0);

    protected override void ColorizeLine(DocumentLine line)
    {
        if(family == 0) return;
        if(dirty || startsincomment.Count != CurrentContext.Document.LineCount) Rebuild();
        if(line.LineNumber - 1 >= startsincomment.Count) return;

        string text = CurrentContext.Document.GetText(line);
        int offset = line.Offset;
        bool incomment = startsincomment[line.LineNumber - 1];
        string extra = config.ExtraWordCharacters;
        int i = 0;

        void Paint(int from, int to, IBrush brush)
        {
            if(to > from) ChangeLinePart(offset + from, offset + to, e => e.TextRunProperties.SetForegroundBrush(brush));
        }

        // A directive line (#include, #define, #region...) in the C-like languages
        int firstnonspace = 0;
        while(firstnonspace < text.Length && char.IsWhiteSpace(text[firstnonspace])) firstnonspace++;
        bool directive = family == 1 && !incomment && firstnonspace < text.Length && text[firstnonspace] == '#';

        while(i < text.Length)
        {
            if(incomment)
            {
                int end = text.IndexOf("*/", i, StringComparison.Ordinal);
                int stop = end < 0 ? text.Length : end + 2;
                Paint(i, stop, comment);
                i = stop;
                incomment = end < 0;
                continue;
            }

            char c = text[i];
            if(family == 2 && c == '#') { Paint(i, text.Length, comment); return; }
            if(family == 1 && c == '/' && i + 1 < text.Length && text[i + 1] == '/') { Paint(i, text.Length, comment); return; }
            if(family == 1 && c == '/' && i + 1 < text.Length && text[i + 1] == '*') { incomment = true; Paint(i, i + 2, comment); i += 2; continue; }
            if(c == '"') { int end = SkipString(text, i); Paint(i, end, directive ? include : str); i = end; continue; }
            if(c == '\'' && family == 1)
            {
                int end = i + 1;
                while(end < text.Length && text[end] != '\'') { if(text[end] == '\\') end++; end++; }
                end = Math.Min(end + 1, text.Length);
                Paint(i, end, literal);
                i = end;
                continue;
            }
            if(directive && i >= firstnonspace) { Paint(i, DirectiveEnd(text, i), include); i = DirectiveEnd(text, i); continue; }
            if(char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                int s = i;
                while(i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '.')) i++;
                Paint(s, i, literal);
                continue;
            }
            if(IsWordChar(c, extra))
            {
                int s = i;
                while(i < text.Length && IsWordChar(text[i], extra)) i++;
                string word = text.Substring(s, i - s);
                if(keywords.Contains(word)) Paint(s, i, keyword);
                else if(constants.Contains(word)) Paint(s, i, constant);
                else if(properties.Contains(word)) Paint(s, i, property);
                continue;
            }
            i++;
        }
    }

    // A directive runs to the end of the line, or to a comment that starts on it
    private static int DirectiveEnd(string text, int from)
    {
        int comment = text.IndexOf("//", from, StringComparison.Ordinal);
        return comment < 0 ? text.Length : comment;
    }
}
