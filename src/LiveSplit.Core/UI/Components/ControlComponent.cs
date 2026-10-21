using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace LiveSplit.UI.Components;

public abstract class ControlComponent : IDeactivatableComponent
{
    public abstract string ComponentName { get; }

    protected IFormHost Form { get; set; }
    protected object Control { get; set; }

    public bool Activated
    {
        get => Form?.GetControlVisible(Control) ?? activated;
        set
        {
            activated = value;
            Form?.SetControlVisible(Control, value);
        }
    }
    protected bool activated;

    protected bool AddedToForm;
    protected bool HasInvalidated = false;

    public abstract float HorizontalWidth { get; }
    public abstract float MinimumHeight { get; }
    public abstract float VerticalHeight { get; }
    public abstract float MinimumWidth { get; }

    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;

    public bool ErrorWithControl { get; set; }
    private readonly Action<Exception> ErrorCallback;

    public IDictionary<string, Action> ContextMenuControls { get; protected set; }

    protected ControlComponent(LiveSplitState state, object control, Action<Exception> errorCallback = null)
    {
        ErrorCallback = errorCallback;
        Form = state.Form;
        Control = control;
        activated = Form?.GetControlVisible(control) ?? true;
    }

    public void InvokeIfNeeded(Action x)
    {
        if (Form is { InvokeRequired: true })
        {
            Form.Invoke(x);
        }
        else
        {
            x();
        }
    }

    public void Reposition(float width, float height, Graphics g)
    {
        PointF[] points =
        [
            new(0, 0),
            new(width, height)
        ];
        g.Transform.TransformPoints(points);

        InvokeIfNeeded(() =>
        {
            lock (Control)
            {
                Form?.SetControlBounds(Control, new Rectangle(
                    (int)(points[0].X + 0.5f) + 1,
                    (int)(points[0].Y + 0.5f) + 1,
                    (int)(points[1].X - points[0].X + 0.5f) - 2,
                    (int)(points[1].Y - points[0].Y + 0.5f) - 2));
            }
        });
    }

    private void AddControlToForm()
    {
        if (!AddedToForm)
        {
            AddedToForm = true;
            try
            {
                Form.AddControl(Control);
            }
            catch (Exception ex)
            {
                ErrorCallback?.Invoke(ex);
                ErrorWithControl = true;
            }
        }
    }

    public virtual void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
    {
        AddControlToForm();
        Reposition(HorizontalWidth, height, g);
    }

    public virtual void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
    {
        AddControlToForm();
        Reposition(width, VerticalHeight, g);
    }

    public abstract object GetSettingsControl(LayoutMode mode);

    public abstract System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document);

    public abstract void SetSettings(System.Xml.XmlNode settings);

    public virtual void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        if (!HasInvalidated && invalidator != null)
        {
            invalidator.Invalidate(0, 0, width, height);
            HasInvalidated = true;
        }
    }

    public virtual void Dispose()
    {
        Form?.RemoveControl(Control);
        (Control as IDisposable)?.Dispose();

        GC.SuppressFinalize(this);
    }
}
