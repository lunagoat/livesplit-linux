using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing
{
    public sealed class Bitmap : Image
    {
        private sealed class LockRecord
        {
            public IntPtr Scan0;
            public Rectangle Rect;
            public ImageLockMode Mode;
            public PixelFormat Format;
            public int Stride;
        }

        private readonly List<LockRecord> _locks = new();

        // ------------------------------------------------------------------ construction

        private static SKBitmap Allocate(int width, int height, PixelFormat format)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Parameter is not valid.");
            }

            var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            // Formats without alpha start as opaque black (all-zero pixels in GDI+); alpha formats start transparent.
            bmp.Erase(Image.IsAlphaPixelFormat(format) || format == PixelFormat.DontCare ? SKColors.Transparent : SKColors.Black);
            return bmp;
        }

        public Bitmap(int width, int height) : this(width, height, PixelFormat.Format32bppArgb)
        {
        }

        public Bitmap(int width, int height, PixelFormat format)
        {
            SetFrames([new Frame { Bitmap = Allocate(width, height, format) }]);
            PixelFormat = format == PixelFormat.DontCare ? PixelFormat.Format32bppArgb : format;
        }

        public Bitmap(int width, int height, Graphics g) : this(width, height, PixelFormat.Format32bppPArgb)
        {
            if (g != null)
            {
                SetResolution(g.DpiX, g.DpiY);
            }
        }

        public Bitmap(int width, int height, int stride, PixelFormat format, IntPtr scan0) : this(width, height, format)
        {
            if (scan0 == IntPtr.Zero)
            {
                return;
            }

            var data = new BitmapData { Width = width, Height = height, Stride = stride, PixelFormat = format, Scan0 = scan0 };
            WriteFromBuffer(CurrentBitmap, new Rectangle(0, 0, width, height), data);
        }

        public Bitmap(Image original) : this(original, original.Width, original.Height)
        {
        }

        public Bitmap(Image original, Size newSize) : this(original, newSize.Width, newSize.Height)
        {
        }

        public Bitmap(Image original, int width, int height)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            var frames = new List<Frame>();
            bool same = width == original.Width && height == original.Height;
            foreach (Frame f in original.Frames)
            {
                var dst = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                if (same)
                {
                    f.Bitmap.CopyTo(dst);
                }
                else
                {
                    dst.Erase(SKColors.Transparent);
                    using var canvas = new SKCanvas(dst);
                    using var img = SKImage.FromBitmap(f.Bitmap);
                    using var paint = new SKPaint { IsAntialias = true };
                    canvas.DrawImage(img, new SKRect(0, 0, f.Bitmap.Width, f.Bitmap.Height), new SKRect(0, 0, width, height),
                        GraphicsHelper.SamplingFor(System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic, f.Bitmap.Width / (float)width), paint);
                }

                frames.Add(new Frame { Bitmap = dst, DelayMs = f.DelayMs });
            }

            SetFrames(frames);
            PixelFormat = original.PixelFormat;
            HorizontalResolution = original.HorizontalResolution;
            VerticalResolution = original.VerticalResolution;
            RawFormat = original.RawFormat;
            if (original.CurrentFrameIndex > 0)
            {
                SelectActiveFrame(FrameDimension.Time, original.CurrentFrameIndex);
            }
        }

        public Bitmap(Stream stream) : this(stream, false)
        {
        }

        public Bitmap(Stream stream, bool useIcm)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            Decode(ms.ToArray());
        }

        public Bitmap(string filename) : this(filename, false)
        {
        }

        public Bitmap(string filename, bool useIcm)
        {
            if (!File.Exists(filename))
            {
                throw new ArgumentException("Parameter is not valid.", nameof(filename));
            }

            Decode(File.ReadAllBytes(filename));
        }

        /// <summary>Wraps an existing SKBitmap (any colour type is converted to premultiplied BGRA8888; the original is not kept).</summary>
        public Bitmap(SKBitmap source)
        {
            SetFrames([new Frame { Bitmap = ToBgraPremul(source, false) }]);
        }

        internal Bitmap(byte[] encoded, bool unused)
        {
            Decode(encoded);
        }

        /// <summary>Creates a bitmap from an SKImage (copies the pixels).</summary>
        public static Bitmap FromSKImage(SKImage image)
        {
            using SKBitmap bmp = SKBitmap.FromImage(image);
            return new Bitmap(bmp);
        }

        private static SKBitmap ToBgraPremul(SKBitmap src, bool takeOwnership)
        {
            if (src.ColorType == SKColorType.Bgra8888 && src.AlphaType == SKAlphaType.Premul)
            {
                if (takeOwnership)
                {
                    return src;
                }

                var copy = new SKBitmap(src.Info);
                src.CopyTo(copy);
                return copy;
            }

            var dst = new SKBitmap(new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            if (!src.CopyTo(dst, SKColorType.Bgra8888))
            {
                dst.Erase(SKColors.Transparent);
                using var canvas = new SKCanvas(dst);
                canvas.DrawBitmap(src, 0, 0);
            }

            if (takeOwnership)
            {
                src.Dispose();
            }

            return dst;
        }

        private void Decode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Parameter is not valid.");
            }

            SKCodec codec = null;
            try
            {
                codec = SKCodec.Create(new MemoryStream(data));
                if (codec == null)
                {
                    throw new ArgumentException("Parameter is not valid.");
                }

                SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var frames = new List<Frame>();
                int frameCount = Math.Max(1, codec.FrameCount);
                if (frameCount == 1)
                {
                    var bmp = new SKBitmap(info);
                    IntPtr ptr = bmp.GetPixels();
                    SKCodecResult result = codec.GetPixels(info, ptr);
                    if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                    {
                        bmp.Dispose();
                        throw new ArgumentException("Parameter is not valid.");
                    }

                    frames.Add(new Frame { Bitmap = bmp });
                }
                else
                {
                    SKCodecFrameInfo[] infos = codec.FrameInfo;
                    frameCount = Math.Min(frameCount, 1000);
                    for (int i = 0; i < frameCount; i++)
                    {
                        var bmp = new SKBitmap(info);
                        int required = infos[i].RequiredFrame;
                        if (required >= 0 && required < frames.Count)
                        {
                            frames[required].Bitmap.CopyTo(bmp);
                        }
                        else
                        {
                            bmp.Erase(SKColors.Transparent);
                        }

                        var options = new SKCodecOptions(i) { PriorFrame = required };
                        if (required >= 0 && required < frames.Count)
                        {
                            options = new SKCodecOptions(i, required);
                        }

                        SKCodecResult result = codec.GetPixels(info, bmp.GetPixels(), options);
                        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                        {
                            bmp.Dispose();
                            if (frames.Count == 0)
                            {
                                throw new ArgumentException("Parameter is not valid.");
                            }

                            break;
                        }

                        int delay = infos[i].Duration;
                        frames.Add(new Frame { Bitmap = bmp, DelayMs = delay <= 10 ? 100 : delay });
                    }
                }

                SetFrames(frames);
                switch (codec.EncodedFormat)
                {
                    case SKEncodedImageFormat.Jpeg:
                        RawFormat = ImageFormat.Jpeg;
                        PixelFormat = PixelFormat.Format24bppRgb;
                        break;
                    case SKEncodedImageFormat.Png:
                        RawFormat = ImageFormat.Png;
                        PixelFormat = codec.Info.AlphaType == SKAlphaType.Opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
                        break;
                    case SKEncodedImageFormat.Gif:
                        RawFormat = ImageFormat.Gif;
                        break;
                    case SKEncodedImageFormat.Bmp:
                        RawFormat = ImageFormat.Bmp;
                        PixelFormat = codec.Info.AlphaType == SKAlphaType.Opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
                        break;
                    case SKEncodedImageFormat.Ico:
                        RawFormat = ImageFormat.Icon;
                        break;
                    case SKEncodedImageFormat.Webp:
                        RawFormat = ImageFormat.Webp;
                        break;
                    default:
                        RawFormat = ImageFormat.MemoryBmp;
                        break;
                }
            }
            finally
            {
                codec?.Dispose();
            }
        }

        // ------------------------------------------------------------------ pixel access

        public void SetResolution(float xDpi, float yDpi)
        {
            HorizontalResolution = xDpi;
            VerticalResolution = yDpi;
        }

        private static void CheckBounds(SKBitmap bmp, int x, int y)
        {
            if (x < 0 || y < 0 || x >= bmp.Width || y >= bmp.Height)
            {
                throw new ArgumentOutOfRangeException(x < 0 || x >= bmp.Width ? nameof(x) : nameof(y), "Parameter must be positive and < Width/Height.");
            }
        }

        public Color GetPixel(int x, int y)
        {
            SKBitmap bmp = CurrentBitmap;
            CheckBounds(bmp, x, y);
            ReadOnlySpan<byte> px = bmp.GetPixelSpan();
            int o = (y * bmp.RowBytes) + (x * 4);
            byte b = px[o], g = px[o + 1], r = px[o + 2], a = px[o + 3];
            if (a == 255 || a == 0)
            {
                return Color.FromArgb(a, r, g, b);
            }

            return Color.FromArgb(a, Unpremul(r, a), Unpremul(g, a), Unpremul(b, a));
        }

        public void SetPixel(int x, int y, Color color)
        {
            SKBitmap bmp = CurrentBitmap;
            CheckBounds(bmp, x, y);
            Span<byte> px = bmp.GetPixelSpan();
            int o = (y * bmp.RowBytes) + (x * 4);
            byte a = color.A;
            if (!Image.IsAlphaPixelFormat(PixelFormat))
            {
                a = 255;
            }

            px[o] = Premul(color.B, a);
            px[o + 1] = Premul(color.G, a);
            px[o + 2] = Premul(color.R, a);
            px[o + 3] = a;
            InvalidateCache();
        }

        public void MakeTransparent() => MakeTransparent(Color.LightGray);

        public void MakeTransparent(Color transparentColor)
        {
            SKBitmap bmp = CurrentBitmap;
            Span<byte> px = bmp.GetPixelSpan();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int o = (y * bmp.RowBytes) + (x * 4);
                    if (px[o + 3] == 255 && px[o] == transparentColor.B && px[o + 1] == transparentColor.G && px[o + 2] == transparentColor.R)
                    {
                        px[o] = px[o + 1] = px[o + 2] = px[o + 3] = 0;
                    }
                }
            }

            InvalidateCache();
        }

        private static byte Premul(byte c, byte a) => a == 255 ? c : (byte)(((c * a) + 127) / 255);

        private static byte Unpremul(byte c, byte a) => a == 0 ? (byte)0 : (byte)Math.Min(255, ((c * 255) + (a / 2)) / a);

        private static int BytesPerPixel(PixelFormat f) => f switch
        {
            PixelFormat.Format24bppRgb => 3,
            _ => 4,
        };

        public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format)
        {
            ThrowIfDisposed();
            SKBitmap bmp = CurrentBitmap;
            if (rect.Width <= 0 || rect.Height <= 0 || rect.X < 0 || rect.Y < 0 || rect.Right > bmp.Width || rect.Bottom > bmp.Height)
            {
                throw new ArgumentException("Parameter is not valid.", nameof(rect));
            }

            if (format is not (PixelFormat.Format24bppRgb or PixelFormat.Format32bppRgb or PixelFormat.Format32bppPArgb or PixelFormat.Format32bppArgb))
            {
                // Indexed / 16 bit / 48 bit formats are not supported; hand out 32bpp ARGB.
                format = PixelFormat.Format32bppArgb;
            }

            int bpp = BytesPerPixel(format);
            int stride = (((rect.Width * bpp) + 3) / 4) * 4;
            IntPtr buffer = Marshal.AllocHGlobal(stride * rect.Height);
            var data = new BitmapData { Width = rect.Width, Height = rect.Height, Stride = stride, PixelFormat = format, Scan0 = buffer };

            if ((flags & ImageLockMode.WriteOnly) == 0 || (flags & ImageLockMode.ReadOnly) != 0)
            {
                ReadToBuffer(bmp, rect, data);
            }
            else
            {
                unsafe
                {
                    new Span<byte>((void*)buffer, stride * rect.Height).Clear();
                }
            }

            lock (_locks)
            {
                _locks.Add(new LockRecord { Scan0 = buffer, Rect = rect, Mode = flags, Format = format, Stride = stride });
            }

            return data;
        }

        public void UnlockBits(BitmapData bitmapdata)
        {
            if (bitmapdata == null)
            {
                throw new ArgumentNullException(nameof(bitmapdata));
            }

            LockRecord rec = null;
            lock (_locks)
            {
                int i = _locks.FindIndex(l => l.Scan0 == bitmapdata.Scan0);
                if (i >= 0)
                {
                    rec = _locks[i];
                    _locks.RemoveAt(i);
                }
            }

            if (rec == null)
            {
                return;
            }

            try
            {
                if ((rec.Mode & ImageLockMode.WriteOnly) != 0)
                {
                    WriteFromBuffer(CurrentBitmap, rec.Rect, bitmapdata);
                    InvalidateCache();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rec.Scan0);
                bitmapdata.Scan0 = IntPtr.Zero;
            }
        }

        private static unsafe void ReadToBuffer(SKBitmap bmp, Rectangle rect, BitmapData data)
        {
            int bpp = BytesPerPixel(data.PixelFormat);
            byte* dstBase = (byte*)data.Scan0;
            byte* srcBase = (byte*)bmp.GetPixels();
            int srcStride = bmp.RowBytes;
            for (int y = 0; y < rect.Height; y++)
            {
                byte* s = srcBase + ((rect.Y + y) * srcStride) + (rect.X * 4);
                byte* d = dstBase + (y * data.Stride);
                for (int x = 0; x < rect.Width; x++)
                {
                    byte b = s[0], g = s[1], r = s[2], a = s[3];
                    switch (data.PixelFormat)
                    {
                        case PixelFormat.Format32bppPArgb:
                            d[0] = b; d[1] = g; d[2] = r; d[3] = a;
                            break;
                        case PixelFormat.Format24bppRgb:
                            d[0] = b; d[1] = g; d[2] = r;
                            break;
                        case PixelFormat.Format32bppRgb:
                            d[0] = b; d[1] = g; d[2] = r; d[3] = 255;
                            break;
                        default:
                            if (a == 255 || a == 0)
                            {
                                d[0] = b; d[1] = g; d[2] = r; d[3] = a;
                            }
                            else
                            {
                                d[0] = Unpremul(b, a); d[1] = Unpremul(g, a); d[2] = Unpremul(r, a); d[3] = a;
                            }

                            break;
                    }

                    s += 4;
                    d += bpp;
                }
            }
        }

        private static unsafe void WriteFromBuffer(SKBitmap bmp, Rectangle rect, BitmapData data)
        {
            int bpp = BytesPerPixel(data.PixelFormat);
            byte* srcBase = (byte*)data.Scan0;
            byte* dstBase = (byte*)bmp.GetPixels();
            int dstStride = bmp.RowBytes;
            for (int y = 0; y < rect.Height; y++)
            {
                byte* s = srcBase + (y * data.Stride);
                byte* d = dstBase + ((rect.Y + y) * dstStride) + (rect.X * 4);
                for (int x = 0; x < rect.Width; x++)
                {
                    switch (data.PixelFormat)
                    {
                        case PixelFormat.Format32bppPArgb:
                            d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; d[3] = s[3];
                            break;
                        case PixelFormat.Format24bppRgb:
                        case PixelFormat.Format32bppRgb:
                            d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; d[3] = 255;
                            break;
                        default:
                            {
                                byte a = s[3];
                                d[0] = Premul(s[0], a); d[1] = Premul(s[1], a); d[2] = Premul(s[2], a); d[3] = a;
                                break;
                            }
                    }

                    s += bpp;
                    d += 4;
                }
            }
        }

        // ------------------------------------------------------------------ cloning etc.

        public override object Clone() => new Bitmap(this);

        public Bitmap Clone(Rectangle rect, PixelFormat format)
        {
            SKBitmap src = CurrentBitmap;
            if (rect.Width <= 0 || rect.Height <= 0 || rect.X < 0 || rect.Y < 0 || rect.Right > src.Width || rect.Bottom > src.Height)
            {
                throw new OutOfMemoryException("Out of memory.");
            }

            var result = new Bitmap(rect.Width, rect.Height, format);
            using var canvas = new SKCanvas(result.CurrentBitmap);
            using var img = SKImage.FromBitmap(src);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(img, new SKRect(rect.X, rect.Y, rect.Right, rect.Bottom), new SKRect(0, 0, rect.Width, rect.Height));
            result.HorizontalResolution = HorizontalResolution;
            result.VerticalResolution = VerticalResolution;
            return result;
        }

        public Bitmap Clone(RectangleF rect, PixelFormat format) => Clone(Rectangle.Round(rect), format);

        public IntPtr GetHbitmap() => IntPtr.Zero;

        public IntPtr GetHbitmap(Color background) => IntPtr.Zero;

        public IntPtr GetHicon() => IntPtr.Zero;

        protected override void Dispose(bool disposing)
        {
            lock (_locks)
            {
                foreach (LockRecord l in _locks)
                {
                    Marshal.FreeHGlobal(l.Scan0);
                }

                _locks.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
