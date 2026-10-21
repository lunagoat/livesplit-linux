using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class CounterView : SettingsPage
{
    public CounterView(object model) : base(model)
    {
        Control Hotkey(string label, string path)
        {
            var capture = new HotkeyCapture();
            Bind(capture, HotkeyCapture.HotkeyProperty, path);
            return Row(label, capture);
        }
        Add(Gradient(), Section("Counter Colors", Check("Override Layout Settings", "OverrideTextColor"), Color("Text Color:", "CounterTextColor", "IsCounterColorEnabled"), Color("Value Color:", "CounterValueColor", "IsCounterColorEnabled")),
            Section("Counter", Text("Counter Text:", "CounterText", validation: true), Number("Initial Value:", "InitialValue", int.MinValue, int.MaxValue), Number("Increment Value:", "Increment", 0, int.MaxValue),
                Section("Hotkeys", Hotkey("Increment Counter:", "IncrementKey"), Hotkey("Decrement Counter:", "DecrementKey"), Hotkey("Reset Counter:", "ResetKey"), Check("Global Counter Hotkeys", "GlobalHotkeysEnabled"))));
    }
}
