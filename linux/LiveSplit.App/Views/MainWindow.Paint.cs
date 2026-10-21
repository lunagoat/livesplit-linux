using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Image = System.Drawing.Image;
using Rectangle = System.Drawing.Rectangle;
using Region = System.Drawing.Region;

namespace LiveSplit.View;

/// <summary>Painting: PaintForm, the background and the screenshot of the original TimerForm.</summary>
public partial class MainWindow
{
    private Image previousBackground;
    private float previousOpacity;
    private float previousBlur;
    private Image blurredBackground;
    private Image bakedBackground;

    protected Region UpdateRegion { get; set; }

    /// <summary>Called by the LayoutView for every frame, on the UI thread.</summary>
    private void PaintView(Graphics g)
    {
        if (Layout == null || ComponentRenderer == null)
        {
            return;
        }

        try
        {
            UpdateRegion = new Region(new Rectangle(0, 0, formWidth, formHeight));
            PaintForm(g, UpdateRegion);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Invalidate();
        }
    }

    private void PaintForm(Graphics g, Region clip)
    {
        DrawBackground(g);

        ApplyPaintWindowSettings();

        g.TextRenderingHint = Layout.Settings.AntiAliasing ? TextRenderingHint.AntiAlias : TextRenderingHint.ClearTypeGridFit;

        g.CompositingQuality = CompositingQuality.GammaCorrected;
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        FixSize();

        float scaleFactor = Layout.Mode == LayoutMode.Vertical
            ? formHeight / ComponentRenderer.OverallSize
            : formWidth / ComponentRenderer.OverallSize;

        g.ResetTransform();
        g.TranslateTransform(-0.5f, -0.5f);
        g.ScaleTransform(scaleFactor, scaleFactor);
        float transformedWidth = formWidth;
        float transformedHeight = formHeight;

        if (Layout.Mode == LayoutMode.Vertical)
        {
            transformedWidth /= scaleFactor;
        }
        else
        {
            transformedHeight /= scaleFactor;
        }

        ComponentRenderer.Render(g, CurrentState, transformedWidth, transformedHeight, Layout.Mode, UpdateRegion);

        FixSize();
        KeepLayoutSize();
        MaintainMinimumSize();
    }

    private void DrawBackground(Graphics g)
    {
        if (Layout.Settings.BackgroundType == BackgroundType.Image)
        {
            if (Layout.Settings.BackgroundImage != null)
            {
                if (Layout.Settings.BackgroundImage != previousBackground
                    || Layout.Settings.ImageOpacity != previousOpacity
                    || Layout.Settings.ImageBlur != previousBlur)
                {
                    CreateBakedBackground();
                }

                if (bakedBackground != null)
                {
                    var rect = new Rectangle(0, 0, formWidth, formHeight);
                    g.DrawImage(bakedBackground, rect, rect, GraphicsUnit.Pixel);
                }
            }
        }
        else if (Layout.Settings.BackgroundColor != Color.Transparent
            || (Layout.Settings.BackgroundType != BackgroundType.SolidColor
            && Layout.Settings.BackgroundColor2 != Color.Transparent))
        {
            using var gradientBrush = new LinearGradientBrush(
                new PointF(0, 0),
                Layout.Settings.BackgroundType == BackgroundType.HorizontalGradient ? new PointF(formWidth, 0) : new PointF(0, formHeight),
                Layout.Settings.BackgroundColor,
                Layout.Settings.BackgroundType == BackgroundType.SolidColor ? Layout.Settings.BackgroundColor : Layout.Settings.BackgroundColor2);
            g.FillRectangle(gradientBrush, 0, 0, formWidth, formHeight);
        }
    }

    private void CreateBakedBackground()
    {
        if (Layout?.Settings == null)
        {
            return;
        }

        Image image = Layout.Settings.BackgroundImage;
        float opacity = Layout.Settings.ImageOpacity;
        float blur = Layout.Settings.ImageBlur;

        if (image != null)
        {
            if (blur > 0)
            {
                if (blur != previousBlur || image != previousBackground)
                {
                    blurredBackground?.Dispose();

                    blurredBackground = ImageBlur.Generate(image, blur * 10);
                }

                image = blurredBackground;
            }

            float croppedWidth = image.Width;
            float croppedHeight = image.Height;

            if (image.Width / (float)image.Height > formWidth / (float)formHeight)
            {
                croppedWidth = image.Height * (formWidth / (float)formHeight);
            }
            else
            {
                croppedHeight = image.Width * (formHeight / (float)formWidth);
            }

            var bitmap = new Bitmap(formWidth, formHeight, image.PixelFormat);

            using (var g = Graphics.FromImage(bitmap))
            {
                var matrix = new ColorMatrix
                {
                    Matrix33 = opacity
                };
                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(
                    image,
                    new Rectangle(0, 0, formWidth, formHeight),
                    (image.Width - croppedWidth) / 2,
                    (image.Height - croppedHeight) / 2,
                    croppedWidth,
                    croppedHeight,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            bakedBackground?.Dispose();

            bakedBackground = bitmap;
            previousBackground = Layout.Settings.BackgroundImage;
            previousOpacity = opacity;
            previousBlur = blur;
        }
    }

    /// <summary>Renders the window to an image on a black background (UI thread).</summary>
    public Image MakeScreenShot()
    {
        var image = new Bitmap(formWidth, formHeight);
        using var graphics = Graphics.FromImage(image);

        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.FillRectangle(Brushes.Transparent, 0, 0, formWidth, formHeight);
        graphics.CompositingMode = CompositingMode.SourceOver;

        // Start with a black background because we don't support the window being transparent, so this is closer to how LiveSplit actually looks
        using (var black = new SolidBrush(Color.Black))
        {
            graphics.FillRectangle(black, 0, 0, formWidth, formHeight);
        }

        var drawRegion = new Region(new Rectangle(0, 0, formWidth, formHeight));
        UpdateRegion = drawRegion;
        PaintForm(graphics, drawRegion);

        return image;
    }
}
