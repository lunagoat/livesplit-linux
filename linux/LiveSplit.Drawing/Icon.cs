using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Minimal Icon: decodes .ico data to a bitmap (the largest image).</summary>
    public sealed class Icon : MarshalByRefObject, ICloneable, IDisposable
    {
        private Bitmap _bitmap;

        public Icon(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _bitmap = new Bitmap(new MemoryStream(ms.ToArray()));
        }

        public Icon(Stream stream, int width, int height) : this(stream)
        {
            if (width > 0 && height > 0 && (width != _bitmap.Width || height != _bitmap.Height))
            {
                var scaled = new Bitmap(_bitmap, width, height);
                _bitmap.Dispose();
                _bitmap = scaled;
            }
        }

        public Icon(Stream stream, Size size) : this(stream, size.Width, size.Height)
        {
        }

        public Icon(string fileName)
        {
            _bitmap = new Bitmap(fileName);
        }

        public Icon(Icon original, Size size)
        {
            _bitmap = new Bitmap(original._bitmap, size);
        }

        public Icon(Icon original, int width, int height) : this(original, new Size(width, height))
        {
        }

        public IntPtr Handle => IntPtr.Zero;

        public int Width => _bitmap.Width;

        public int Height => _bitmap.Height;

        public Size Size => _bitmap.Size;

        public Bitmap ToBitmap() => new Bitmap(_bitmap);

        public void Save(Stream outputStream)
        {
            _bitmap.Save(outputStream, ImageFormat.Icon);
        }

        public object Clone() => new Icon(this, Size);

        public void Dispose()
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }

        public static Icon FromHandle(IntPtr handle) => null;

        public static Icon ExtractAssociatedIcon(string filePath) => null;
    }
}
