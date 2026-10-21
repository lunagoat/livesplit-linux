using System;
using System.Drawing;

namespace LiveSplit.UI;

public interface IFormHost
{
    bool InvokeRequired { get; }

    bool IsActive { get; }

    void Invoke(Action action);

    void BeginInvoke(Action action);

    void Invalidate(Rectangle rectangle);

    void AddControl(object control);

    void RemoveControl(object control);

    void SetControlBounds(object control, Rectangle bounds);

    bool GetControlVisible(object control);

    void SetControlVisible(object control, bool visible);
}
