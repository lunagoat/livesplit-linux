using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Xunit;

namespace LiveSplit.Drawing.Tests
{
    public class RegionMatrixTests
    {
        private static readonly Bitmap Dummy = new Bitmap(1, 1);

        private static Graphics G() => Graphics.FromImage(Dummy);

        [Fact]
        public void Region_UnionBounds()
        {
            using var r = new Region(new RectangleF(0, 0, 10, 10));
            r.Union(new RectangleF(20, 5, 10, 10));
            using Graphics g = G();
            Assert.Equal(new RectangleF(0, 0, 30, 15), r.GetBounds(g));
            Assert.True(r.IsVisible(new PointF(5, 5)));
            Assert.True(r.IsVisible(new PointF(25, 10)));
            Assert.False(r.IsVisible(new PointF(15, 5)));
        }

        [Fact]
        public void Region_IntersectExcludeXorComplement()
        {
            using Graphics g = G();
            using var a = new Region(new RectangleF(0, 0, 10, 10));
            a.Intersect(new RectangleF(5, 5, 10, 10));
            Assert.Equal(new RectangleF(5, 5, 5, 5), a.GetBounds(g));

            using var b = new Region(new RectangleF(0, 0, 10, 10));
            b.Exclude(new RectangleF(0, 0, 5, 10));
            Assert.Equal(new RectangleF(5, 0, 5, 10), b.GetBounds(g));
            Assert.False(b.IsVisible(new PointF(2, 2)));

            using var c = new Region(new RectangleF(0, 0, 10, 10));
            c.Xor(new RectangleF(5, 0, 10, 10));
            Assert.False(c.IsVisible(new PointF(7, 5)));
            Assert.True(c.IsVisible(new PointF(2, 5)));
            Assert.True(c.IsVisible(new PointF(12, 5)));

            using var d = new Region(new RectangleF(0, 0, 10, 10));
            d.Complement(new RectangleF(0, 0, 20, 10));
            Assert.Equal(new RectangleF(10, 0, 10, 10), d.GetBounds(g));
        }

        [Fact]
        public void Region_InfiniteEmptyTranslateTransform()
        {
            using Graphics g = G();
            using var r = new Region();
            Assert.True(r.IsInfinite(g));
            Assert.False(r.IsEmpty(g));
            r.MakeEmpty();
            Assert.True(r.IsEmpty(g));
            Assert.False(r.IsVisible(new RectangleF(0, 0, 5, 5)));
            r.MakeInfinite();
            Assert.True(r.IsInfinite(g));
            Assert.True(r.IsVisible(new RectangleF(1000, 1000, 5, 5)));

            using var s = new Region(new RectangleF(0, 0, 10, 10));
            s.Translate(5, 7);
            Assert.Equal(new RectangleF(5, 7, 10, 10), s.GetBounds(g));
            using var m = new Matrix();
            m.Scale(2, 2);
            s.Transform(m);
            Assert.Equal(new RectangleF(10, 14, 20, 20), s.GetBounds(g));
            using Region clone = s.Clone();
            s.MakeEmpty();
            Assert.False(clone.IsEmpty(g));
            Assert.True(clone.Equals(clone.Clone(), g));
        }

        [Fact]
        public void Region_IsVisibleRectangle_UsesInteriorOverlap()
        {
            using var r = new Region(new RectangleF(0, 0, 10, 10));
            Assert.True(r.IsVisible(new RectangleF(9, 9, 5, 5)));
            Assert.False(r.IsVisible(new RectangleF(10, 0, 5, 5)));
            Assert.False(r.IsVisible(new RectangleF(20, 20, 5, 5)));
            using var l = new Region(new RectangleF(0, 0, 10, 10));
            l.Exclude(new RectangleF(2, 2, 6, 6));
            Assert.False(l.IsVisible(new RectangleF(3, 3, 2, 2)));
            Assert.True(l.IsVisible(new RectangleF(0, 0, 3, 3)));
        }

