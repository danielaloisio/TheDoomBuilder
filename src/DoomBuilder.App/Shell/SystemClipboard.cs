using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using System.Windows.Forms;
using DoomBuilder.UI;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The system clipboard for the editor's synchronous clipboard code (the copied geometry travels as text). Avalonia's clipboard is
/// asynchronous, so each call waits for it in a nested dispatcher loop (<see cref="DialogPump"/>).
/// </summary>
internal sealed class SystemClipboard : IClipboardProvider
{
    private readonly Func<TopLevel> owner;

    public SystemClipboard(Func<TopLevel> owner) { this.owner = owner; }

    private IClipboard Clip => owner()?.Clipboard;

    public string GetText()
    {
        IClipboard clipboard = Clip;
        if (clipboard == null) throw new InvalidOperationException("no clipboard");
        return DialogPump.Run(async () => await clipboard.TryGetTextAsync());
    }

    public void SetText(string text)
    {
        IClipboard clipboard = Clip;
        if (clipboard == null) throw new InvalidOperationException("no clipboard");
        DialogPump.Run(async () => { await clipboard.SetTextAsync(text); return true; });
    }
}
