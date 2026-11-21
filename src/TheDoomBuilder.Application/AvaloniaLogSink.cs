using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Logging;
using CodeImp.DoomBuilder;

namespace TheDoomBuilder.Application;

/// <summary>
/// Copies what Avalonia itself reports (warnings and errors, and everything of the OpenGL area) to UDBuilder.log, next to the
/// sink it already had. Avalonia's own log only goes to the trace output, which nobody sees when the app is opened from the
/// desktop; it is where it says why a platform could not give a GL context.
/// </summary>
internal sealed class AvaloniaLogSink : ILogSink
{
    private const int MaxMessages = 200;
    private static readonly Regex Placeholder = new Regex(@"\{[^{}]*\}", RegexOptions.CultureInvariant);

    private readonly ILogSink inner;
    private readonly HashSet<string> seen = new HashSet<string>();

    public AvaloniaLogSink(ILogSink inner) { this.inner = inner; }

    private static bool Wanted(LogEventLevel level, string area) =>
        level >= LogEventLevel.Warning || string.Equals(area, "OpenGL", StringComparison.OrdinalIgnoreCase);

    public bool IsEnabled(LogEventLevel level, string area) => (inner?.IsEnabled(level, area) ?? false) || Wanted(level, area);

    public void Log(LogEventLevel level, string area, object source, string messageTemplate)
    {
        if (inner != null && inner.IsEnabled(level, area)) inner.Log(level, area, source, messageTemplate);
        Write(level, area, source, messageTemplate, null);
    }

    public void Log(LogEventLevel level, string area, object source, string messageTemplate, params object[] propertyValues)
    {
        if (inner != null && inner.IsEnabled(level, area)) inner.Log(level, area, source, messageTemplate, propertyValues);
        Write(level, area, source, messageTemplate, propertyValues);
    }

    private void Write(LogEventLevel level, string area, object source, string template, object[] values)
    {
        if (!Wanted(level, area)) return;

        int next = 0;
        string text = values == null ? template : Placeholder.Replace(template ?? string.Empty, m => next < values.Length ? Convert.ToString(values[next++]) : m.Value);
        string line = "[Avalonia " + level + "/" + area + "] " + text + (source != null ? " (" + source.GetType().Name + ")" : "");

        lock (seen)
        {
            if (seen.Count >= MaxMessages || !seen.Add(line)) return;
        }
        try { General.WriteLogLine(line); }
        catch (Exception) { }
    }
}
