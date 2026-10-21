using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using SkiaSharp;

namespace System.Drawing
{
    public delegate bool GetThumbnailImageAbort();

    /// <summary>
    /// Base class of <see cref="Bitmap"/>. Pixels are held as premultiplied BGRA8888 SKBitmaps
    /// (one per frame for animated GIF/WebP).
    /// </summary>
    public abstract class Image : MarshalByRefObject, ICloneable, IDisposable
    {
        internal sealed class Frame
        {
            public SKBitmap Bitmap;
            public int DelayMs = 100;
        }

        internal List<Frame> Frames = new();
        private int _frameIndex;
        private int _width, _height;
        private SKImage _cachedImage;
        private int _cachedImageFrame = -1;
        private bool _disposed;

        internal Image()
        {
        }

        internal void SetFrames(List<Frame> frames)
        {
            Frames = frames;
            _frameIndex = 0;
            _width = frames[0].Bitmap.Width;
            _height = frames[0].Bitmap.Height;
            InvalidateCache();
        }

        public object Tag { get; set; }

        public int Width => _width;

        public int Height => _height;

        public Size Size => new Size(_width, _height);

        public SizeF PhysicalDimension => new SizeF(_width, _height);

        public float HorizontalResolution { get; protected set; } = 96f;

        public float VerticalResolution { get; protected set; } = 96f;

        public PixelFormat PixelFormat { get; protected set; } = PixelFormat.Format32bppArgb;

        public ImageFormat RawFormat { get; protected set; } = ImageFormat.MemoryBmp;

        public int Flags => (int)(ImageFlags.HasAlpha | ImageFlags.ColorSpaceRgb);

        public bool IsDisposed => _disposed;

        public Guid[] FrameDimensionsList => [FrameDimension.Time.Guid];

        public int GetFrameCount(FrameDimension dimension) => dimension.Guid == FrameDimension.Time.Guid || dimension.Guid == FrameDimension.Page.Guid ? Frames.Count : 1;

        public int SelectActiveFrame(FrameDimension dimension, int frameIndex)
        {
            if (frameIndex < 0 || frameIndex >= Frames.Count)
            {
                throw new ArgumentException("Parameter is not valid.", nameof(frameIndex));
            }

            _frameIndex = frameIndex;
            InvalidateCache();
            return 0;
        }

        internal int CurrentFrameIndex => _frameIndex;

        internal int FrameDelayMs(int index) => Frames[index].DelayMs;

        internal void SetCurrentFrameInternal(int index)
        {
            if (index >= 0 && index < Frames.Count && index != _frameIndex)
            {
                _frameIndex = index;
                InvalidateCache();
            }
        }

        /// <summary>The pixels of the active frame (premultiplied BGRA8888). Do not dispose.</summary>
        public SKBitmap ToSKBitmap()
        {
            ThrowIfDisposed();
            return Frames[_frameIndex].Bitmap;
        }

        internal SKBitmap CurrentBitmap => ToSKBitmap();

        /// <summary>An immutable SKImage snapshot of the active frame (cached until the pixels change). Do not dispose.</summary>
        public SKImage GetSKImage()
        {
            ThrowIfDisposed();
            lock (this)
            {
                if (_cachedImage == null || _cachedImageFrame != _frameIndex)
                {
                    _cachedImage?.Dispose();
                    _cachedImage = SKImage.FromBitmap(Frames[_frameIndex].Bitmap);
                    _cachedImageFrame = _frameIndex;
                }

                return _cachedImage;
            }
        }

        /// <summary>Must be called after the pixels were modified so cached images are rebuilt.</summary>
        internal void InvalidateCache()
        {
            lock (this)
            {
                _cachedImage?.Dispose();
                _cachedImage = null;
                _cachedImageFrame = -1;
            }
        }

        public void Touch() => InvalidateCache();

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ArgumentException("Parameter is not valid.");
            }
        }

        // ------------------------------------------------------------------ loading

        public static Image FromFile(string filename) => FromFile(filename, false);

        public static Image FromFile(string filename, bool useEmbeddedColorManagement)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("File not found.", filename);
            }

            return FromBytes(File.ReadAllBytes(filename));
        }

        public static Image FromStream(Stream stream) => FromStream(stream, false, true);

        public static Image FromStream(Stream stream, bool useEmbeddedColorManagement) => FromStream(stream, useEmbeddedColorManagement, true);

        public static Image FromStream(Stream stream, bool useEmbeddedColorManagement, bool validateImageData)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return FromBytes(ms.ToArray());
        }

        /// <summary>Decodes PNG/JPEG/GIF/BMP/ICO/WebP/... Throws ArgumentException for undecodable data like GDI+.</summary>
        public static Bitmap FromBytes(byte[] data)
        {
            return new Bitmap(data, true);
        }

        public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

        public static bool IsAlphaPixelFormat(PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Alpha) != 0;

        public static bool IsCanonicalPixelFormat(PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Canonical) != 0;

        public static bool IsExtendedPixelFormat(PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Extended) != 0;

        // ------------------------------------------------------------------ saving

        public void Save(string filename) => Save(filename, ImageFormat.FromExtension(filename));

        public void Save(string filename, ImageFormat format)
        {
            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }

            using FileStream fs = File.Create(filename);
            Save(fs, format);
        }

        public void Save(Stream stream, ImageFormat format)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            ThrowIfDisposed();
            byte[] bytes = ImageEncoder.Encode(CurrentBitmap, format ?? ImageFormat.Png);
            stream.Write(bytes, 0, bytes.Length);
        }

        // ------------------------------------------------------------------ misc

        public abstract object Clone();

        public Image GetThumbnailImage(int thumbWidth, int thumbHeight, GetThumbnailImageAbort callback, IntPtr callbackData)
        {
            return new Bitmap(this, thumbWidth, thumbHeight);
        }

        public void RotateFlip(RotateFlipType rotateFlipType)
        {
            ThrowIfDisposed();
            int v = (int)rotateFlipType;
            int rot = v & 3;
            bool flipX = (v & 4) != 0;
            var newFrames = new List<Frame>();
            foreach (Frame f in Frames)
            {
                SKBitmap src = f.Bitmap;
                bool swap = rot % 2 == 1;
                int w = swap ? src.Height : src.Width;
                int h = swap ? src.Width : src.Height;
                var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (var canvas = new SKCanvas(dst))
                {
                    canvas.Clear(SKColors.Transparent);
                    // Rotate clockwise by rot*90 about the destination centre, then flip horizontally if requested.
                    canvas.Translate(w / 2f, h / 2f);
                    if (flipX)
                    {
                        canvas.Scale(-1, 1);
                    }

                    canvas.RotateDegrees(rot * 90);
                    canvas.Translate(-src.Width / 2f, -src.Height / 2f);
                    using var img = SKImage.FromBitmap(src);
                    canvas.DrawImage(img, 0, 0);
                }

                newFrames.Add(new Frame { Bitmap = dst, DelayMs = f.DelayMs });
                src.Dispose();
            }

            int idx = _frameIndex;
            SetFrames(newFrames);
            _frameIndex = Math.Min(idx, Frames.Count - 1);
            InvalidateCache();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ImageAnimator.StopAnimate(this);
            if (disposing)
            {
                InvalidateCache();
                foreach (Frame f in Frames)
                {
                    f.Bitmap?.Dispose();
                }
            }
        }

        ~Image()
        {
            Dispose(false);
        }
    }
}
