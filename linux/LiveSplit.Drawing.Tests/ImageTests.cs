using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    public class ImageTests
    {
        private static Bitmap Sample()
        {
            var bmp = new Bitmap(16, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 16, y * 32, 128));
                }
            }

            bmp.SetPixel(0, 0, Color.FromArgb(128, 200, 100, 50));
            bmp.SetPixel(1, 0, Color.FromArgb(0, 0, 0, 0));
            return bmp;
        }

        [Fact]
        public void Bitmap_BasicProperties()
        {
            using var bmp = new Bitmap(30, 20);
            Assert.Equal(30, bmp.Width);
            Assert.Equal(20, bmp.Height);
            Assert.Equal(new Size(30, 20), bmp.Size);
            Assert.Equal(new SizeF(30, 20), bmp.PhysicalDimension);
            Assert.Equal(96f, bmp.HorizontalResolution);
            Assert.Equal(PixelFormat.Format32bppArgb, bmp.PixelFormat);
            Assert.Equal(0, bmp.GetPixel(3, 3).A); // transparent by default
            using var rgb = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), rgb.GetPixel(1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => bmp.GetPixel(30, 0));
            Assert.Throws<ArgumentException>(() => new Bitmap(0, 5));
        }

        [Fact]
        public void GetSetPixel_PreservesStraightAlpha()
        {
            using var bmp = new Bitmap(2, 2);
            bmp.SetPixel(0, 0, Color.FromArgb(128, 200, 100, 50));
            Color c = bmp.GetPixel(0, 0);
            Assert.Equal(128, c.A);
            Assert.InRange(c.R, 199, 201);
            Assert.InRange(c.G, 99, 101);
            Assert.InRange(c.B, 49, 51);
        }

        [Fact]
        public void Png_RoundTrip_ThroughStreamAndFile()
        {
            using Bitmap src = Sample();
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using Image loaded = Image.FromStream(ms);
            Assert.IsType<Bitmap>(loaded);
            Assert.Equal(src.Size, loaded.Size);
            Assert.True(loaded.RawFormat.Equals(ImageFormat.Png));
            var lb = (Bitmap)loaded;
            Assert.Equal(src.GetPixel(5, 3), lb.GetPixel(5, 3));
            Assert.Equal(src.GetPixel(15, 7), lb.GetPixel(15, 7));
            Assert.Equal(128, lb.GetPixel(0, 0).A);

            string path = Path.Combine(TestUtil.OutDir, "roundtrip.png");
            src.Save(path);
            using var fromFile = new Bitmap(path);
            Assert.Equal(src.GetPixel(9, 4), fromFile.GetPixel(9, 4));
            using Image viaFrom = Image.FromFile(path);
            Assert.Equal(16, viaFrom.Width);
        }

        [Theory]
        [InlineData("Jpeg")]
        [InlineData("Bmp")]
        [InlineData("Gif")]
        [InlineData("Icon")]
        public void OtherFormats_SaveAndDecode(string name)
        {
            ImageFormat format = (ImageFormat)typeof(ImageFormat).GetProperty(name).GetValue(null);
            using var src = new Bitmap(24, 24);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.White);
                g.FillRectangle(Brushes.Red, 0, 0, 12, 24);
                g.FillRectangle(Brushes.Blue, 12, 0, 12, 24);
            }

            using var ms = new MemoryStream();
            src.Save(ms, format);
            Assert.True(ms.Length > 20);
            ms.Position = 0;
            using var loaded = new Bitmap(ms);
            Assert.Equal(24, loaded.Width);
            Assert.Equal(24, loaded.Height);
            Color l = loaded.GetPixel(4, 12), r = loaded.GetPixel(20, 12);
            Assert.True(l.R > 200 && l.G < 70 && l.B < 70, $"{name}: left {l}");
            Assert.True(r.B > 200 && r.R < 70 && r.G < 70, $"{name}: right {r}");
        }

        [Fact]
        public void Gif_PreservesTransparency()
        {
            using var src = new Bitmap(8, 8);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.FillRectangle(Brushes.Black, 0, 0, 4, 8);
            }

            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Gif);
            ms.Position = 0;
            using var loaded = new Bitmap(ms);
            Assert.Equal(255, loaded.GetPixel(1, 1).A);
            Assert.Equal(0, loaded.GetPixel(6, 1).A);
        }

        [Fact]
        public void InvalidData_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Image.FromStream(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));
            Assert.Throws<ArgumentException>(() => new Bitmap(new MemoryStream(Array.Empty<byte>())));
        }

        [Fact]
        public void CopyConstructors_AreIndependent()
        {
            using Bitmap src = Sample();
            using var copy = new Bitmap(src);
            using var scaled = new Bitmap(src, 32, 16);
            using var cloned = (Bitmap)src.Clone();
            src.SetPixel(5, 5, Color.Magenta);
            Assert.NotEqual(Color.FromArgb(255, 255, 0, 255), copy.GetPixel(5, 5));
            Assert.NotEqual(Color.FromArgb(255, 255, 0, 255), cloned.GetPixel(5, 5));
            Assert.Equal(new Size(32, 16), scaled.Size);
            Assert.Equal(src.PixelFormat, copy.PixelFormat);
            using Bitmap part = src.Clone(new Rectangle(2, 2, 4, 4), PixelFormat.Format32bppArgb);
            Assert.Equal(new Size(4, 4), part.Size);
        }

        [Fact]
        public void LockBits_ReadsStraightArgb_AndWritesBack()
        {
            using Bitmap bmp = Sample();
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, 16, 8), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                Assert.Equal(16 * 4, data.Stride);
                Assert.Equal(16, data.Width);
                Assert.NotEqual(IntPtr.Zero, data.Scan0);
                int px = Marshal.ReadInt32(data.Scan0, (3 * data.Stride) + (5 * 4));
                Assert.Equal(unchecked((int)0xFF000000 | (5 * 16 << 16) | (3 * 32 << 8) | 128), px);
                int translucent = Marshal.ReadInt32(data.Scan0);
                Assert.Equal(128, (translucent >> 24) & 0xFF);
                Assert.InRange((translucent >> 16) & 0xFF, 199, 201);

                // write an opaque green pixel at (7,2) and a half transparent red one at (8,2)
                Marshal.WriteInt32(data.Scan0, (2 * data.Stride) + (7 * 4), unchecked((int)0xFF00FF00));
                Marshal.WriteInt32(data.Scan0, (2 * data.Stride) + (8 * 4), unchecked((int)0x80FF0000));
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            Assert.Equal(Color.FromArgb(255, 0, 255, 0), bmp.GetPixel(7, 2));
            Color half = bmp.GetPixel(8, 2);
            Assert.Equal(128, half.A);
            Assert.InRange(half.R, 253, 255);
        }

        [Fact]
        public void LockBits_ReadOnly_DoesNotModify_AndPartialRect()
        {
            using Bitmap bmp = Sample();
            Color before = bmp.GetPixel(10, 5);
            BitmapData data = bmp.LockBits(new Rectangle(8, 4, 4, 2), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Assert.Equal(4, data.Width);
            Assert.Equal(2, data.Height);
            int first = Marshal.ReadInt32(data.Scan0);
            Marshal.WriteInt32(data.Scan0, 0);
            bmp.UnlockBits(data);
            Assert.Equal(before, bmp.GetPixel(10, 5));
            Assert.Equal(bmp.GetPixel(8, 4).ToArgb(), first);
        }

        [Fact]
        public void LockBits_24bppRgb()
        {
            using var bmp = new Bitmap(3, 2, PixelFormat.Format24bppRgb);
            bmp.SetPixel(1, 1, Color.FromArgb(255, 10, 20, 30));
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, 3, 2), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            Assert.Equal(12, data.Stride); // 3*3=9 rounded up to 4
            byte b = Marshal.ReadByte(data.Scan0, data.Stride + 3);
            byte g = Marshal.ReadByte(data.Scan0, data.Stride + 4);
            byte r = Marshal.ReadByte(data.Scan0, data.Stride + 5);
            Assert.Equal((10, 20, 30), (r, g, b));
            Marshal.WriteByte(data.Scan0, 0, 99);
            bmp.UnlockBits(data);
            Assert.Equal(99, bmp.GetPixel(0, 0).B);
        }

        [Fact]
        public void LockBits_32bppPArgb_IsPremultiplied()
        {
            using var bmp = new Bitmap(1, 1);
            bmp.SetPixel(0, 0, Color.FromArgb(128, 200, 100, 50));
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            int v = Marshal.ReadInt32(data.Scan0);
            bmp.UnlockBits(data);
            Assert.InRange((v >> 16) & 0xFF, 99, 101);
        }

        [Fact]
        public void DrawingOnBitmap_InvalidatesImageCache()
        {
            using var a = new Bitmap(4, 4);
            using var b = new Bitmap(4, 4);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.DrawImage(a, 0, 0); // caches a's SKImage
            }

            using (Graphics g = Graphics.FromImage(a))
            {
                g.Clear(Color.Red);
            }

            using (Graphics g = Graphics.FromImage(b))
            {
                g.DrawImage(a, 0, 0);
            }

            Assert.Equal(Color.FromArgb(255, 255, 0, 0), b.GetPixel(1, 1));
        }

        [Fact]
        public void ScaleHelper_LikeLiveSplitImageExtensions_Works()
        {
            // Mirrors LiveSplit.UI.ImageExtensions.Scale: new Bitmap(w, h, image.PixelFormat) + DrawImage with attributes
            using Bitmap big = new Bitmap(200, 100);
            using (Graphics g = Graphics.FromImage(big))
            {
                g.Clear(Color.Orange);
            }

            using var bitmap = new Bitmap(96, 48, big.PixelFormat);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                var attributes = new ImageAttributes();
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(big, new Rectangle(0, 0, 96, 48), 0, 0, big.Width, big.Height, GraphicsUnit.Pixel, attributes);
            }

            Color c = bitmap.GetPixel(48, 24);
            Assert.True(TestUtil.Near(c, Color.Orange, 3), c.ToString());
        }

        [Fact]
        public void RotateFlip_Rotate90_MovesPixels()
        {
            using var bmp = new Bitmap(4, 2);
            bmp.SetPixel(0, 0, Color.Red);
            bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            Assert.Equal(new Size(2, 4), bmp.Size);
            Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(1, 0)); // top-left goes to top-right
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
            Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(0, 0));
        }

        [Fact]
        public void Disposed_Image_DoesNotCrashDrawing()
        {
            var a = new Bitmap(4, 4);
            a.Dispose();
            using var b = new Bitmap(4, 4);
            using Graphics g = Graphics.FromImage(b);
            g.DrawImage(a, 0, 0); // GDI+ would throw; we silently skip
            Assert.Equal(4, a.Width);
            Assert.Throws<ArgumentException>(() => Graphics.FromImage(a));
        }

        [Fact]
        public void Animation_SingleFrameImagesAreNotAnimatable()
        {
            using var bmp = new Bitmap(2, 2);
            Assert.False(ImageAnimator.CanAnimate(bmp));
            ImageAnimator.Animate(bmp, (s, e) => { });
            ImageAnimator.UpdateFrames(bmp);
            Assert.Equal(1, bmp.GetFrameCount(new FrameDimension(bmp.FrameDimensionsList[0])));
        }

        [Fact]
        public void Icon_ToBitmap_FromIcoStream()
        {
            using var src = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.Clear(Color.Purple);
            }

            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Icon);
            ms.Position = 0;
            using var icon = new Icon(ms);
            Assert.Equal(32, icon.Width);
            using Bitmap bmp = icon.ToBitmap();
            Assert.Equal(Color.FromArgb(255, 128, 0, 128), bmp.GetPixel(10, 10));
        }

        [Fact]
        public void ImageFormat_Members()
        {
            Assert.Equal("Png", ImageFormat.Png.ToString());
            Assert.True(ImageFormat.Png.Equals(new ImageFormat(ImageFormat.Png.Guid)));
            Assert.False(ImageFormat.Png.Equals(ImageFormat.Jpeg));
        }
    }
}
