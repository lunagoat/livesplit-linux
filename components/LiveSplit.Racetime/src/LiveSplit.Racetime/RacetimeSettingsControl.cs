using System;
using System.Windows.Forms;

namespace LiveSplit.Racetime;

public partial class RacetimeSettingsControl : UserControl
{
    public RacetimeSettings Settings
    {
        get;
        set
        {
            field = value;
            RacetimeSettingsControl_VisibleChanged(this, null);
        }
    }

    public RacetimeSettingsControl()
    {
        InitializeComponent();
    }

    private void RacetimeSettingsControl_VisibleChanged(object sender, EventArgs e)
    {
    }
}
