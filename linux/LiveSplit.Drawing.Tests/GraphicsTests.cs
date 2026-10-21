using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SkiaSharp;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    public class GraphicsTests
    {
        [Fact]
        public void FillRectangle_PaintsPixels()
        {
            using Bitmap bmp = TestUtil.White(20, 20);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.Red))
            {
                g.FillRectangle(brush, new RectangleF(2, 2, 10, 10));
            }

            Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(5, 5));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(15, 15));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(1, 1));
        }

        [Fact]
        public void FillRectangle_FloatOverload_AndAntialiasedEdge()
        {
            using Bitmap bmp = TestUtil.White(10, 10);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(Brushes.Black, 0, 0, 4.5f, 10);
            }

            Assert.Equal(Color.FromArgb(255, 0, 0, 0), bmp.GetPixel(2, 5));
            Color edge = bmp.GetPixel(4, 5);
            Assert.InRange(edge.R, 100, 155); // half coverage
        }

        [Fact]
        public void Clip_ExcludesOutsideRegion()
        {
            using Bitmap bmp = TestUtil.White(30, 30);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SetClip(new RectangleF(10, 10, 10, 10));
                g.FillRectangle(Brushes.Blue, 0, 0, 30, 30);
            }

            Assert.Equal(Color.FromArgb(255, 0, 0, 255), bmp.GetPixel(15, 15));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(5, 5));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(25, 25));
        }

        [Fact]
        public void IntersectClip_AccumulatesAndClipPropertyRoundTrips()
        {
            using Bitmap bmp = TestUtil.White(40, 40);
            using Graphics g = Graphics.FromImage(bmp);
            g.IntersectClip(new RectangleF(0, 0, 20, 20));
            g.IntersectClip(new RectangleF(10, 10, 20, 20));
            Region clip = g.Clip;
            Assert.Equal(new RectangleF(10, 10, 10, 10), clip.GetBounds(g));

            g.Clip = new Region();
            Assert.True(g.Clip.IsInfinite(g));
            g.Clip = clip;
            g.FillRectangle(Brushes.Green, 0, 0, 40, 40);
            Assert.Equal(Color.FromArgb(255, 0, 128, 0), bmp.GetPixel(12, 12));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(5, 5));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(25, 25));
        }

        [Fact]
        public void Clip_IsStoredInDeviceSpace_LikeGdiPlus()
        {
            using Bitmap bmp = TestUtil.White(40, 40);
            using Graphics g = Graphics.FromImage(bmp);
            g.TranslateTransform(10, 10);
            g.IntersectClip(new RectangleF(0, 0, 10, 10)); // device (10,10)-(20,20)
            g.TranslateTransform(5, 5);                     // clip does not move
            Assert.Equal(new RectangleF(-5, -5, 10, 10), g.Clip.GetBounds(g));
            g.FillRectangle(Brushes.Black, -100, -100, 500, 500);
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), bmp.GetPixel(12, 12));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(22, 22));
        }

        [Fact]
        public void Transform_TranslateScale_AffectDrawing()
        {
            using Bitmap bmp = TestUtil.White(40, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TranslateTransform(10, 10);
                g.ScaleTransform(2, 2);
                g.FillRectangle(Brushes.Red, 0, 0, 5, 5); // device (10,10)-(20,20)
            }

            Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(15, 15));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(21, 21));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(9, 9));
        }

        [Fact]
        public void Transform_Property_HasCopySemantics()
        {
            using Bitmap bmp = TestUtil.White(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            g.TranslateTransform(3, 4);
            Matrix saved = g.Transform;
            g.ScaleTransform(2, 2);
            Assert.Equal(3f, saved.OffsetX);
            Assert.Equal(1f, saved.Elements[0]);
            Assert.Equal(2f, g.Transform.Elements[0]);
            g.Transform = saved;
            Assert.Equal(1f, g.Transform.Elements[0]);
            saved.Translate(100, 100);
            Assert.Equal(3f, g.Transform.OffsetX); // mutating the copy does not touch the graphics
        }

        [Fact]
        public void SaveRestore_RestoresTransformClipAndModes()
        {
            using Bitmap bmp = TestUtil.White(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            GraphicsState state = g.Save();
            g.TranslateTransform(5, 5);
            g.SetClip(new RectangleF(1, 1, 2, 2));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Restore(state);
            Assert.True(g.Transform.IsIdentity);
            Assert.True(g.Clip.IsInfinite(g));
            Assert.Equal(SmoothingMode.Default, g.SmoothingMode);
        }

        [Fact]
        public void LinearGradient_VerticalAndHorizontal()
        {
            using Bitmap bmp = new Bitmap(100, 100);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using var v = new LinearGradientBrush(new PointF(0, 0), new PointF(0, 100), Color.Black, Color.White);
                g.FillRectangle(v, 0, 0, 50, 100);
                using var h = new LinearGradientBrush(new PointF(50, 0), new PointF(100, 0), Color.Red, Color.Blue);
                g.FillRectangle(h, 50, 0, 50, 100);
            }

            TestUtil.SavePng(bmp, "gradient.png");
            Assert.InRange(bmp.GetPixel(10, 1).R, 0, 8);
            Assert.InRange(bmp.GetPixel(10, 98).R, 247, 255);
            Assert.InRange(bmp.GetPixel(10, 50).R, 118, 138);
            Color left = bmp.GetPixel(51, 50), right = bmp.GetPixel(98, 50);
            Assert.True(left.R > 230 && left.B < 25);
            Assert.True(right.B > 230 && right.R < 25);
        }

        [Fact]
        public void LinearGradient_RectAndAngleConstructors()
        {
            using var b = new LinearGradientBrush(new Rectangle(0, 0, 10, 10), Color.Red, Color.Blue, LinearGradientMode.Horizontal);
            Assert.Equal(Color.Red, b.LinearColors[0]);
            Assert.Equal(Color.Blue, b.LinearColors[1]);
            using Bitmap bmp = new Bitmap(10, 10);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.FillRectangle(b, 0, 0, 10, 10);
            }

            Assert.True(bmp.GetPixel(0, 5).R > bmp.GetPixel(9, 5).R);
            using var a = new LinearGradientBrush(new RectangleF(0, 0, 10, 10), Color.Black, Color.White, 90f);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.FillRectangle(a, 0, 0, 10, 10);
            }

            Assert.True(bmp.GetPixel(5, 0).R < bmp.GetPixel(5, 9).R);
        }

        [Fact]
        public void FillEllipsePolygonPath_ProduceExpectedShapes()
        {
            using Bitmap bmp = TestUtil.White(60, 60);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(Brushes.Red, 0, 0, 20, 20);
                g.FillPolygon(Brushes.Green, new[] { new PointF(30, 0), new PointF(50, 0), new PointF(30, 20) });
                using var path = new GraphicsPath();
                path.AddRectangle(new RectangleF(0, 30, 20, 20));
                g.FillPath(Brushes.Blue, path);
                using var pen = new Pen(Color.Black, 2f);
                g.DrawLine(pen, 30, 40, 55, 40);
                g.DrawRectangle(pen, 30, 45, 10, 10);
            }

            Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(10, 10));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(0, 0)); // outside the circle
            Assert.Equal(Color.FromArgb(255, 0, 128, 0), bmp.GetPixel(35, 3));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(48, 15));
            Assert.Equal(Color.FromArgb(255, 0, 0, 255), bmp.GetPixel(10, 40));
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), bmp.GetPixel(40, 40));
        }

        [Fact]
        public void CompositingMode_SourceCopy_ReplacesPixels()
        {
            using Bitmap bmp = TestUtil.White(10, 10);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.FillRectangle(Brushes.Transparent, 0, 0, 10, 10);
            }

            Assert.Equal(0, bmp.GetPixel(5, 5).A);
        }

        [Fact]
        public void Graphics_FromSKCanvas_DrawsToSurface()
        {
            using SKSurface surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.White);
            using (Graphics g = Graphics.FromSKCanvas(surface.Canvas))
            {
                g.FillRectangle(Brushes.Red, 0, 0, 10, 10);
            }

            using SKBitmap result = SKBitmap.FromImage(surface.Snapshot());
            Assert.Equal(new SKColor(255, 0, 0), result.GetPixel(5, 5));
            Assert.Equal(SKColors.White, result.GetPixel(15, 15));
        }

        [Fact]
        public void Graphics_FromSKCanvas_RespectsHostTransform()
        {
            using SKSurface surface = SKSurface.Create(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.Translate(10, 10);
            using (Graphics g = Graphics.FromSKCanvas(surface.Canvas))
            {
                g.FillRectangle(Brushes.Red, 0, 0, 5, 5);
            }

            using SKBitmap result = SKBitmap.FromImage(surface.Snapshot());
            Assert.Equal(new SKColor(255, 0, 0), result.GetPixel(12, 12));
            Assert.Equal(SKColors.White, result.GetPixel(2, 2));
            Assert.Equal(10f, surface.Canvas.TotalMatrix.TransX); // host state restored after Dispose
        }

        [Fact]
        public void DrawImage_ScaledAndSourceRect()
        {
            using var src = new Bitmap(2, 2);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(1, 0, Color.Green);
            src.SetPixel(0, 1, Color.Blue);
            src.SetPixel(1, 1, Color.White);
            using Bitmap dst = new Bitmap(8, 8);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(src, new Rectangle(0, 0, 8, 8), new Rectangle(0, 0, 2, 2), GraphicsUnit.Pixel);
            }

            Assert.Equal(Color.FromArgb(255, 255, 0, 0), dst.GetPixel(1, 1));
            Assert.Equal(Color.FromArgb(255, 0, 128, 0), dst.GetPixel(6, 1));
            Assert.Equal(Color.FromArgb(255, 0, 0, 255), dst.GetPixel(1, 6));
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), dst.GetPixel(6, 6));
        }

        [Fact]
        public void DrawImage_WithColorMatrixOpacity()
        {
            using var src = new Bitmap(4, 4);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.White);
            }

            using Bitmap dst = new Bitmap(4, 4);
            using (Graphics g = Graphics.FromImage(dst))
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.5f };
                attributes.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                g.DrawImage(src, new Rectangle(0, 0, 4, 4), 0, 0, 4, 4, GraphicsUnit.Pixel, attributes);
            }

            Color c = dst.GetPixel(2, 2);
            Assert.InRange(c.A, 126, 129);
            Assert.InRange(c.R, 253, 255);
        }

        [Fact]
        public void DrawImage_WithColorMatrix_Greyscale()
        {
            using var src = new Bitmap(2, 2);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.Red);
            }

            using Bitmap dst = new Bitmap(2, 2);
            using (Graphics g = Graphics.FromImage(dst))
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var cm = new System.Drawing.Imaging.ColorMatrix(new[]
                {
                    new[] { .3f, .3f, .3f, 0, 0 },
                    new[] { .59f, .59f, .59f, 0, 0 },
                    new[] { .11f, .11f, .11f, 0, 0 },
                    new[] { 0f, 0, 0, 1, 0 },
                    new[] { 0f, 0, 0, 0, 1 },
                });
                attributes.SetColorMatrix(cm);
                g.DrawImage(src, new Rectangle(0, 0, 2, 2), 0, 0, 2, 2, GraphicsUnit.Pixel, attributes);
            }

            Color c = dst.GetPixel(1, 1);
            Assert.InRange(c.R, 74, 78);
            Assert.Equal(c.R, c.G);
            Assert.Equal(c.R, c.B);
        }

        [Fact]
        public void DrawImage_ColorMatrixTranslation_IsOn255Scale()
        {
            using var src = new Bitmap(2, 2);
            using Bitmap dst = new Bitmap(2, 2);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.Black);
            }

            using (Graphics g = Graphics.FromImage(dst))
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                var cm = new System.Drawing.Imaging.ColorMatrix { Matrix40 = 0.5f };
                attributes.SetColorMatrix(cm);
                g.DrawImage(src, new Rectangle(0, 0, 2, 2), 0, 0, 2, 2, GraphicsUnit.Pixel, attributes);
            }

            Assert.InRange(dst.GetPixel(0, 0).R, 126, 129);
        }
    }
}
