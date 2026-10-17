using System.Windows.Forms;
using Xunit;
using CodeImp.DoomBuilder.Actions;

namespace DoomBuilder.Core.Tests;

/// <summary>The names of shortcut keys as shown in menus, tooltips and hints.</summary>
public class KeyNamesTests
{
    [Theory]
    [InlineData((int)Keys.E, "E")]
    [InlineData((int)Keys.D3, "3")]
    [InlineData((int)Keys.Space, "Space")]
    [InlineData((int)Keys.Prior, "PageUp")]
    [InlineData((int)Keys.Next, "PageDown")]
    [InlineData((int)Keys.F5, "F5")]
    [InlineData((int)Keys.OemMinus, "-")]
    [InlineData((int)Keys.Back, "Backspace")]
    [InlineData((int)Keys.Control | (int)Keys.S, "Ctrl+S")]
    [InlineData((int)Keys.Control | (int)Keys.Shift | (int)Keys.Z, "Ctrl+Shift+Z")]
    [InlineData((int)Keys.Alt | (int)Keys.Return, "Alt+Return")]
    public void A_key_is_shown_by_its_name_not_its_code(int key, string expected)
    {
        Assert.Equal(expected, CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(key));
    }

    [Fact]
    public void No_key_is_an_empty_description()
    {
        Assert.Equal("", CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(0));
    }
}
