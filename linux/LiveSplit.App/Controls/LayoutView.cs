using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Drawing;
using Point = Avalonia.Point;

namespace LiveSplit.Controls;

/// <summary>
/// Draws the LiveSplit layout. The layout is painted on the UI thread (components read the live timer state) through the
/// System.Drawing shim into an offscreen Skia surface at the device resolution; the finished frame is handed to
/// Avalonia's render thread as an image. <see cref="Painter"/> works in device independent pixels, like the WinForms
/// original worked in pixels at 100% scaling.
/// </summary>
public sealed class LayoutView : Control
{
    private SKSurface surface;
    private int surfaceWidth;
    private int surfaceHeight;

    /// <summary>Paints one frame. Receives a Graphics over a cleared (transparent) canvas.</summary>
    public Action<Graphics> Painter { get; set; }

    public double RenderScalingFactor => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width < 1 || bounds.Height < 1 || Painter == null)
        {
            return;
        }

        double scale = RenderScalingFactor;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
        EnsureSurface(pixelWidth, pixelHeight);

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.ResetMatrix();
        canvas.Scale((float)(pixelWidth / bounds.Width), (float)(pixelHeight / bounds.Height));
        using (var graphics = Graphics.FromSKCanvas(canvas))
        {
            try
            {
                Painter(graphics);
            }
            catch (Exception ex)
            {
                LiveSplit.Options.Log.Error(ex);
            }
        }

        SKImage frame = surface.Snapshot();
        context.Custom(new FrameOperation(frame, bounds));
    }

    private void EnsureSurface(int width, int height)
    {
        if (surface != null && surfaceWidth == width && surfaceHeight == height)
        {
            return;
        }

        surface?.Dispose();
        surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        surfaceWidth = width;
        surfaceHeight = height;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        surface?.Dispose();
        surface = null;
    }

    /// <summary>Draws a finished frame on the render thread.</summary>
    private sealed class FrameOperation : ICustomDrawOperation
    {
        private readonly SKImage frame;
        private readonly Rect bounds;

        public FrameOperation(SKImage frame, Rect bounds)
        {
            this.frame = frame;
            this.bounds = bounds;
        }

        public Rect Bounds => bounds;

        public void Dispose()
        {
            frame.Dispose();
        }

        public bool Equals(ICustomDrawOperation other)
        {
            return false;
        }

        public bool HitTest(Point p)
        {
            return bounds.Contains(p);
        }

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature == null)
            {
                return;
            }

            using ISkiaSharpApiLease lease = feature.Lease();
            SKCanvas canvas = lease.SkCanvas;
            using var paint = new SKPaint { BlendMode = SKBlendMode.SrcOver };
            canvas.DrawImage(frame, new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height), new SKSamplingOptions(SKFilterMode.Nearest), paint);
        }
    }
}
