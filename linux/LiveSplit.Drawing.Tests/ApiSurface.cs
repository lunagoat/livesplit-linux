using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    /// <summary>
    /// Compile-time (and smoke-run) check that every System.Drawing member/overload LiveSplit's drawing code
    /// calls exists in the shim with GDI+-compatible signatures. The statements below are taken from the
    /// component/core/view sources (found by a Roslyn pass over all 478 non-WinForms-only files).
    /// </summary>
    public class ApiSurface
    {
        // Mirrors BlankSpace/Title/Splits... background gradient code, Timer.cs, GraphComponent.cs, ComponentRenderer.cs, ...
        private static void ComponentStyleDrawing(Graphics g, Region clipRegion, Image icon, Image shadow, Font font)
        {
            // BlankSpace.DrawBackground & friends
            var gradientBrush = new LinearGradientBrush(
                new PointF(0, 0),
                true ? new PointF(100, 0) : new PointF(0, 30),
                Color.FromArgb(200, 10, 20, 30),
                Color.Red);
            g.FillRectangle(gradientBrush, 0, 0, 100f, 30f);
            g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Black)), 0, 0, 10, 10);
            Color first = gradientBrush.LinearColors[0];
            Assert.Equal(200, first.A);

            // HotkeyIndicator / Separators
            Region oldClip = g.Clip;
            Matrix oldMatrix = g.Transform;
            SmoothingMode oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.Default;
            g.Clip = new Region();
            float scale = g.Transform.Elements[0];
            g.TranslateTransform(0, (2f - scale) / 2f);
            g.Clip = oldClip;
            g.Transform = oldMatrix;
            g.SmoothingMode = oldMode;

            // ComponentRenderer
            g.IntersectClip(new RectangleF(0, 1, 100, 20));
            bool visible = clipRegion.IsVisible(new RectangleF(g.Transform.OffsetX, g.Transform.OffsetY, 50, 10));
            Assert.True(visible);
            g.TranslateTransform(0.0f, 4f);
            g.ScaleTransform(1, -1);

            // Timer.cs
            float sizeMultiplier = font.Size / font.FontFamily.GetEmHeight(font.Style);
            float ascent = sizeMultiplier * font.FontFamily.GetCellAscent(font.Style);
            float descent = sizeMultiplier * font.FontFamily.GetCellDescent(font.Style);
            Assert.True(ascent > 0 && descent > 0);
            var decimals = new Font(font.FontFamily.Name, font.Size / 50f * 18f, font.Style, GraphicsUnit.Pixel);
            var fromFamily = new Font(font.FontFamily, font.Size, font.Style, font.Unit);
            Assert.NotNull(decimals);
            Assert.NotNull(fromFamily);

            // Title / Detailed Timer / SplitComponent icons
            g.DrawImage(icon, 7f, 2f, 20f, 20f);
            g.DrawImage(shadow, 7 + 0.5f, 2 + 0.5f, 20, 20);
            ImageAnimator.Animate(icon, (s, o) => { });
            ImageAnimator.UpdateFrames(icon);
            int frameCount = icon == null ? 0 : icon.GetFrameCount(new FrameDimension(icon.FrameDimensionsList[0]));
            Assert.Equal(1, frameCount);

            // GraphComponent
            var pen = new Pen(Color.Gray, 2.0f);
            pen.Width = 1.75f;
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            var shadowPen = (Pen)pen.Clone();
            shadowPen.Color = Color.Black;
            g.DrawLine(shadowPen, 1f + 1, 1f, 20f, 20f);
            g.DrawLine(pen, 0, 5f, 100, 5f);
            var brush = new SolidBrush(Color.Green);
            brush.Color = Color.Red;
            g.FillEllipse(brush, 3f, 4f, 5f, 5f);
            g.FillPolygon(brush, new PointF[] { new PointF(0, 0), new PointF(5, 0), new PointF(5, 5) });
            g.FillPolygon(brush, new List<PointF> { new PointF(0, 0), new PointF(5, 0), new PointF(5, 5) }.ToArray());
            g.DrawRectangle(new Pen(Color.White), 0, 0, 5 - 1, 5 - 1);
        }

        // SimpleLabel.cs
        private static void SimpleLabelStyleDrawing(Graphics g, Font font, Brush brush)
        {
            var format = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            if (g.TextRenderingHint == TextRenderingHint.AntiAlias)
            {
                using var gp = new GraphicsPath();
                using var outline = new Pen(Color.Black, 2.3f) { LineJoin = LineJoin.Round };
                gp.AddString("text", font.FontFamily, (int)font.Style, g.DpiY * font.Size / 72, new RectangleF(1, 1, 50, 20), format);
                g.FillPath(brush, gp);
                gp.Reset();
                g.DrawPath(outline, gp);
            }

            g.DrawString("text", font, brush, new RectangleF(0, 0, 40, 20), format);
            float w = g.MeasureString("text", font, 9999, format).Width;
            float h = g.MeasureString("A", font).Height * 1.7f;
            Assert.True(w > 0 && h > 0);
            Assert.True(font.Unit == GraphicsUnit.Pixel || font.Unit == GraphicsUnit.Point);
        }

        // TimerForm: MakeScreenShot / DrawBackground / CreateBakedBackground
        private static void TimerFormStyleDrawing(Bitmap image, Region updateRegion)
        {
            var bitmap = new Bitmap(50, 50, image.PixelFormat);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.FillRectangle(Brushes.Transparent, 0, 0, 50, 50);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.FillRectangle(new SolidBrush(Color.Black), 0, 0, 50, 50);

                var drawRegion = new Region(new Rectangle(0, 0, 50, 50));
                if (!drawRegion.GetBounds(graphics).Equals(updateRegion.GetBounds(graphics)))
                {
                    updateRegion.Union(drawRegion);
                }

                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.CompositingQuality = CompositingQuality.GammaCorrected;
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.ResetTransform();
                graphics.TranslateTransform(-0.5f, -0.5f);
                graphics.ScaleTransform(2f, 2f);

                foreach (RectangleF rectangle in updateRegion.GetRegionScans(graphics.Transform))
                {
                    var rect = Rectangle.Round(rectangle);
                    graphics.DrawImage(image, rect, rect, GraphicsUnit.Pixel);
                }

                var matrix = new ColorMatrix { Matrix33 = 0.5f };
                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(
                    image,
                    new Rectangle(0, 0, 50, 50),
                    (image.Width - 20f) / 2,
                    (image.Height - 20f) / 2,
                    20f,
                    20f,
                    GraphicsUnit.Pixel,
                    attributes);
                graphics.DrawImage(image, new Rectangle(0, 0, 50, 50), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }

            // Icon shadow / blur: GetPixel/SetPixel/LockBits/Marshal
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, 50, 50), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[50 * 50];
                for (int y = 0; y < 50; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + (y * data.Stride), pixels, y * 50, 50);
                }

                for (int y = 0; y < 50; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixels, y * 50, data.Scan0 + (y * data.Stride), 50);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            var scaledCopy = new Bitmap(bitmap, 24, 24);
            Color px = scaledCopy.GetPixel(1, 1);
            scaledCopy.SetPixel(1, 1, Color.FromArgb((int)(px.A * 0.5), px.R, px.G, px.B));
            var plainCopy = new Bitmap(bitmap);
            using var ms = new MemoryStream();
            plainCopy.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            Image reloaded = Image.FromStream(ms);
            Image clone = (Image)reloaded.Clone();
            Assert.Equal(reloaded.Size, clone.Size);
            reloaded.Dispose();
        }

        // StandardLayoutFactory / settings / LayoutSettings / SpeedRunsLiveForm / FontList / FontDialog
        private static void SettingsStyleFonts()
        {
            var f = new Font("Calibri", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
            Font clone = f.Clone() as Font;
            Assert.Equal(f.Name, clone.Name);
            string label = $"{f.FontFamily.Name} {f.Style}";
            Assert.Contains("Calibri", label);
            int hash = f.Name.GetHashCode() ^ f.FontFamily.GetHashCode() ^ f.Size.GetHashCode() ^ f.Style.GetHashCode();
            Assert.NotEqual(0, hash | 1);
            foreach (FontFamily family in FontFamily.Families)
            {
                if (family.IsStyleAvailable(FontStyle.Regular))
                {
                    _ = new Font(family, 12, FontStyle.Regular);
                    break;
                }
            }

            FontFamily generic = FontFamily.GenericSansSerif;
            Assert.NotNull(generic);
            using var sample = new Font(generic, 12, FontStyle.Underline | FontStyle.Strikeout, GraphicsUnit.Pixel);
            Assert.True(sample.Underline && sample.Strikeout && !sample.Bold && !sample.Italic);
            var big = new Font(f, FontStyle.Italic);
            Assert.True(big.Italic);
            var legacy = new Font("Microsoft Sans Serif", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            Assert.Equal(9F, legacy.Size);
            f.Dispose();
        }

        [Fact]
        public void EverythingLiveSplitCallsExistsAndRuns()
        {
            using var target = new Bitmap(120, 60);
            using var icon = new Bitmap(24, 24);
            using var shadow = new Bitmap(30, 30);
            using var font = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel);
            using var clip = new Region(new RectangleF(0, 0, 500, 500));
            using (Graphics g = Graphics.FromImage(target))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                ComponentStyleDrawing(g, clip, icon, shadow, font);
                SimpleLabelStyleDrawing(g, font, new SolidBrush(Color.White));
            }

            TimerFormStyleDrawing(target, clip);
            SettingsStyleFonts();
        }
    }
}
