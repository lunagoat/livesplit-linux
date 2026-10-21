using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    public class TextTests
    {
        private static Font Arial(float px, FontStyle style = FontStyle.Regular) => new Font("Arial", px, style, GraphicsUnit.Pixel);

        [Fact]
        public void Font_Properties_MatchGdiPlusConventions()
        {
            using var f = new Font("Segoe UI", 12f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point);
            Assert.Equal("Segoe UI", f.Name);
            Assert.Equal("Segoe UI", f.FontFamily.Name);
            Assert.Equal(12f, f.Size);
            Assert.Equal(12f, f.SizeInPoints);
            Assert.Equal(GraphicsUnit.Point, f.Unit);
            Assert.True(f.Bold);
            Assert.True(f.Italic);
            Assert.False(f.Underline);
            Assert.True(f.Height >= 16 && f.Height <= 26, "Height " + f.Height);

            using var px = new Font("Segoe UI", 16f, GraphicsUnit.Pixel);
            Assert.Equal(12f, px.SizeInPoints, 3);
            Assert.True(px.GetHeight() >= 16f);
            using Font clone = (Font)px.Clone();
            Assert.Equal(px.Name, clone.Name);
            Assert.Equal(px.Size, clone.Size);
            Assert.Equal(px.Unit, clone.Unit);
        }

        [Fact]
        public void Font_UnavailableFamily_FallsBackWithoutThrowing()
        {
            using var f = new Font("Definitely Not A Real Font 12345", 14f, GraphicsUnit.Pixel);
            using Bitmap bmp = TestUtil.White(120, 30);
            using Graphics g = Graphics.FromImage(bmp);
            SizeF size = g.MeasureString("Hello", f);
            Assert.True(size.Width > 10);
            Assert.Equal("Definitely Not A Real Font 12345", f.Name);
        }

        [Fact]
        public void FontFamily_ListsInstalledFamilies_AndMetrics()
        {
            FontFamily[] families = FontFamily.Families;
            Assert.NotEmpty(families);
            Assert.All(families, f => Assert.False(string.IsNullOrEmpty(f.Name)));
            Assert.Equal(families.Length, families.Select(f => f.Name.ToLowerInvariant()).Distinct().Count());
            FontFamily ff = FontFamily.GenericSansSerif;
            Assert.True(ff.GetEmHeight(FontStyle.Regular) > 0);
            Assert.True(ff.GetCellAscent(FontStyle.Regular) > 0);
            Assert.True(ff.GetCellDescent(FontStyle.Regular) > 0);
            Assert.True(ff.GetLineSpacing(FontStyle.Regular) >= ff.GetCellAscent(FontStyle.Regular) + ff.GetCellDescent(FontStyle.Regular));
            Assert.True(ff.IsStyleAvailable(FontStyle.Regular));
            using var installed = new InstalledFontCollection();
            Assert.Equal(families.Length, installed.Families.Length);
        }

        [Fact]
        public void MeasureString_ReturnsPaddedWidthAndLineHeight()
        {
            using Font f = Arial(20);
            using Bitmap bmp = new Bitmap(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            SizeF hello = g.MeasureString("Hello", f);
            Assert.True(hello.Width > 0);
            Assert.Equal(f.GetHeight(g), hello.Height, 2);

            SizeF typographic = g.MeasureString("Hello", f, new PointF(0, 0), StringFormat.GenericTypographic);
            // Default GDI+ measurement adds em/6 on both sides.
            Assert.Equal(typographic.Width + (2 * 20f / 6f), hello.Width, 1);

            SizeF longer = g.MeasureString("Hello World", f);
            Assert.True(longer.Width > hello.Width);
            Assert.Equal(hello.Height, longer.Height, 2);
            Assert.Equal(SizeF.Empty, g.MeasureString("", f));
        }

        [Fact]
        public void MeasureString_WrapsToWidth()
        {
            using Font f = Arial(20);
            using Bitmap bmp = new Bitmap(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            string text = "The quick brown fox jumps over the lazy dog";
            SizeF single = g.MeasureString(text, f);
            SizeF wrapped = g.MeasureString(text, f, 150);
            Assert.True(wrapped.Width <= 150.01f);
            Assert.True(wrapped.Height > single.Height * 1.9f, $"{wrapped.Height} vs {single.Height}");
            SizeF wrapped2 = g.MeasureString(text, f, new SizeF(150, 1000), null, out int chars, out int lines);
            Assert.Equal(wrapped, wrapped2);
            Assert.True(lines >= 3);
            Assert.Equal(text.Length, chars);

            var noWrap = new StringFormat { FormatFlags = StringFormatFlags.NoWrap };
            SizeF nw = g.MeasureString(text, f, 9999, noWrap);
            Assert.Equal(single.Width, nw.Width, 1);
            Assert.Equal(single.Height, nw.Height, 1);

            SizeF newline = g.MeasureString("a\nb", f);
            Assert.Equal(single.Height * 2, newline.Height, 1);
        }

        [Fact]
        public void MeasureString_TrimsToLayoutWidthWithEllipsis()
        {
            using Font f = Arial(20);
            using Bitmap bmp = new Bitmap(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
            SizeF full = g.MeasureString("A very long split name that does not fit", f, 9999, format);
            SizeF trimmed = g.MeasureString("A very long split name that does not fit", f, 120, format);
            Assert.True(trimmed.Width <= 120.01f);
            Assert.True(full.Width > 120);
        }

        [Fact]
        public void DrawString_RendersAntialiasedLegibleText()
        {
            using Bitmap bmp = TestUtil.White(260, 120);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(28, FontStyle.Bold))
            using (Font small = Arial(14))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var gradient = new LinearGradientBrush(new PointF(0, 10), new PointF(0, 50), Color.Black, Color.FromArgb(255, 0, 90, 200));
                g.DrawString("Hello 1:23.45", f, gradient, new RectangleF(5, 5, 250, 50), new StringFormat());
                g.DrawString("Anti-aliased small text", small, Brushes.Black, 5, 60);
                g.DrawString("Right aligned", small, Brushes.DarkRed, new RectangleF(0, 90, 255, 25),
                    new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            }

            TestUtil.SavePng(bmp, "text.png");

            int dark = 0, grey = 0;
            for (int y = 5; y < 50; y++)
            {
                for (int x = 5; x < 250; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R < 80 && c.G < 120)
                    {
                        dark++;
                    }
                    else if (c.R < 240 && c.R > 20)
                    {
                        grey++;
                    }
                }
            }

            Assert.True(dark > 100, "expected solid glyph pixels, got " + dark);
            Assert.True(grey > 100, "expected antialiased edge pixels, got " + grey);
            // The header band must remain empty below the text
            Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(250, 56));
        }

        [Fact]
        public void DrawString_AlignmentPlacesTextWhereExpected()
        {
            using Bitmap bmp = TestUtil.White(200, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(16))
            {
                var far = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString("W", f, Brushes.Black, new RectangleF(0, 0, 200, 40), far);
            }

            // ink must be in the lower right quadrant only
            (int minX, int minY, int maxX, int maxY) = Ink(bmp);
            Assert.True(minX > 150, "minX " + minX);
            Assert.True(minY > 15, "minY " + minY);
            Assert.True(maxX < 200 && maxY < 40);
        }

        [Fact]
        public void DrawString_ClipsToLayoutRectangle()
        {
            using Bitmap bmp = TestUtil.White(100, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(30))
            {
                g.DrawString("WWWWWWWW", f, Brushes.Black, new RectangleF(10, 5, 40, 30), new StringFormat { FormatFlags = StringFormatFlags.NoWrap });
            }

            (int minX, _, int maxX, _) = Ink(bmp);
            Assert.True(minX >= 10);
            Assert.True(maxX < 50, "maxX " + maxX);
        }

        [Fact]
        public void DrawString_EllipsisTrimmingKeepsTextInsideRect()
        {
            using Bitmap bmp = TestUtil.White(160, 30);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(16))
            {
                var fmt = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString("This is a very long label that cannot fit", f, Brushes.Black, new RectangleF(0, 0, 100, 30), fmt);
            }

            (_, _, int maxX, _) = Ink(bmp);
            Assert.True(maxX < 100, "maxX " + maxX);
            Assert.True(maxX > 60, "ellipsis should reach near the right edge: " + maxX);
        }

        [Fact]
        public void DrawString_WithShadowAndOutlinePath_Works()
        {
            using Bitmap bmp = TestUtil.White(200, 60);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(30, FontStyle.Bold))
            using (var path = new GraphicsPath())
            using (var outline = new Pen(Color.Black, 3f) { LineJoin = LineJoin.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                path.AddString("Path", f.FontFamily, (int)f.Style, f.Size, new RectangleF(5, 5, 190, 50), new StringFormat());
                g.FillPath(Brushes.Gold, path);
                g.DrawPath(outline, path);
            }

            TestUtil.SavePng(bmp, "textpath.png");
            (int minX, int minY, int maxX, int maxY) = Ink(bmp);
            Assert.True(maxX > minX + 40 && maxY > minY + 15);
        }

        [Fact]
        public void GraphicsPath_AddString_BoundsAreSane()
        {
            using var path = new GraphicsPath();
            path.AddString("Hello", FontFamily.GenericSansSerif, (int)FontStyle.Regular, 40f, new PointF(0, 0), StringFormat.GenericTypographic);
            RectangleF b = path.GetBounds();
            Assert.True(b.Width > 60 && b.Width < 140, "w " + b.Width);
            Assert.True(b.Height > 20 && b.Height < 45, "h " + b.Height);
        }

        [Fact]
        public void Unicode_FallbackFontsAreUsedForMissingGlyphs()
        {
            using Font f = Arial(24);
            using Bitmap bmp = new Bitmap(10, 10);
            using Graphics g = Graphics.FromImage(bmp);
            float latin = g.MeasureString("ab", f).Width;
            float mixed = g.MeasureString("abあい", f).Width; // hiragana
            Assert.True(mixed > latin + 20);
        }

        [Fact]
        public void TextRenderingHint_SingleBitPerPixelDisablesAntialiasing()
        {
            using Bitmap bmp = TestUtil.White(100, 30);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font f = Arial(20))
            {
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.DrawString("Aliased", f, Brushes.Black, 2, 2);
            }

            bool anyGrey = false;
            for (int y = 0; y < 30 && !anyGrey; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R > 20 && c.R < 235)
                    {
                        anyGrey = true;
                        break;
                    }
                }
            }

            Assert.False(anyGrey);
        }

        private static (int minX, int minY, int maxX, int maxY) Ink(Bitmap bmp)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.R < 200 || c.G < 200 || c.B < 200)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            return (minX, minY, maxX, maxY);
        }
    }
}
