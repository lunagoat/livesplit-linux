using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiveSplit.UI;

/// <summary>
/// Tiny base class for toolkit-free component settings objects. A UI layer (Avalonia) binds to the
/// settings object directly, so every settable property must raise <see cref="PropertyChanged"/>.
/// </summary>
public abstract class SettingsBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises <see cref="PropertyChanged"/>
    /// for the calling property when the value actually changed.
    /// </summary>
    /// <returns><c>true</c> when the value changed (use it to raise change events for dependent computed properties).</returns>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
