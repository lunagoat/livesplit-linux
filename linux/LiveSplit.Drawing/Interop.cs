using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Conversions between the System.Drawing shim types and SkiaSharp types.</summary>
    public static class SkiaConvert
    {
        public static SKColor ToSK(this Color c) => new SKColor(c.R, c.G, c.B, c.A);

        public static Color ToColor(this SKColor c) => Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);

        public static SKPoint ToSK(this PointF p) => new SKPoint(p.X, p.Y);

        public static SKRect ToSK(this RectangleF r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

        public static SKRect ToSK(this Rectangle r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);

        public static RectangleF ToRectangleF(this SKRect r) => new RectangleF(r.Left, r.Top, r.Width, r.Height);

        public static SKMatrix ToSK(this Matrix m)
        {
            float[] e = m.Elements;
            return new SKMatrix(e[0], e[2], e[4], e[1], e[3], e[5], 0, 0, 1);
        }

        public static Matrix ToMatrix(this SKMatrix m)
        {
            return new Matrix(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
        }
    }
}
