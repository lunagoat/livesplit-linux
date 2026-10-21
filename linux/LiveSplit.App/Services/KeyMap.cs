using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.Services;

/// <summary>Avalonia key events to the WinForms-compatible <see cref="Keys"/> the hotkey settings use.</summary>
public static class KeyMap
{
    private static readonly Dictionary<Key, Keys> Special = new()
    {
        [Key.Back] = Keys.Back,
        [Key.Return] = Keys.Return,
        [Key.Escape] = Keys.Escape,
        [Key.PageUp] = Keys.PageUp,
        [Key.PageDown] = Keys.PageDown,
        [Key.Capital] = Keys.CapsLock,
        [Key.Scroll] = Keys.Scroll,
        [Key.Pause] = Keys.Pause,
        [Key.PrintScreen] = Keys.PrintScreen,
        [Key.LeftShift] = Keys.LShiftKey,
        [Key.RightShift] = Keys.RShiftKey,
        [Key.LeftCtrl] = Keys.LControlKey,
        [Key.RightCtrl] = Keys.RControlKey,
        [Key.LeftAlt] = Keys.LMenu,
        [Key.RightAlt] = Keys.RMenu,
        [Key.LWin] = Keys.LWin,
        [Key.RWin] = Keys.RWin,
        [Key.Apps] = Keys.Apps,
        [Key.OemSemicolon] = Keys.OemSemicolon,
        [Key.OemPlus] = Keys.Oemplus,
        [Key.OemComma] = Keys.Oemcomma,
        [Key.OemMinus] = Keys.OemMinus,
        [Key.OemPeriod] = Keys.OemPeriod,
        [Key.OemQuestion] = Keys.OemQuestion,
        [Key.OemTilde] = Keys.Oemtilde,
        [Key.OemOpenBrackets] = Keys.OemOpenBrackets,
        [Key.OemPipe] = Keys.OemPipe,
        [Key.OemCloseBrackets] = Keys.OemCloseBrackets,
        [Key.OemQuotes] = Keys.OemQuotes,
        [Key.OemBackslash] = Keys.OemBackslash,
    };

    public static Keys ToKeys(Key key)
    {
        if (Special.TryGetValue(key, out Keys mapped))
        {
            return mapped;
        }

        // A..Z, D0..D9, NumPad0..9, F1..F24, Tab, Space, Home, End, Left, Up, Right, Down, Insert, Delete, NumLock,
        // Multiply, Add, Subtract, Decimal, Divide share their names with WinForms.
        return Enum.TryParse(key.ToString(), out Keys parsed) && Enum.IsDefined(parsed) ? parsed : Keys.None;
    }

    public static Keys ToKeys(Key key, KeyModifiers modifiers)
    {
        Keys result = ToKeys(key);
        if (result == Keys.None)
        {
            return Keys.None;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= Keys.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= Keys.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= Keys.Alt;
        }

        return result;
    }
}
