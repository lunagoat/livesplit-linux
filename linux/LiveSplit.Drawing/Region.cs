using System.Collections.Generic;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>
    /// GDI+ compatible region backed by an <see cref="SKPath"/> (floating point, boolean ops through SKPath.Op).
    /// An infinite region is represented as the same huge rectangle GDI+ uses.
    /// </summary>
    public sealed class Region : MarshalByRefObject, IDisposable
    {
        private const float InfiniteMin = -4194304f;
        private const float InfiniteSize = 8388608f;

        private SKPath _path;

        public Region()
        {
            _path = CreateInfinite();
        }

        public Region(RectangleF rect)
        {
            _path = FromRect(rect);
        }

        public Region(Rectangle rect)
        {
            _path = FromRect(rect);
        }

        public Region(GraphicsPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _path = new SKPath(path.SKPath);
        }

        internal Region(SKPath ownedPath)
        {
            _path = ownedPath;
        }

        private static SKPath CreateInfinite()
        {
            var p = new SKPath();
            p.AddRect(new SKRect(InfiniteMin, InfiniteMin, InfiniteMin + InfiniteSize, InfiniteMin + InfiniteSize));
            return p;
        }

        private static SKPath FromRect(RectangleF r)
        {
            var p = new SKPath();
            if (r.Width > 0 && r.Height > 0)
            {
                p.AddRect(r.ToSK());
            }

            return p;
        }

        /// <summary>The region as an SKPath (do not dispose or mutate; copy it first).</summary>
        public SKPath SKPath => _path;

        public Region Clone() => new Region(new SKPath(_path));

        public void Dispose()
        {
            _path?.Dispose();
            _path = null;
        }

        public void MakeInfinite()
        {
            _path.Dispose();
            _path = CreateInfinite();
        }

        public void MakeEmpty()
        {
            _path.Dispose();
            _path = new SKPath();
        }

        private void Combine(SKPath other, SKPathOp op)
        {
            SKPath result = _path.Op(other, op);
            _path.Dispose();
            _path = result ?? new SKPath();
        }

        private void CombineRect(RectangleF rect, SKPathOp op)
        {
            using SKPath p = FromRect(rect);
            Combine(p, op);
        }

        public void Intersect(RectangleF rect) => CombineRect(rect, SKPathOp.Intersect);

        public void Intersect(Rectangle rect) => CombineRect(rect, SKPathOp.Intersect);

        public void Intersect(GraphicsPath path) => Combine(path.SKPath, SKPathOp.Intersect);

        public void Intersect(Region region) => Combine(region._path, SKPathOp.Intersect);

        public void Union(RectangleF rect) => CombineRect(rect, SKPathOp.Union);

        public void Union(Rectangle rect) => CombineRect(rect, SKPathOp.Union);

        public void Union(GraphicsPath path) => Combine(path.SKPath, SKPathOp.Union);

        public void Union(Region region) => Combine(region._path, SKPathOp.Union);

        public void Xor(RectangleF rect) => CombineRect(rect, SKPathOp.Xor);

        public void Xor(Rectangle rect) => CombineRect(rect, SKPathOp.Xor);

        public void Xor(GraphicsPath path) => Combine(path.SKPath, SKPathOp.Xor);

        public void Xor(Region region) => Combine(region._path, SKPathOp.Xor);

        public void Exclude(RectangleF rect) => CombineRect(rect, SKPathOp.Difference);

        public void Exclude(Rectangle rect) => CombineRect(rect, SKPathOp.Difference);

        public void Exclude(GraphicsPath path) => Combine(path.SKPath, SKPathOp.Difference);

        public void Exclude(Region region) => Combine(region._path, SKPathOp.Difference);

        public void Complement(RectangleF rect) => CombineRect(rect, SKPathOp.ReverseDifference);

        public void Complement(Rectangle rect) => CombineRect(rect, SKPathOp.ReverseDifference);

        public void Complement(GraphicsPath path) => Combine(path.SKPath, SKPathOp.ReverseDifference);

        public void Complement(Region region) => Combine(region._path, SKPathOp.ReverseDifference);

        public void Translate(float dx, float dy) => _path.Transform(SKMatrix.CreateTranslation(dx, dy));

        public void Translate(int dx, int dy) => Translate((float)dx, (float)dy);

        public void Transform(Matrix matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            _path.Transform(matrix.ToSK());
        }

        public RectangleF GetBounds(Graphics g)
        {
            return _path.IsEmpty ? RectangleF.Empty : _path.Bounds.ToRectangleF();
        }

        public bool IsEmpty(Graphics g)
        {
            if (_path.IsEmpty)
            {
                return true;
            }

            SKRect b = _path.Bounds;
            return b.Width <= 0 || b.Height <= 0;
        }

        public bool IsInfinite(Graphics g)
        {
            if (!_path.IsRect)
            {
                return false;
            }

            SKRect b = _path.Bounds;
            // Tolerant: transforming the huge rectangle (e.g. by a non-integer scale) must still read as infinite.
            return b.Left <= -2000000f && b.Top <= -2000000f && b.Right >= 2000000f && b.Bottom >= 2000000f;
        }

        public bool Equals(Region region, Graphics g)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            using SKPath diff = _path.Op(region._path, SKPathOp.Xor);
            return diff == null || diff.IsEmpty || diff.Bounds.Width <= 0 || diff.Bounds.Height <= 0;
        }

        public bool IsVisible(float x, float y) => _path.Contains(x, y);

        public bool IsVisible(float x, float y, Graphics g) => IsVisible(x, y);

        public bool IsVisible(PointF point) => IsVisible(point.X, point.Y);

        public bool IsVisible(PointF point, Graphics g) => IsVisible(point.X, point.Y);

        public bool IsVisible(int x, int y) => IsVisible((float)x, (float)y);

        public bool IsVisible(int x, int y, Graphics g) => IsVisible((float)x, (float)y);

        public bool IsVisible(Point point) => IsVisible((float)point.X, (float)point.Y);

        public bool IsVisible(Point point, Graphics g) => IsVisible((float)point.X, (float)point.Y);

        public bool IsVisible(float x, float y, float width, float height) => IsVisible(new RectangleF(x, y, width, height));

        public bool IsVisible(float x, float y, float width, float height, Graphics g) => IsVisible(new RectangleF(x, y, width, height));

        public bool IsVisible(int x, int y, int width, int height) => IsVisible(new RectangleF(x, y, width, height));

        public bool IsVisible(int x, int y, int width, int height, Graphics g) => IsVisible(new RectangleF(x, y, width, height));

        public bool IsVisible(Rectangle rect) => IsVisible((RectangleF)rect);

        public bool IsVisible(Rectangle rect, Graphics g) => IsVisible((RectangleF)rect);

        public bool IsVisible(RectangleF rect, Graphics g) => IsVisible(rect);

        public bool IsVisible(RectangleF rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || _path.IsEmpty)
            {
                return false;
            }

            SKRect r = rect.ToSK();
            if (!_path.Bounds.IntersectsWith(r))
            {
                return false;
            }

            if (_path.IsRect)
            {
                SKRect b = _path.Bounds;
                return Math.Min(b.Right, r.Right) > Math.Max(b.Left, r.Left)
                    && Math.Min(b.Bottom, r.Bottom) > Math.Max(b.Top, r.Top);
            }

            using var rp = new SKPath();
            rp.AddRect(r);
            using SKPath inter = _path.Op(rp, SKPathOp.Intersect);
            return inter != null && !inter.IsEmpty && inter.Bounds.Width > 0 && inter.Bounds.Height > 0;
        }

        /// <summary>
        /// The rectangles that make up the region after applying <paramref name="matrix"/>
        /// (rectangular regions are exact; other shapes are scanned at integer precision).
        /// </summary>
        public RectangleF[] GetRegionScans(Matrix matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            if (_path.IsEmpty)
            {
                return [];
            }

            SKMatrix m = matrix.ToSK();
            if (_path.IsRect && m.SkewX == 0 && m.SkewY == 0)
            {
                SKRect b = m.MapRect(_path.Bounds);
                return [b.ToRectangleF()];
            }

            using var tp = new SKPath(_path);
            tp.Transform(m);
            SKRectI bounds = SKRectI.Ceiling(tp.Bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return [];
            }

            // Guard against scanning the infinite region.
            bounds = SKRectI.Intersect(bounds, new SKRectI(-100000, -100000, 100000, 100000));
            using var clip = new SKRegion(bounds);
            using var region = new SKRegion();
            region.SetPath(tp, clip);
            var result = new List<RectangleF>();
            using SKRegion.RectIterator it = region.CreateRectIterator();
            while (it.Next(out SKRectI r))
            {
                result.Add(new RectangleF(r.Left, r.Top, r.Width, r.Height));
            }

            return result.ToArray();
        }

        public static Region FromHrgn(IntPtr hrgn) => new Region();

        public IntPtr GetHrgn(Graphics g) => IntPtr.Zero;

        public void ReleaseHrgn(IntPtr regionHandle)
        {
        }
    }
}
