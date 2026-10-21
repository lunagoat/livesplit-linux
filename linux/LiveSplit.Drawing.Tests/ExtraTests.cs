using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Threading;
using LiveSplit.UI;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    public class LegacySerializationTests
    {
        // TimesFont from LiveSplit's DefaultLayout.lsl: Segoe UI, 16px, Bold (BinaryFormatter blob written on Windows).
        private const string WindowsFontBlob = "AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABNTeXN0ZW0uRHJhd2luZy5Gb250BAAAAAROYW1lBFNpemUFU3R5bGUEVW5pdAEABAQLGFN5c3RlbS5EcmF3aW5nLkZvbnRTdHlsZQIAAAAbU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AgAAAAIAAAAGAwAAAAhTZWdvZSBVSQAAgEEF/P///xhTeXN0ZW0uRHJhd2luZy5Gb250U3R5bGUBAAAAB3ZhbHVlX18ACAIAAAABAAAABfv///8bU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AQAAAAd2YWx1ZV9fAAgCAAAAAgAAAAs=";

        [Fact]
        public void ReadsFontBlobWrittenByWindowsBinaryFormatter()
        {
            Font font = LegacySerialization.DeserializeFont(Convert.FromBase64String(WindowsFontBlob));
            Assert.Equal("Segoe UI", font.Name);
            Assert.Equal(16f, font.Size);
            Assert.Equal(FontStyle.Bold, font.Style);
            Assert.Equal(GraphicsUnit.Pixel, font.Unit);
        }

        [Fact]
        public void WritesByteIdenticalFontBlob()
        {
            byte[] original = Convert.FromBase64String(WindowsFontBlob);
            using Font font = LegacySerialization.DeserializeFont(original);
            Assert.Equal(original, LegacySerialization.SerializeFont(font));
        }

        [Fact]
        public void FontRoundTrip_VariousStyles()
        {
            using var f = new Font("Arial", 12.5f, FontStyle.Italic | FontStyle.Underline, GraphicsUnit.Point);
            Font back = LegacySerialization.DeserializeFont(LegacySerialization.SerializeFont(f));
            Assert.Equal(f.Name, back.Name);
            Assert.Equal(f.Size, back.Size);
            Assert.Equal(f.Style, back.Style);
            Assert.Equal(f.Unit, back.Unit);
            Assert.False(LegacySerialization.TryDeserializeFont(new byte[] { 1, 2, 3 }, out _));
        }

        [Fact]
        public void ImageRoundTrip()
        {
            using var bmp = new Bitmap(10, 6);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Teal);
            }

            byte[] blob = LegacySerialization.SerializeImage(bmp);
            using Image back = LegacySerialization.DeserializeImage(blob);
            Assert.Equal(new Size(10, 6), back.Size);
            Assert.Equal(Color.FromArgb(255, 0, 128, 128), ((Bitmap)back).GetPixel(3, 3));
        }
    }

    public class AnimatedGifTests
    {
        // 8x8, three frames (red, blue, green), 40ms each
        private const string Gif = "R0lGODlhCAAIAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQABAAAACwAAAAACAAIAAAIDwABCBxIsKDBgwgTKkwYEAAh+QQBBAABACwAAAAACAAIAIEAAP8AAAAAAAAAAAAIDwABCBxIsKDBgwgTKkwYEAAh+QQBBAABACwAAAAACAAIAIEA/wAAAAAAAAAAAAAIDwABCBxIsKDBgwgTKkwYEAA7";

        [Fact]
        public void DecodesAllFrames_AndSelectActiveFrameSwitchesPixels()
        {
            using Image img = Image.FromStream(new MemoryStream(Convert.FromBase64String(Gif)));
            var dim = new FrameDimension(img.FrameDimensionsList[0]);
            Assert.Equal(3, img.GetFrameCount(dim));
            var bmp = (Bitmap)img;
            Color first = bmp.GetPixel(2, 2);
            Assert.True(first.R > 200 && first.B < 50);
            img.SelectActiveFrame(dim, 1);
            Color second = bmp.GetPixel(2, 2);
            Assert.True(second.B > 200 && second.R < 50);
            img.SelectActiveFrame(dim, 2);
            Assert.True(bmp.GetPixel(2, 2).G > 200);
            Assert.True(ImageAnimator.CanAnimate(img));
        }

        [Fact]
        public void ImageAnimator_AdvancesFramesOnUpdate()
        {
            using Image img = Image.FromStream(new MemoryStream(Convert.FromBase64String(Gif)));
            int notifications = 0;
            ImageAnimator.Animate(img, (s, e) => Interlocked.Increment(ref notifications));
            var bmp = (Bitmap)img;
            Color start = bmp.GetPixel(2, 2);
            bool changed = false;
            for (int i = 0; i < 100 && !changed; i++)
            {
                Thread.Sleep(20);
                ImageAnimator.UpdateFrames(img);
                changed = bmp.GetPixel(2, 2) != start;
            }

            Assert.True(changed, "frame never advanced");
            Assert.True(notifications > 0);
            ImageAnimator.StopAnimate(img, null);
        }

        [Fact]
        public void GifCopy_KeepsFrames()
        {
            using Image img = Image.FromStream(new MemoryStream(Convert.FromBase64String(Gif)));
            using var copy = new Bitmap(img);
            Assert.Equal(3, copy.GetFrameCount(FrameDimension.Time));
        }
    }

    /// <summary>The unmodified LiveSplit source files (see Verbatim/) compile against the shim; run them to prove they work.</summary>
    public class VerbatimLiveSplitCodeTests
    {
        private static Bitmap Icon()
        {
            var b = new Bitmap(64, 64);
            using Graphics g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.OrangeRed, 4, 4, 56, 56);
            return b;
        }

        [Fact]
        public void ImageExtensions_Scale_DownscalesLargeImagesAndKeepsSmallOnes()
        {
            Bitmap big = new Bitmap(300, 150);
            using (Graphics g = Graphics.FromImage(big))
            {
                g.Clear(Color.CornflowerBlue);
            }

            Image scaled = big.ScaleIcon();
            Assert.Equal(new Size(96, 48), scaled.Size);
            Assert.True(TestUtil.Near(((Bitmap)scaled).GetPixel(40, 20), Color.CornflowerBlue, 3));
            using var small = new Bitmap(10, 10);
            Assert.Same(small, small.ScaleIcon());
        }

        [Fact]
        public void IconShadow_Generate_ProducesBlurredAlpha()
        {
            using Bitmap icon = Icon();
            using Bitmap shadow = IconShadow.Generate(icon, Color.FromArgb(255, 0, 0, 0));
            Assert.Equal(new Size(30, 30), shadow.Size);
            Assert.True(shadow.GetPixel(15, 15).A > 150);
            Assert.True(shadow.GetPixel(0, 0).A < 20);
            Assert.Equal(Color.Black.R, shadow.GetPixel(15, 15).R);
        }

        [Fact]
        public void ImageBlur_Generate_ReducesSizeAndSmoothsEdges()
        {
            using var src = new Bitmap(100, 100);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.Black);
                g.FillRectangle(Brushes.White, 50, 0, 50, 100);
            }

            using Bitmap blurred = ImageBlur.Generate(src, 5.0);
            Assert.True(blurred.Width < 100 && blurred.Width > 5);
            int mid = blurred.Width / 2;
            Color atEdge = blurred.GetPixel(mid, blurred.Height / 2);
            Assert.InRange(atEdge.R, 40, 215);
            Assert.True(blurred.GetPixel(1, 1).R < 40);
            Assert.True(blurred.GetPixel(blurred.Width - 2, 1).R > 215);
        }

        [Fact]
        public void SimpleLabel_DrawsAlignedTrimmedAndAlternateText()
        {
            using Bitmap bmp = TestUtil.White(300, 120);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var font = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel);
                var near = new SimpleLabel("Near", 0, 0, font, new SolidBrush(Color.Black), 300, 30, StringAlignment.Near, StringAlignment.Center) { HasShadow = false };
                var far = new SimpleLabel("Far", 0, 30, font, new SolidBrush(Color.Black), 300, 30, StringAlignment.Far, StringAlignment.Center) { HasShadow = false };
                var trimmed = new SimpleLabel("This text is definitely too long to fit into the label", 0, 60, font, new SolidBrush(Color.Black), 100, 30, StringAlignment.Near, StringAlignment.Center) { HasShadow = false };
                var alt = new SimpleLabel("Very Long Name", 100, 90, font, new SolidBrush(Color.Black), 100, 30, StringAlignment.Near, StringAlignment.Center, new[] { "Short" }) { HasShadow = false };
                near.Draw(g);
                far.Draw(g);
                trimmed.Draw(g);
                alt.Draw(g);
                Assert.True(alt.ActualWidth < 100);
                Assert.True(near.Width == 300);
            }

            TestUtil.SavePng(bmp, "simplelabel.png");
            Assert.True(InkInRect(bmp, new Rectangle(0, 0, 60, 30)) > 20);        // near text at left
            Assert.Equal(0, InkInRect(bmp, new Rectangle(100, 0, 100, 30)));      // nothing in the middle of the row
            Assert.True(InkInRect(bmp, new Rectangle(250, 30, 50, 30)) > 20);     // far text at right
            Assert.Equal(0, InkInRect(bmp, new Rectangle(101, 60, 199, 30)));     // trimmed text stays inside 100px
            Assert.True(InkInRect(bmp, new Rectangle(0, 60, 100, 30)) > 100);
            Assert.True(InkInRect(bmp, new Rectangle(100, 90, 100, 30)) > 20);
        }

        [Fact]
        public void SimpleLabel_ShadowAndOutlineRender()
        {
            using Bitmap bmp = TestUtil.White(200, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                var font = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
                new SimpleLabel("Shadow", 0, 0, font, new SolidBrush(Color.White), 100, 40, StringAlignment.Near, StringAlignment.Center).Draw(g);
                new SimpleLabel("Outline", 100, 0, font, new SolidBrush(Color.White), 100, 40, StringAlignment.Near, StringAlignment.Center) { OutlineColor = Color.Black }.Draw(g);
            }

            Assert.True(InkInRect(bmp, new Rectangle(0, 0, 100, 40)) > 30);
            Assert.True(InkInRect(bmp, new Rectangle(100, 0, 100, 40)) > 100);
        }

        private static int InkInRect(Bitmap bmp, Rectangle r)
        {
            int n = 0;
            for (int y = r.Top; y < r.Bottom && y < bmp.Height; y++)
            {
                for (int x = r.Left; x < r.Right && x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R < 200 || c.G < 200 || c.B < 200)
                    {
                        n++;
                    }
                }
            }

            return n;
        }
    }
}

