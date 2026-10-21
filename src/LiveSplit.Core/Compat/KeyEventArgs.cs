using System;

namespace System.Windows.Forms;

public delegate void KeyEventHandler(object sender, KeyEventArgs e);

public class KeyEventArgs : EventArgs
{
    private readonly Keys keyData;

    public KeyEventArgs(Keys keyData)
    {
        this.keyData = keyData;
    }

    public virtual bool Alt => (keyData & Keys.Alt) == Keys.Alt;

    public bool Control => (keyData & Keys.Control) == Keys.Control;

    public bool Handled { get; set; }

    public Keys KeyCode
    {
        get
        {
            Keys keyGenerated = keyData & Keys.KeyCode;
            return Enum.IsDefined(keyGenerated) ? keyGenerated : Keys.None;
        }
    }

    public int KeyValue => (int)(keyData & Keys.KeyCode);

    public Keys KeyData => keyData;

    public Keys Modifiers => keyData & Keys.Modifiers;

    public virtual bool Shift => (keyData & Keys.Shift) == Keys.Shift;

    public bool SuppressKeyPress { get; set; }
}
