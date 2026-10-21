using System;
using System.Windows.Forms;
using Xunit;

namespace LiveSplit.Tests.Input;

public class KeysMust
{
    [Theory]
    [InlineData(Keys.Back, 8)]
    [InlineData(Keys.Return, 13)]
    [InlineData(Keys.Escape, 27)]
    [InlineData(Keys.Space, 32)]
    [InlineData(Keys.D0, 0x30)]
    [InlineData(Keys.A, 0x41)]
    [InlineData(Keys.Z, 0x5A)]
    [InlineData(Keys.NumPad1, 0x61)]
    [InlineData(Keys.F1, 0x70)]
    [InlineData(Keys.F24, 0x87)]
    [InlineData(Keys.LShiftKey, 0xA0)]
    [InlineData(Keys.Oemtilde, 0xC0)]
    [InlineData(Keys.OemBackslash, 0xE2)]
    [InlineData(Keys.OemClear, 0xFE)]
    [InlineData(Keys.Shift, 0x10000)]
    [InlineData(Keys.Control, 0x20000)]
    [InlineData(Keys.Alt, 0x40000)]
    public void HaveTheSameNumericValuesAsWindowsForms(Keys key, int value)
    {
        Assert.Equal(value, (int)key);
    }

    [Theory]
    [InlineData("NumPad1", Keys.NumPad1)]
    [InlineData("A, Control", Keys.A | Keys.Control)]
    [InlineData("Oemtilde, Shift", Keys.Oemtilde | Keys.Shift)]
    [InlineData("F5, Control, Alt", Keys.F5 | Keys.Control | Keys.Alt)]
    [InlineData("ControlKey", Keys.ControlKey)]
    public void ParseTheNamesPersistedInSettingsFiles(string text, Keys expected)
    {
        Assert.Equal(expected, Enum.Parse<Keys>(text, true));
    }

    [Theory]
    [InlineData(Keys.NumPad1, "NumPad1")]
    [InlineData(Keys.A | Keys.Control, "A, Control")]
    [InlineData(Keys.F5 | Keys.Control | Keys.Alt, "F5, Control, Alt")]
    public void FormatTheNamesPersistedInSettingsFiles(Keys key, string expected)
    {
        Assert.Equal(expected, key.ToString());
    }

    [Fact]
    public void ExposeModifierAccessorsOnKeyEventArgs()
    {
        var args = new KeyEventArgs(Keys.A | Keys.Control | Keys.Shift);

        Assert.Equal(Keys.A, args.KeyCode);
        Assert.True(args.Control);
        Assert.True(args.Shift);
        Assert.False(args.Alt);
        Assert.Equal(Keys.Control | Keys.Shift, args.Modifiers);
        Assert.Equal(Keys.A | Keys.Control | Keys.Shift, args.KeyData);
    }
}
