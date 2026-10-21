using System.Collections.Generic;
using System.Drawing.Text;
using SkiaSharp;

namespace System.Drawing.Drawing2D
{
    /// <summary>GDI+ compatible vector path backed by an <see cref="SKPath"/>.</summary>
    public sealed class GraphicsPath : MarshalByRefObject, ICloneable, IDisposable
    {
        private SKPath _path;
        private bool _newFigure = true;
        private FillMode _fillMode;

        public GraphicsPath() : this(FillMode.Alternate)
        {
        }

        public GraphicsPath(FillMode fillMode)
        {
            _path = new SKPath();
            FillMode = fillMode;
        }

        public GraphicsPath(PointF[] pts, byte[] types) : this(pts, types, FillMode.Alternate)
        {
        }

        public GraphicsPath(PointF[] pts, byte[] types, FillMode fillMode) : this(fillMode)
        {
            // Types: low 3 bits = 0 start, 1 line, 3 bezier; 0x80 = close subpath.
            if (pts == null || types == null || pts.Length != types.Length)
            {
                throw new ArgumentException("pts and types must be non-null and the same length.");
            }

            for (int i = 0; i < pts.Length; i++)
            {
                switch (types[i] & 0x07)
                {
                    case 0:
                        _path.MoveTo(pts[i].ToSK());
                        break;
                    case 1:
                        _path.LineTo(pts[i].ToSK());
                        break;
                    case 3 when i + 2 < pts.Length:
                        _path.CubicTo(pts[i].ToSK(), pts[i + 1].ToSK(), pts[i + 2].ToSK());
                        i += 2;
                        break;
                }

                if ((types[i] & 0x80) != 0)
                {
                    _path.Close();
                }
            }
        }

        /// <summary>The underlying path (do not dispose).</summary>
        public SKPath SKPath => _path;

        public FillMode FillMode
        {
            get => _fillMode;
            set
            {
                _fillMode = value;
                _path.FillType = value == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;
            }
        }

        public int PointCount => _path.PointCount;

        public PointF[] PathPoints
        {
            get
            {
                SKPoint[] pts = _path.Points;
                var result = new PointF[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    result[i] = new PointF(pts[i].X, pts[i].Y);
                }

                return result;
            }
        }

        public object Clone()
        {
            var c = new GraphicsPath(FillMode);
            c._path.Dispose();
            c._path = new SKPath(_path);
            c._newFigure = _newFigure;
            return c;
        }

        public void Dispose()
        {
            _path?.Dispose();
            _path = null;
        }

        public void Reset()
        {
            _path.Reset();
            _path.FillType = _fillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;
            _newFigure = true;
        }

        public void StartFigure() => _newFigure = true;

        public void CloseFigure()
        {
            if (_path.PointCount > 0)
            {
                _path.Close();
            }

            _newFigure = true;
        }

        public void CloseAllFigures() => CloseFigure();

        public void SetMarkers()
        {
        }

        public void ClearMarkers()
        {
        }

        private void ConnectTo(PointF p)
        {
            if (_newFigure || _path.PointCount == 0)
            {
                _path.MoveTo(p.X, p.Y);
                _newFigure = false;
            }
            else
            {
                _path.LineTo(p.X, p.Y);
            }
        }

        public void AddLine(PointF pt1, PointF pt2)
        {
            ConnectTo(pt1);
            _path.LineTo(pt2.X, pt2.Y);
        }

        public void AddLine(float x1, float y1, float x2, float y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

        public void AddLine(Point pt1, Point pt2) => AddLine((PointF)pt1, (PointF)pt2);

        public void AddLine(int x1, int y1, int x2, int y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

        public void AddLines(PointF[] points)
        {
            if (points == null || points.Length == 0)
            {
                throw new ArgumentException("points");
            }

            ConnectTo(points[0]);
            for (int i = 1; i < points.Length; i++)
            {
                _path.LineTo(points[i].X, points[i].Y);
            }
        }

        public void AddLines(Point[] points) => AddLines(Array.ConvertAll(points, p => (PointF)p));

        public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            ConnectTo(pt1);
            _path.CubicTo(pt2.ToSK(), pt3.ToSK(), pt4.ToSK());
        }

        public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
            => AddBezier(new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

        public void AddRectangle(RectangleF rect)
        {
            _path.AddRect(rect.ToSK());
            _newFigure = true;
        }

        public void AddRectangle(Rectangle rect) => AddRectangle((RectangleF)rect);

        public void AddRectangles(RectangleF[] rects)
        {
            foreach (RectangleF r in rects)
            {
                AddRectangle(r);
            }
        }

        public void AddEllipse(RectangleF rect)
        {
            _path.AddOval(rect.ToSK());
            _newFigure = true;
        }

        public void AddEllipse(Rectangle rect) => AddEllipse((RectangleF)rect);

        public void AddEllipse(float x, float y, float width, float height) => AddEllipse(new RectangleF(x, y, width, height));

        public void AddEllipse(int x, int y, int width, int height) => AddEllipse(new RectangleF(x, y, width, height));

        public void AddArc(RectangleF rect, float startAngle, float sweepAngle)
        {
            if (_newFigure || _path.PointCount == 0)
            {
                _path.ArcTo(rect.ToSK(), startAngle, sweepAngle, true);
                _newFigure = false;
            }
            else
            {
                _path.ArcTo(rect.ToSK(), startAngle, sweepAngle, false);
            }
        }

        public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
            => AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);

        public void AddPie(RectangleF rect, float startAngle, float sweepAngle)
        {
            SKRect r = rect.ToSK();
            _path.MoveTo(r.MidX, r.MidY);
            _path.ArcTo(r, startAngle, sweepAngle, false);
            _path.Close();
            _newFigure = true;
        }

        public void AddPolygon(PointF[] points)
        {
            if (points == null || points.Length < 3)
            {
                throw new ArgumentException("A polygon needs at least 3 points.", nameof(points));
            }

            _path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                _path.LineTo(points[i].X, points[i].Y);
            }

            _path.Close();
            _newFigure = true;
        }