namespace LiveSplit.Drawing.Tests
{
    public class MiscBrushPenTests
    {
        [Fact]
        public void TextureBrush_TilesImage()
        {
            using var tile = new Bitmap(2, 2);
            tile.SetPixel(0, 0, Color.Red);
            tile.SetPixel(1, 0, Color.Blue);
            tile.SetPixel(0, 1, Color.Blue);
            tile.SetPixel(1, 1, Color.Red);
            using var target = new Bitmap(8, 8);
            using (Graphics g = Graphics.FromImage(target))
            using (var brush = new TextureBrush(tile))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.FillRectangle(brush, 0, 0, 8, 8);
            }

            Assert.Equal(target.GetPixel(0, 0), target.GetPixel(4, 4));
            Assert.Equal(target.GetPixel(1, 0), target.GetPixel(5, 0));
            Assert.NotEqual(target.GetPixel(0, 0), target.GetPixel(1, 0));
        }

        [Fact]
        public void DashedPen_LeavesGaps()
        {
            using Bitmap bmp = TestUtil.White(40, 10);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.Black, 2f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pen, 0, 5, 40, 5);
            }

            int black = 0, white = 0;
            for (int x = 0; x < 40; x++)
            {
                if (bmp.GetPixel(x, 4).R < 128)
                {
                    black++;
                }
                else
                {
                    white++;
                }
            }

            Assert.True(black > 10 && white > 5, $"{black}/{white}");
        }

        [Fact]
        public void RoundCapPen_GeneratesEnds_And_PenFromBrush()
        {
            using Bitmap bmp = TestUtil.White(30, 30);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Brushes.Blue, 8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, 10, 15, 20, 15);
            }

            Assert.Equal(Color.FromArgb(255, 0, 0, 255), bmp.GetPixel(15, 15));
            Assert.True(bmp.GetPixel(7, 15).B > 200 && bmp.GetPixel(7, 15).R < 100); // round cap extends 4px left of x=10
        }

        [Fact]
        public void BrushesAndPens_StaticColors()
        {
            Assert.Equal(Color.White, ((SolidBrush)Brushes.White).Color);
            Assert.Equal(Color.Red, Pens.Red.Color);
            Assert.Equal(1f, Pens.Black.Width);
            Assert.Equal(0, ((SolidBrush)Brushes.Transparent).Color.A);
        }
    }
}
