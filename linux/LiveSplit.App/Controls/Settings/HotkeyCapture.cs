using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LiveSplit.Model.Input;
using LiveSplit.Services;
using System;

namespace LiveSplit.Controls.Settings;

public sealed class HotkeyCapture : Button
{
    public static readonly StyledProperty<KeyOrButton> HotkeyProperty = AvaloniaProperty.Register<HotkeyCapture, KeyOrButton>(nameof(Hotkey), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public KeyOrButton Hotkey { get => GetValue(HotkeyProperty); set => SetValue(HotkeyProperty, value); }
    public HotkeyCapture()
    {
        Content = "None";
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        GotFocus += (_, _) => Content = SettingsPage.T("Set Hotkey...");
        LostFocus += (_, _) => Refresh();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if (change.Property == HotkeyProperty) Refresh(); }
    private void Refresh() => Content = Hotkey?.ToString() ?? SettingsPage.T("None");
    private static bool Modifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;
    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        if (!Modifier(e.Key)) Capture(e);
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        if (Modifier(e.Key)) Capture(e);
    }
    private void Capture(KeyEventArgs e)
    {
        SetCurrentValue(HotkeyProperty, e.Key == Key.Escape ? null : new KeyOrButton(KeyMap.ToKeys(e.Key, e.KeyModifiers)));
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        Refresh();
    }
}
