using System.Windows.Forms;
using Avalonia.Input;
using DoomBuilder.App.Input;
using Xunit;

namespace DoomBuilder.App.Tests;

public class KeyMapTests
{
    [Theory]
    [InlineData(Key.A, 65)]
    [InlineData(Key.Z, 90)]
    [InlineData(Key.D0, 48)]
    [InlineData(Key.D9, 57)]
    [InlineData(Key.NumPad0, 96)]
    [InlineData(Key.NumPad9, 105)]
    [InlineData(Key.F1, 112)]
    [InlineData(Key.F12, 123)]
    [InlineData(Key.F24, 135)]
    [InlineData(Key.Escape, 27)]
    [InlineData(Key.Space, 32)]
    [InlineData(Key.Return, 13)]
    [InlineData(Key.Back, 8)]
    [InlineData(Key.Tab, 9)]
    [InlineData(Key.Left, 37)]
    [InlineData(Key.Up, 38)]
    [InlineData(Key.Right, 39)]
    [InlineData(Key.Down, 40)]
    [InlineData(Key.PageUp, 33)]
    [InlineData(Key.PageDown, 34)]
    [InlineData(Key.Home, 36)]
    [InlineData(Key.End, 35)]
    [InlineData(Key.Insert, 45)]
    [InlineData(Key.Delete, 46)]
    [InlineData(Key.OemOpenBrackets, 219)]
    [InlineData(Key.OemCloseBrackets, 221)]
    [InlineData(Key.OemComma, 188)]
    [InlineData(Key.OemPeriod, 190)]
    [InlineData(Key.OemMinus, 189)]
    [InlineData(Key.OemPlus, 187)]
    [InlineData(Key.OemTilde, 192)]
    [InlineData(Key.OemSemicolon, 186)]
    [InlineData(Key.OemQuotes, 222)]
    public void Keys_map_to_the_windows_virtual_key_codes_that_settings_are_stored_as(Key key, int expected)
        => Assert.Equal(expected, (int)KeyMap.ToKeyCode(key));

    [Fact]
    public void Left_and_right_modifier_keys_share_one_generic_code()
    {
        Assert.Equal(Keys.ShiftKey, KeyMap.ToKeyCode(Key.LeftShift));
        Assert.Equal(Keys.ShiftKey, KeyMap.ToKeyCode(Key.RightShift));
        Assert.Equal(Keys.ControlKey, KeyMap.ToKeyCode(Key.LeftCtrl));
        Assert.Equal(Keys.ControlKey, KeyMap.ToKeyCode(Key.RightCtrl));
        Assert.Equal(Keys.Menu, KeyMap.ToKeyCode(Key.LeftAlt));
        Assert.Equal(Keys.Menu, KeyMap.ToKeyCode(Key.RightAlt));
    }

    [Fact]
    public void Unknown_keys_map_to_none()
    {
        Assert.Equal(Keys.None, KeyMap.ToKeyCode(Key.None));
        Assert.Equal(Keys.None, KeyMap.ToKeyData(Key.None, KeyModifiers.Control, macos: false));
    }

    [Fact]
    public void Modifiers_become_the_winforms_modifier_bits()
    {
        Keys data = KeyMap.ToKeyData(Key.K, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt, macos: false);
        Assert.Equal(Keys.K | Keys.Control | Keys.Shift | Keys.Alt, data);
    }

    [Fact]
    public void Matches_the_shortcut_values_the_default_bindings_use()
    {
        // From UDB's Actions.cfg: Ctrl+Shift+G is 196679, Ctrl+K style bindings are Control (0x20000) + the key code
        Assert.Equal(196679, (int)KeyMap.ToKeyData(Key.G, KeyModifiers.Control | KeyModifiers.Shift, macos: false));
        Assert.Equal(0x20000 + 75, (int)KeyMap.ToKeyData(Key.K, KeyModifiers.Control, macos: false));
    }

    [Fact]
    public void Pressing_a_modifier_includes_its_own_bit()
    {
        Assert.Equal(Keys.ShiftKey | Keys.Shift, KeyMap.ToKeyData(Key.LeftShift, KeyModifiers.Shift, macos: false));
        Assert.Equal(Keys.ControlKey | Keys.Control, KeyMap.ToKeyData(Key.LeftCtrl, KeyModifiers.Control, macos: false));
    }

    [Fact]
    public void On_macos_command_counts_as_control()
    {
        Assert.Equal(Keys.Z | Keys.Control, KeyMap.ToKeyData(Key.Z, KeyModifiers.Meta, macos: true));
        Assert.Equal(Keys.Z | Keys.Control | Keys.Shift, KeyMap.ToKeyData(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift, macos: true));

        // pressing Command itself behaves like pressing Control
        Assert.Equal(Keys.ControlKey | Keys.Control, KeyMap.ToKeyData(Key.LWin, KeyModifiers.Meta, macos: true));
    }

    [Fact]
    public void Elsewhere_the_meta_key_is_not_control()
    {
        Assert.Equal(Keys.Z, KeyMap.ToKeyData(Key.Z, KeyModifiers.Meta, macos: false));
    }

    [Fact]
    public void Every_mapped_key_survives_the_modifier_bits_round_trip()
    {
        foreach (Key key in System.Enum.GetValues<Key>())
        {
            Keys code = KeyMap.ToKeyCode(key);
            if (code == Keys.None) continue;
            Keys data = KeyMap.ToKeyData(key, KeyModifiers.None, macos: false);
            Assert.Equal(code, data & Keys.KeyCode);
        }
    }
}
