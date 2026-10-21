using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public sealed class KeyEventTranslator
{
    private readonly HashSet<int> pressedCodes = [];
    private readonly object syncRoot = new();

    public Keys Modifiers { get; private set; }

    public bool TryProcess(int evdevCode, int value, out Keys keyWithModifiers)
    {
        keyWithModifiers = Keys.None;

        lock (syncRoot)
        {
            if (!LinuxKeyMap.TryGetKeys(evdevCode, out Keys key))
            {
                return false;
            }

            if (value == 0)
            {
                pressedCodes.Remove(evdevCode);
                UpdateModifiers();
                return false;
            }

            if (value != 1 || !pressedCodes.Add(evdevCode))
            {
                return false;
            }

            Keys modifiersBefore = Modifiers;
            UpdateModifiers();

            Keys reported = key;
            Keys alreadyHeld = Keys.None;
            switch (key)
            {
                case Keys.LControlKey or Keys.RControlKey:
                    reported = Keys.ControlKey;
                    alreadyHeld = Keys.Control;
                    break;
                case Keys.LMenu or Keys.RMenu:
                    reported = Keys.Menu;
                    alreadyHeld = Keys.Alt;
                    break;
                case Keys.LShiftKey or Keys.RShiftKey:
                    reported = Keys.ShiftKey;
                    alreadyHeld = Keys.Shift;
                    break;
            }

            if (alreadyHeld != Keys.None && (modifiersBefore & alreadyHeld) != 0)
            {
                return false;
            }

            keyWithModifiers = reported | modifiersBefore;
            return true;
        }
    }

    public bool IsKeyDown(Keys key)
    {
        lock (syncRoot)
        {
            foreach (int code in LinuxKeyMap.GetEvdevCodes(key))
            {
                if (pressedCodes.Contains(code))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Reset()
    {
        lock (syncRoot)
        {
            pressedCodes.Clear();
            Modifiers = Keys.None;
        }
    }

    private void UpdateModifiers()
    {
        Keys modifiers = Keys.None;
        if (pressedCodes.Contains(LinuxKeyMap.KeyLeftShift) || pressedCodes.Contains(LinuxKeyMap.KeyRightShift))
        {
            modifiers |= Keys.Shift;
        }

        if (pressedCodes.Contains(LinuxKeyMap.KeyLeftCtrl) || pressedCodes.Contains(LinuxKeyMap.KeyRightCtrl))
        {
            modifiers |= Keys.Control;
        }

        if (pressedCodes.Contains(LinuxKeyMap.KeyLeftAlt) || pressedCodes.Contains(LinuxKeyMap.KeyRightAlt))
        {
            modifiers |= Keys.Alt;
        }

        Modifiers = modifiers;
    }
}