        [Fact]
        public void Region_FromPath_AndScans()
        {
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(0, 0, 4, 4));
            using var r = new Region(path);
            using var matrix = new Matrix();
            RectangleF[] scans = r.GetRegionScans(matrix);
            Assert.Equal(new[] { new RectangleF(0, 0, 4, 4) }, scans);
            r.Union(new RectangleF(10, 0, 4, 4));
            Assert.Equal(2, r.GetRegionScans(matrix).Length);
            Assert.Equal(new RectangleF(0, 0, 4, 4), new Region(new RectangleF(0, 0, 4, 4)).GetRegionScans(matrix)[0]);
        }

        [Fact]
        public void Matrix_InvertRoundTrips()
        {
            using var m = new Matrix(2, 0.5f, -0.25f, 3, 10, -4);
            using Matrix inv = m.Clone();
            inv.Invert();
            var pts = new[] { new PointF(3, 4), new PointF(-7, 2.5f) };
            var orig = (PointF[])pts.Clone();
            m.TransformPoints(pts);
            inv.TransformPoints(pts);
            Assert.Equal(orig[0].X, pts[0].X, 3);
            Assert.Equal(orig[0].Y, pts[0].Y, 3);
            Assert.Equal(orig[1].X, pts[1].X, 3);
            Assert.Equal(orig[1].Y, pts[1].Y, 3);
            Assert.True(m.IsInvertible);
            Assert.False(new Matrix(0, 0, 0, 0, 1, 1).IsInvertible);
        }

        [Fact]
        public void Matrix_PrependAppendOrderMatchesGdiPlus()
        {
            // Prepend: translate applied *after* the existing scale in point-transform order? GDI+: M' = T * M (T first).
            var prepend = new Matrix();
            prepend.Scale(2, 2);
            prepend.Translate(10, 0, MatrixOrder.Prepend); // translate in scaled space -> offset 20
            Assert.Equal(20f, prepend.OffsetX);

            var append = new Matrix();
            append.Scale(2, 2);
            append.Translate(10, 0, MatrixOrder.Append);    // translate in device space -> offset 10
            Assert.Equal(10f, append.OffsetX);

            var p = new[] { new PointF(1, 1) };
            prepend.TransformPoints(p);
            Assert.Equal(new PointF(22, 2), p[0]);
        }

        [Fact]
        public void Matrix_RotateAndVectors()
        {
            var m = new Matrix();
            m.Rotate(90);
            var p = new[] { new PointF(1, 0) };
            m.TransformPoints(p);
            Assert.Equal(0f, p[0].X, 4);
            Assert.Equal(1f, p[0].Y, 4); // clockwise in y-down space

            var t = new Matrix();
            t.Translate(5, 5);
            var v = new[] { new PointF(1, 1) };
            t.TransformVectors(v);
            Assert.Equal(new PointF(1, 1), v[0]);
            Assert.Equal(new float[] { 1, 0, 0, 1, 5, 5 }, t.Elements);
            Assert.True(new Matrix().IsIdentity);
            Assert.Equal(5f, t.OffsetX);
            t.Reset();
            Assert.True(t.IsIdentity);
        }

        [Fact]
        public void Matrix_Multiply()
        {
            var a = new Matrix();
            a.Translate(1, 2);
            var b = new Matrix();
            b.Scale(3, 3);
            a.Multiply(b); // prepend: b applied first
            var p = new[] { new PointF(1, 1) };
            a.TransformPoints(p);
            Assert.Equal(new PointF(4, 5), p[0]);

            var c = new Matrix();
            c.Translate(1, 2);
            c.Multiply(b, MatrixOrder.Append); // a applied first
            p = new[] { new PointF(1, 1) };
            c.TransformPoints(p);
            Assert.Equal(new PointF(6, 9), p[0]);
        }

        [Fact]
        public void GraphicsPath_BoundsAndHitTest()
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, 10, 10);
            path.AddRectangle(new RectangleF(20, 20, 5, 5));
            RectangleF b = path.GetBounds();
            Assert.Equal(0f, b.X, 2);
            Assert.Equal(25f, b.Right, 2);
            Assert.True(path.IsVisible(5, 5));
            Assert.False(path.IsVisible(1, 1));
            using var clone = (GraphicsPath)path.Clone();
            path.Reset();
            Assert.Equal(0, path.PointCount);
            Assert.True(clone.PointCount > 0);
        }
    }
}
