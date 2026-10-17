namespace DoomBuilder.App.Input;

/// <summary>
/// Whether the main window is the one in use. UDB keeps a flag set by the form's Activated and Deactivate events and its modes only open
/// their edit dialogs while it is true (so a mouse button released after switching to another window does nothing). The platform's
/// "is active" can stay false after a modal dialog closes (a compositor that does not hand the activation back), while the user keeps
/// working in the window: then every edit dialog after the first one silently never opened. Input that reaches the window itself
/// proves that it is the one in use.
/// </summary>
public sealed class WindowActivity
{
    private bool active;

    public void Activated() => active = true;
    public void Deactivated() => active = false;

    /// <summary>A mouse button or key reached the main window.</summary>
    public void InputReceived() => active = true;

    /// <param name="platformactive">The window system's own answer.</param>
    public bool IsActive(bool platformactive) => platformactive || active;
}
