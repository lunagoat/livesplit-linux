using LiveSplit.Model.Input;
using System.Windows.Forms;
using Xunit;

namespace LiveSplit.Tests.Input;

public class KeyEventTranslatorMust
{
    private const int KeyA = 30;
    private const int KeyLeftCtrl = 29;
    private const int KeyRightCtrl = 97;
    private const int KeyLeftShift = 42;
    private const int KeyLeftAlt = 56;
    private const int KeyKp1 = 79;
    private const int KeyGrave = 41;

    [Theory]
    [InlineData(KeyA, Keys.A)]
    [InlineData(KeyKp1, Keys.NumPad1)]
    [InlineData(KeyGrave, Keys.Oemtilde)]
    [InlineData(1, Keys.Escape)]
    [InlineData(59, Keys.F1)]
    [InlineData(88, Keys.F12)]
    [InlineData(183, Keys.F13)]
    [InlineData(194, Keys.F24)]
    [InlineData(57, Keys.Space)]
    [InlineData(2, Keys.D1)]
    [InlineData(11, Keys.D0)]
    [InlineData(104, Keys.PageUp)]
    public void MapEvdevCodesToKeys(int code, Keys expected)
    {
        Assert.True(LinuxKeyMap.TryGetKeys(code, out Keys key));
        Assert.Equal(expected, key);
    }

    [Fact]
    public void ReportPlainKeyPresses()
    {
        var translator = new KeyEventTranslator();

        Assert.True(translator.TryProcess(KeyA, 1, out Keys key));
        Assert.Equal(Keys.A, key);
    }

    [Fact]
    public void IgnoreReleasesAndAutoRepeat()
    {
        var translator = new KeyEventTranslator();

        translator.TryProcess(KeyA, 1, out _);

        Assert.False(translator.TryProcess(KeyA, 2, out _));
        Assert.False(translator.TryProcess(KeyA, 1, out _));
        Assert.False(translator.TryProcess(KeyA, 0, out _));
        Assert.True(translator.TryProcess(KeyA, 1, out _));
    }

    [Fact]
    public void ReportHeldModifiersWithTheKey()
    {
        var translator = new KeyEventTranslator();

        translator.TryProcess(KeyLeftCtrl, 1, out _);
        translator.TryProcess(KeyLeftAlt, 1, out _);
        Assert.True(translator.TryProcess(KeyA, 1, out Keys key));

        Assert.Equal(Keys.A | Keys.Control | Keys.Alt, key);
    }

    [Fact]
    public void ReportModifierKeysThemselvesLikeTheWindowsHook()
    {
        var translator = new KeyEventTranslator();

        Assert.True(translator.TryProcess(KeyLeftCtrl, 1, out Keys control));
        Assert.Equal(Keys.ControlKey, control);

        Assert.True(translator.TryProcess(KeyLeftShift, 1, out Keys shift));
        Assert.Equal(Keys.ShiftKey | Keys.Control, shift);
    }

    [Fact]
    public void NotReportASecondControlKeyWhileOneIsHeld()
    {
        var translator = new KeyEventTranslator();

        translator.TryProcess(KeyLeftCtrl, 1, out _);

        Assert.False(translator.TryProcess(KeyRightCtrl, 1, out _));
    }

    [Fact]
    public void ForgetModifiersOnceReleased()
    {
        var translator = new KeyEventTranslator();

        translator.TryProcess(KeyLeftCtrl, 1, out _);
        translator.TryProcess(KeyLeftCtrl, 0, out _);
        translator.TryProcess(KeyA, 1, out Keys key);

        Assert.Equal(Keys.A, key);
        Assert.Equal(Keys.None, translator.Modifiers);
    }

    [Fact]
    public void TrackWhichKeysAreDown()
    {
        var translator = new KeyEventTranslator();

        translator.TryProcess(KeyRightCtrl, 1, out _);
        translator.TryProcess(KeyA, 1, out _);

        Assert.True(translator.IsKeyDown(Keys.A));
        Assert.True(translator.IsKeyDown(Keys.ControlKey));
        Assert.True(translator.IsKeyDown(Keys.RControlKey));
        Assert.False(translator.IsKeyDown(Keys.LControlKey));
        Assert.False(translator.IsKeyDown(Keys.B));

        translator.TryProcess(KeyA, 0, out _);
        Assert.False(translator.IsKeyDown(Keys.A));
    }

    [Fact]
    public void IgnoreUnknownCodes()
    {
        var translator = new KeyEventTranslator();

        Assert.False(translator.TryProcess(0x2FF, 1, out _));
    }

    [Fact]
    public void RecognizeKeyboardsFromSysfsCapabilities()
    {
        const string keyboardKeys = "402000000 3803078f800d001 feffffdfffefffff fffffffffffffffe";
        const string mouseKeys = "1f0000 0 0 0 0";

        Assert.True(EvdevKeyboardBackend.IsKeyboard("120013", keyboardKeys));
        Assert.False(EvdevKeyboardBackend.IsKeyboard("17", mouseKeys));
        Assert.False(EvdevKeyboardBackend.IsKeyboard("5", keyboardKeys));
    }

    [Fact]
    public void ReadSysfsBitmasks()
    {
        Assert.True(EvdevKeyboardBackend.HasBit("10", 4));
        Assert.False(EvdevKeyboardBackend.HasBit("10", 3));
        Assert.True(EvdevKeyboardBackend.HasBit("1 0", 64));
        Assert.False(EvdevKeyboardBackend.HasBit("1 0", 0));
        Assert.False(EvdevKeyboardBackend.HasBit("1", 64));
    }
}
