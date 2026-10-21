using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public static class LinuxKeyMap
{
    public const int KeyEsc = 1;
    public const int KeyLeftCtrl = 29;
    public const int KeyLeftShift = 42;
    public const int KeyRightShift = 54;
    public const int KeyLeftAlt = 56;
    public const int KeyRightCtrl = 97;
    public const int KeyRightAlt = 100;

    private static readonly Dictionary<int, Keys> EvdevToKeys = BuildEvdevMap();
    private static readonly Dictionary<Keys, int[]> KeysToEvdev = BuildReverseMap();

    public static bool TryGetKeys(int evdevCode, out Keys key)
    {
        return EvdevToKeys.TryGetValue(evdevCode, out key);
    }

    public static IReadOnlyList<int> GetEvdevCodes(Keys key)
    {
        Keys code = key & Keys.KeyCode;
        return KeysToEvdev.TryGetValue(code, out int[] codes) ? codes : [];
    }

    public static bool IsModifier(Keys key)
    {
        return (key & Keys.KeyCode) is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
            or Keys.Menu or Keys.LMenu or Keys.RMenu;
    }

    private static Dictionary<int, Keys> BuildEvdevMap()
    {
        var map = new Dictionary<int, Keys>
        {
            [1] = Keys.Escape,
            [2] = Keys.D1,
            [3] = Keys.D2,
            [4] = Keys.D3,
            [5] = Keys.D4,
            [6] = Keys.D5,
            [7] = Keys.D6,
            [8] = Keys.D7,
            [9] = Keys.D8,
            [10] = Keys.D9,
            [11] = Keys.D0,
            [12] = Keys.OemMinus,
            [13] = Keys.Oemplus,
            [14] = Keys.Back,
            [15] = Keys.Tab,
            [16] = Keys.Q,
            [17] = Keys.W,
            [18] = Keys.E,
            [19] = Keys.R,
            [20] = Keys.T,
            [21] = Keys.Y,
            [22] = Keys.U,
            [23] = Keys.I,
            [24] = Keys.O,
            [25] = Keys.P,
            [26] = Keys.OemOpenBrackets,
            [27] = Keys.OemCloseBrackets,
            [28] = Keys.Return,
            [29] = Keys.LControlKey,
            [30] = Keys.A,
            [31] = Keys.S,
            [32] = Keys.D,
            [33] = Keys.F,
            [34] = Keys.G,
            [35] = Keys.H,
            [36] = Keys.J,
            [37] = Keys.K,
            [38] = Keys.L,
            [39] = Keys.OemSemicolon,
            [40] = Keys.OemQuotes,
            [41] = Keys.Oemtilde,
            [42] = Keys.LShiftKey,
            [43] = Keys.OemPipe,
            [44] = Keys.Z,
            [45] = Keys.X,
            [46] = Keys.C,
            [47] = Keys.V,
            [48] = Keys.B,
            [49] = Keys.N,
            [50] = Keys.M,
            [51] = Keys.Oemcomma,
            [52] = Keys.OemPeriod,
            [53] = Keys.OemQuestion,
            [54] = Keys.RShiftKey,
            [55] = Keys.Multiply,
            [56] = Keys.LMenu,
            [57] = Keys.Space,
            [58] = Keys.Capital,
            [59] = Keys.F1,
            [60] = Keys.F2,
            [61] = Keys.F3,
            [62] = Keys.F4,
            [63] = Keys.F5,
            [64] = Keys.F6,
            [65] = Keys.F7,
            [66] = Keys.F8,
            [67] = Keys.F9,
            [68] = Keys.F10,
            [69] = Keys.NumLock,
            [70] = Keys.Scroll,
            [71] = Keys.NumPad7,
            [72] = Keys.NumPad8,
            [73] = Keys.NumPad9,
            [74] = Keys.Subtract,
            [75] = Keys.NumPad4,
            [76] = Keys.NumPad5,
            [77] = Keys.NumPad6,
            [78] = Keys.Add,
            [79] = Keys.NumPad1,
            [80] = Keys.NumPad2,
            [81] = Keys.NumPad3,
            [82] = Keys.NumPad0,
            [83] = Keys.Decimal,
            [86] = Keys.OemBackslash,
            [87] = Keys.F11,
            [88] = Keys.F12,
            [96] = Keys.Return,
            [97] = Keys.RControlKey,
            [98] = Keys.Divide,
            [99] = Keys.Snapshot,
            [100] = Keys.RMenu,
            [102] = Keys.Home,
            [103] = Keys.Up,
            [104] = Keys.Prior,
            [105] = Keys.Left,
            [106] = Keys.Right,
            [107] = Keys.End,
            [108] = Keys.Down,
            [109] = Keys.Next,
            [110] = Keys.Insert,
            [111] = Keys.Delete,
            [113] = Keys.VolumeMute,
            [114] = Keys.VolumeDown,
            [115] = Keys.VolumeUp,
            [119] = Keys.Pause,
            [121] = Keys.Separator,
            [125] = Keys.LWin,
            [126] = Keys.RWin,
            [127] = Keys.Apps,
            [139] = Keys.Apps,
            [163] = Keys.MediaNextTrack,
            [164] = Keys.MediaPlayPause,
            [165] = Keys.MediaPreviousTrack,
            [166] = Keys.MediaStop,
        };

        for (int i = 0; i < 12; i++)
        {
            map[183 + i] = Keys.F13 + i;
        }

        return map;
    }

    private static Dictionary<Keys, int[]> BuildReverseMap()
    {
        var lists = new Dictionary<Keys, List<int>>();
        foreach (KeyValuePair<int, Keys> pair in EvdevToKeys)
        {
            if (!lists.TryGetValue(pair.Value, out List<int> list))
            {
                lists[pair.Value] = list = [];
            }

            list.Add(pair.Key);
        }

        var result = new Dictionary<Keys, int[]>();
        foreach (KeyValuePair<Keys, List<int>> pair in lists)
        {
            pair.Value.Sort();
            result[pair.Key] = [.. pair.Value];
        }

        result[Keys.ShiftKey] = [KeyLeftShift, KeyRightShift];
        result[Keys.ControlKey] = [KeyLeftCtrl, KeyRightCtrl];
        result[Keys.Menu] = [KeyLeftAlt, KeyRightAlt];
        return result;
    }
}