        public void AddPolygon(Point[] points) => AddPolygon(Array.ConvertAll(points, p => (PointF)p));

        public void AddPath(GraphicsPath addingPath, bool connect)
        {
            if (addingPath == null)
            {
                throw new ArgumentNullException(nameof(addingPath));
            }

            _path.AddPath(addingPath._path, connect ? SKPathAddMode.Extend : SKPathAddMode.Append);
            _newFigure = true;
        }

        // --- AddString ---

        public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat format)
            => AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);

        public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat format)
            => AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);

        public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat format)
            => AddString(s, family, style, emSize, (RectangleF)layoutRect, format);

        public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat format)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            if (family == null)
            {
                throw new ArgumentNullException(nameof(family));
            }

            var info = new TextEngine.FontInfo(family, (FontStyle)style, emSize);
            TextEngine.Layout layout = TextEngine.DoLayout(s, info, format, layoutRect, true);
            foreach (TextEngine.Line line in layout.Lines)
            {
                float x = line.X;
                foreach (TextEngine.Run run in line.Runs)
                {
                    using SKPath glyphs = run.Font.GetTextPath(run.Text, new SKPoint(x, line.Baseline));
                    _path.AddPath(glyphs);
                    x += run.Width;
                }

                TextEngine.AddDecorationPaths(_path, info, line, layout);
            }

            _newFigure = true;
        }

        // --- Queries / transforms ---

        public RectangleF GetBounds() => _path.IsEmpty ? RectangleF.Empty : _path.TightBounds.ToRectangleF();

        public RectangleF GetBounds(Matrix matrix) => GetBounds(matrix, null);

        public RectangleF GetBounds(Matrix matrix, Pen pen)
        {
            using var p = new SKPath(_path);
            if (pen != null)
            {
                using var paint = new SKPaint { Style = SKPaintStyle.Stroke };
                pen.ConfigureStroke(paint);
                paint.GetFillPath(_path, p);
            }

            if (matrix != null)
            {
                p.Transform(matrix.ToSK());
            }

            return p.IsEmpty ? RectangleF.Empty : p.TightBounds.ToRectangleF();
        }

        public void Transform(Matrix matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            _path.Transform(matrix.ToSK());
        }

        public bool IsVisible(float x, float y) => _path.Contains(x, y);

        public bool IsVisible(PointF point) => _path.Contains(point.X, point.Y);

        public bool IsVisible(int x, int y) => _path.Contains(x, y);

        public bool IsVisible(Point point) => _path.Contains(point.X, point.Y);

        public bool IsVisible(PointF point, Graphics g) => IsVisible(point);

        public bool IsVisible(float x, float y, Graphics g) => IsVisible(x, y);

        public bool IsOutlineVisible(float x, float y, Pen pen)
        {
            using var paint = new SKPaint { Style = SKPaintStyle.Stroke };
            pen.ConfigureStroke(paint);
            using var outline = new SKPath();
            paint.GetFillPath(_path, outline);
            return outline.Contains(x, y);
        }

        public bool IsOutlineVisible(PointF point, Pen pen) => IsOutlineVisible(point.X, point.Y, pen);

        public void Flatten()
        {
        }

        public void Flatten(Matrix matrix)
        {
            if (matrix != null)
            {
                Transform(matrix);
            }
        }

        public void Widen(Pen pen)
        {
            using var paint = new SKPaint { Style = SKPaintStyle.Stroke };
            pen.ConfigureStroke(paint);
            var outline = new SKPath();
            paint.GetFillPath(_path, outline);
            _path.Dispose();
            _path = outline;
        }
    }
}
