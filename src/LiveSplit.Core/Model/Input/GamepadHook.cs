using System;
using System.Collections.Generic;

namespace LiveSplit.Model.Input;

public struct GamepadButton
{
    public string GamepadName;
    public string Button;

    public GamepadButton(string gamepadName, string button)
    {
        GamepadName = gamepadName;
        Button = button;
    }

    public static bool operator ==(GamepadButton a, GamepadButton b)
    {
        return a.GamepadName == b.GamepadName && a.Button == b.Button;
    }

    public static bool operator !=(GamepadButton a, GamepadButton b)
    {
        return !(a == b);
    }

    public override readonly bool Equals(object obj)
    {
        if (obj is GamepadButton button)
        {
            return this == button;
        }

        return base.Equals(obj);
    }

    public override readonly int GetHashCode()
    {
        return GamepadName.GetHashCode() ^ Button.GetHashCode();
    }
}

public class GamepadHook
{
    public event EventHandlerT<GamepadButton> ButtonPressed;

    public IReadOnlyList<string> GamepadNames => [];

    public GamepadHook()
    {
    }

    public object GetMouse()
    {
        return null;
    }

    public void Poll()
    {
    }

    protected void OnButtonPressed(GamepadButton button)
    {
        ButtonPressed?.Invoke(this, button);
    }
}
