using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using SkiaSharp;

namespace System.Drawing
{
    internal static class GraphicsHelper
    {
        /// <param name="shrink">source pixels per destination pixel (&gt;1 when minifying).</param>
        public static SKSamplingOptions SamplingFor(InterpolationMode mode, float shrink)
        {
            switch (mode)
            {
                case InterpolationMode.NearestNeighbor:
                    return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
                case InterpolationMode.HighQualityBicubic:
                case InterpolationMode.Bicubic:
                    return shrink > 1.5f
                        ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                        : new SKSamplingOptions(SKCubicResampler.Mitchell);
                case InterpolationMode.HighQualityBilinear:
                case InterpolationMode.High:
                    return shrink > 1.5f
                        ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
                        : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
                default:
                    return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            }
        }
    }

    public sealed class GraphicsState : MarshalByRefObject
    {
        internal Matrix Transform;
        internal SKPath Clip; // device space; null = infinite
        internal SmoothingMode SmoothingMode;
        internal TextRenderingHint TextRenderingHint;
        internal InterpolationMode InterpolationMode;
        internal CompositingMode CompositingMode;
        internal CompositingQuality CompositingQuality;
        internal PixelOffsetMode PixelOffsetMode;
        internal int TextContrast;

        internal GraphicsState()
        {
        }
    }

    /// <summary>
    /// GDI+ compatible drawing surface on top of an <see cref="SKCanvas"/>. The GDI+ conventions are kept:
    /// brush/pen coordinates are world coordinates, the clip is stored in device space and the
    /// <see cref="Clip"/> property reads/writes it in world space.
    /// </summary>
    public sealed class Graphics : MarshalByRefObject, IDisposable
    {
        private SKCanvas _canvas;
        private readonly bool _ownsCanvas;
        private readonly Image _targetImage;
        private readonly int _baseSave;
        private readonly SKMatrix _baseMatrix;
        private Matrix _transform = new Matrix();
        private SKPath _clip; // device space relative to base; null = infinite
        private readonly List<GraphicsState> _states = new();
        private bool _disposed;

        // ------------------------------------------------------------------ construction

        private Graphics(SKCanvas canvas, bool ownsCanvas, Image target)
        {
            _canvas = canvas;
            _ownsCanvas = ownsCanvas;
            _targetImage = target;
            _baseMatrix = canvas.TotalMatrix;
            _baseSave = canvas.Save();
        }

        /// <summary>Creates a Graphics that draws into the bitmap's pixels.</summary>
        public static Graphics FromImage(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (image.IsDisposed)
            {
                throw new ArgumentException("Parameter is not valid.", nameof(image));
            }

            var canvas = new SKCanvas(image.ToSKBitmap());
            image.InvalidateCache();
            return new Graphics(canvas, true, image);
        }

        /// <summary>Creates a Graphics over an existing canvas (e.g. an Avalonia render target). The current canvas matrix/clip become the "device" space.</summary>
        public static Graphics FromSKCanvas(SKCanvas canvas, bool ownsCanvas = false)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            return new Graphics(canvas, ownsCanvas, null);
        }

        /// <summary>Creates a Graphics that draws onto an SKSurface.</summary>
        public static Graphics FromSKSurface(SKSurface surface)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            return new Graphics(surface.Canvas, false, null);
        }

        /// <summary>The underlying canvas (for mixing in direct Skia drawing).</summary>
        public SKCanvas SKCanvas => _canvas;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _targetImage?.InvalidateCache();
            try
            {
                _canvas.RestoreToCount(_baseSave);
            }
            catch
            {
            }

            _clip?.Dispose();
            _clip = null;
            if (_ownsCanvas)
            {
                _canvas.Dispose();
            }

            _canvas = null;
        }

        // ------------------------------------------------------------------ simple properties

        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;

        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;

        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Bilinear;

        public CompositingMode CompositingMode { get; set; } = CompositingMode.SourceOver;

        public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;

        public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;

        public int TextContrast { get; set; } = 4;

        public float DpiX => 96f;

        public float DpiY => 96f;

        public Point RenderingOrigin { get; set; }

        public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Pixel;

        public float PageScale { get; set; } = 1f;

        private bool AntiAlias => SmoothingMode is SmoothingMode.AntiAlias or SmoothingMode.HighQuality;

        // ------------------------------------------------------------------ transform

        public Matrix Transform
        {
            get => _transform.Clone();
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _transform = value.Clone();
            }
        }

        public void ResetTransform() => _transform = new Matrix();

        public void MultiplyTransform(Matrix matrix) => _transform.Multiply(matrix, MatrixOrder.Prepend);

        public void MultiplyTransform(Matrix matrix, MatrixOrder order) => _transform.Multiply(matrix, order);

        public void TranslateTransform(float dx, float dy) => _transform.Translate(dx, dy, MatrixOrder.Prepend);

        public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform.Translate(dx, dy, order);

        public void ScaleTransform(float sx, float sy) => _transform.Scale(sx, sy, MatrixOrder.Prepend);

        public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform.Scale(sx, sy, order);

        public void RotateTransform(float angle) => _transform.Rotate(angle, MatrixOrder.Prepend);

        public void RotateTransform(float angle, MatrixOrder order) => _transform.Rotate(angle, order);

        public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, PointF[] pts)
        {
            if (pts == null)
            {
                throw new ArgumentNullException(nameof(pts));
            }

            bool srcWorld = srcSpace == CoordinateSpace.World;
            bool dstWorld = destSpace == CoordinateSpace.World;
            if (srcWorld == dstWorld)
            {
                return;
            }

            if (srcWorld)
            {
                _transform.TransformPoints(pts);
            }
            else
            {
                using Matrix inv = _transform.Clone();
                inv.Invert();
                inv.TransformPoints(pts);
            }
        }

        public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, Point[] pts)
        {
            var f = Array.ConvertAll(pts, p => (PointF)p);
            TransformPoints(destSpace, srcSpace, f);
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = Point.Round(f[i]);
            }
        }

        // ------------------------------------------------------------------ clipping

        private SKPath WorldToDevice(SKPath worldPath)
        {
            var p = new SKPath(worldPath);
            p.Transform(_transform.ToSK());
            return p;
        }

        private void SetClipDevice(SKPath devicePath)
        {
            _clip?.Dispose();
            _clip = devicePath;
            if (_clip != null && IsInfinitePath(_clip))
            {
                _clip.Dispose();
                _clip = null;
            }
        }

        private static bool IsInfinitePath(SKPath p)
        {
            if (!p.IsRect)
            {
                return false;
            }

            SKRect b = p.Bounds;
            return b.Left <= -2000000f && b.Top <= -2000000f && b.Right >= 2000000f && b.Bottom >= 2000000f;
        }

        private void CombineClip(SKPath worldPath, CombineMode mode)
        {
            using SKPath devicePath = WorldToDevice(worldPath);
            if (mode == CombineMode.Replace || (_clip == null && mode == CombineMode.Intersect))
            {
                SetClipDevice(new SKPath(devicePath));
                return;
            }

            using var infinite = new Region();
            SKPath existing = _clip ?? infinite.SKPath;
            SKPathOp op = mode switch
            {
                CombineMode.Intersect => SKPathOp.Intersect,
                CombineMode.Union => SKPathOp.Union,
                CombineMode.Xor => SKPathOp.Xor,
                CombineMode.Exclude => SKPathOp.Difference,
                _ => SKPathOp.ReverseDifference,
            };
            SKPath result = existing.Op(devicePath, op) ?? new SKPath();
            SetClipDevice(result);
        }

        private static SKPath RectPath(RectangleF r)
        {
            var p = new SKPath();
            if (r.Width > 0 && r.Height > 0)
            {
                p.AddRect(r.ToSK());
            }

            return p;
        }

        public Region Clip
        {
            get
            {
                if (_clip == null)
                {
                    return new Region();
                }

                var p = new SKPath(_clip);
                if (_transform.IsInvertible)
                {
                    using Matrix inv = _transform.Clone();
                    inv.Invert();
                    p.Transform(inv.ToSK());
                }

                return new Region(p);
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                CombineClip(value.SKPath, CombineMode.Replace);
            }
        }

        public void SetClip(Graphics g) => SetClip(g, CombineMode.Replace);

        public void SetClip(Graphics g, CombineMode combineMode)
        {
            Region r = g.Clip;
            SetClip(r, combineMode);
        }

        public void SetClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Replace);

        public void SetClip(Rectangle rect, CombineMode combineMode) => SetClip((RectangleF)rect, combineMode);

        public void SetClip(RectangleF rect) => SetClip(rect, CombineMode.Replace);

        public void SetClip(RectangleF rect, CombineMode combineMode)
        {
            using SKPath p = RectPath(rect);
            CombineClip(p, combineMode);
        }

        public void SetClip(GraphicsPath path) => SetClip(path, CombineMode.Replace);

        public void SetClip(GraphicsPath path, CombineMode combineMode) => CombineClip(path.SKPath, combineMode);

        public void SetClip(Region region, CombineMode combineMode) => CombineClip(region.SKPath, combineMode);

        public void IntersectClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Intersect);

        public void IntersectClip(RectangleF rect) => SetClip(rect, CombineMode.Intersect);

        public void IntersectClip(Region region) => SetClip(region, CombineMode.Intersect);

        public void ExcludeClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Exclude);

        public void ExcludeClip(Region region) => SetClip(region, CombineMode.Exclude);

        public void ResetClip() => SetClipDevice(null);

        public void TranslateClip(float dx, float dy)
        {
            if (_clip != null)
            {
                var p = new SKPath(_clip);
                p.Transform(SKMatrix.CreateTranslation(dx, dy));
                SetClipDevice(p);
            }
        }

        public void TranslateClip(int dx, int dy) => TranslateClip((float)dx, (float)dy);

        public RectangleF ClipBounds
        {
            get
            {
                using Region r = Clip;
                return r.GetBounds(this);
            }
        }

        public RectangleF VisibleClipBounds => ClipBounds;

        public bool IsClipEmpty
        {
            get
            {
                using Region r = Clip;
                return r.IsEmpty(this);
            }
        }

        public bool IsVisibleClipEmpty => IsClipEmpty;

        public bool IsVisible(RectangleF rect)
        {
            using Region r = Clip;
            return r.IsVisible(rect);
        }

        public bool IsVisible(Rectangle rect) => IsVisible((RectangleF)rect);

        public bool IsVisible(float x, float y)
        {
            using Region r = Clip;
            return r.IsVisible(x, y);
        }

        public bool IsVisible(PointF point) => IsVisible(point.X, point.Y);

        public bool IsVisible(int x, int y) => IsVisible((float)x, (float)y);

        public bool IsVisible(Point point) => IsVisible((float)point.X, (float)point.Y);

        public bool IsVisible(float x, float y, float width, float height) => IsVisible(new RectangleF(x, y, width, height));

        public bool IsVisible(int x, int y, int width, int height) => IsVisible(new RectangleF(x, y, width, height));

        // ------------------------------------------------------------------ save / restore

        public GraphicsState Save()
        {
            var s = new GraphicsState
            {
                Transform = _transform.Clone(),
                Clip = _clip == null ? null : new SKPath(_clip),
                SmoothingMode = SmoothingMode,
                TextRenderingHint = TextRenderingHint,
                InterpolationMode = InterpolationMode,
                CompositingMode = CompositingMode,
                CompositingQuality = CompositingQuality,
                PixelOffsetMode = PixelOffsetMode,
                TextContrast = TextContrast,
            };
            _states.Add(s);
            return s;
        }

        public void Restore(GraphicsState gstate)
        {
            if (gstate == null)
            {
                throw new ArgumentNullException(nameof(gstate));
            }

            int i = _states.IndexOf(gstate);
            if (i < 0)
            {
                return;
            }

            _states.RemoveRange(i, _states.Count - i);
            _transform = gstate.Transform.Clone();
            SetClipDevice(gstate.Clip == null ? null : new SKPath(gstate.Clip));
            SmoothingMode = gstate.SmoothingMode;
            TextRenderingHint = gstate.TextRenderingHint;
            InterpolationMode = gstate.InterpolationMode;
            CompositingMode = gstate.CompositingMode;
            CompositingQuality = gstate.CompositingQuality;
            PixelOffsetMode = gstate.PixelOffsetMode;
            TextContrast = gstate.TextContrast;
        }

        // ------------------------------------------------------------------ canvas sync

        /// <summary>Pushes the GDI+ clip/transform state onto the Skia canvas. Returns false when nothing can be drawn.</summary>
        private bool Prepare()
        {
            if (_disposed || _canvas == null)
            {
                return false;
            }

            _canvas.RestoreToCount(_baseSave);
            _canvas.Save();
            _canvas.SetMatrix(_baseMatrix);
            if (_clip != null)
            {
                if (_clip.IsEmpty)
                {
                    return false;
                }

                if (_clip.IsRect)
                {
                    _canvas.ClipRect(_clip.Bounds, SKClipOperation.Intersect, false);
                }
                else
                {
                    _canvas.ClipPath(_clip, SKClipOperation.Intersect, false);
                }
            }

            _canvas.SetMatrix(SKMatrix.Concat(_baseMatrix, _transform.ToSK()));
            _targetImage?.InvalidateCache();
            return true;
        }

        private SKPaint FillPaint(Brush brush)
        {
            var paint = new SKPaint { IsAntialias = AntiAlias, Style = SKPaintStyle.Fill };
            brush.ConfigurePaint(paint);
            if (CompositingMode == CompositingMode.SourceCopy)
            {
                paint.BlendMode = SKBlendMode.Src;
            }

            return paint;
        }

        private SKPaint StrokePaint(Pen pen)
        {
            var paint = new SKPaint { IsAntialias = AntiAlias };
            pen.ConfigurePaint(paint);
            if (CompositingMode == CompositingMode.SourceCopy)
            {
                paint.BlendMode = SKBlendMode.Src;
            }

            return paint;
        }

        // ------------------------------------------------------------------ clear / flush

        public void Clear(Color color)
        {
            if (!Prepare())
            {
                return;
            }

            _canvas.Clear(color.ToSK());
        }

        public void Flush()
        {
            _canvas?.Flush();
        }

        public void Flush(FlushIntention intention) => Flush();

        // ------------------------------------------------------------------ fills

        public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, (float)x, y, width, height);

        public void FillRectangle(Brush brush, float x, float y, float width, float height)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = FillPaint(brush);
            _canvas.DrawRect(new SKRect(x, y, x + width, y + height), paint);
        }

        public void FillRectangles(Brush brush, RectangleF[] rects)
        {
            foreach (RectangleF r in rects)
            {
                FillRectangle(brush, r);
            }
        }

        public void FillRectangles(Brush brush, Rectangle[] rects)
        {
            foreach (Rectangle r in rects)
            {
                FillRectangle(brush, r);
            }
        }

        public void FillEllipse(Brush brush, RectangleF rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillEllipse(Brush brush, int x, int y, int width, int height) => FillEllipse(brush, (float)x, y, width, height);

        public void FillEllipse(Brush brush, float x, float y, float width, float height)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = FillPaint(brush);
            _canvas.DrawOval(new SKRect(x, y, x + width, y + height), paint);
        }

        public void FillPie(Brush brush, RectangleF rect, float startAngle, float sweepAngle)
        {
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddPie(rect, startAngle, sweepAngle);
            FillPath(brush, path);
        }

        public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => FillPie(brush, new RectangleF(x, y, width, height), startAngle, sweepAngle);

        public void FillPolygon(Brush brush, PointF[] points) => FillPolygon(brush, points, FillMode.Alternate);

        public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, points, FillMode.Alternate);

        public void FillPolygon(Brush brush, Point[] points, FillMode fillMode) => FillPolygon(brush, Array.ConvertAll(points, p => (PointF)p), fillMode);

        public void FillPolygon(Brush brush, PointF[] points, FillMode fillMode)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Length < 2 || !Prepare())
            {
                return;
            }

            using var path = new SKPath { FillType = fillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd };
            path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i].X, points[i].Y);
            }

            path.Close();
            using SKPaint paint = FillPaint(brush);
            _canvas.DrawPath(path, paint);
        }

        public void FillPath(Brush brush, GraphicsPath path)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = FillPaint(brush);
            _canvas.DrawPath(path.SKPath, paint);
        }

        public void FillRegion(Brush brush, Region region)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = FillPaint(brush);
            _canvas.DrawPath(region.SKPath, paint);
        }

        // ------------------------------------------------------------------ strokes

        public void DrawLine(Pen pen, PointF pt1, PointF pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

        public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, (float)pt1.X, pt1.Y, pt2.X, pt2.Y);

        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, (float)x1, y1, x2, y2);

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            if (pen == null)
            {
                throw new ArgumentNullException(nameof(pen));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = StrokePaint(pen);
            _canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        public void DrawLines(Pen pen, PointF[] points)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            using var path = new SKPath();
            path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i].X, points[i].Y);
            }

            DrawSkPath(pen, path);
        }

        public void DrawLines(Pen pen, Point[] points) => DrawLines(pen, Array.ConvertAll(points, p => (PointF)p));

        public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawRectangle(Pen pen, RectangleF rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, (float)x, y, width, height);

        public void DrawRectangle(Pen pen, float x, float y, float width, float height)
        {
            if (pen == null)
            {
                throw new ArgumentNullException(nameof(pen));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = StrokePaint(pen);
            _canvas.DrawRect(new SKRect(x, y, x + width, y + height), paint);
        }

        public void DrawRectangles(Pen pen, RectangleF[] rects)
        {
            foreach (RectangleF r in rects)
            {
                DrawRectangle(pen, r);
            }
        }

        public void DrawEllipse(Pen pen, RectangleF rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawEllipse(Pen pen, Rectangle rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawEllipse(Pen pen, int x, int y, int width, int height) => DrawEllipse(pen, (float)x, y, width, height);

        public void DrawEllipse(Pen pen, float x, float y, float width, float height)
        {
            if (pen == null)
            {
                throw new ArgumentNullException(nameof(pen));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = StrokePaint(pen);
            _canvas.DrawOval(new SKRect(x, y, x + width, y + height), paint);
        }

        public void DrawArc(Pen pen, RectangleF rect, float startAngle, float sweepAngle)
        {
            using var path = new GraphicsPath();
            path.AddArc(rect, startAngle, sweepAngle);
            DrawPath(pen, path);
        }

        public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => DrawArc(pen, new RectangleF(x, y, width, height), startAngle, sweepAngle);

        public void DrawBezier(Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            using var path = new SKPath();
            path.MoveTo(pt1.ToSK());
            path.CubicTo(pt2.ToSK(), pt3.ToSK(), pt4.ToSK());
            DrawSkPath(pen, path);
        }

        public void DrawPolygon(Pen pen, PointF[] points)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            using var path = new SKPath();
            path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i].X, points[i].Y);
            }

            path.Close();
            DrawSkPath(pen, path);
        }

        public void DrawPolygon(Pen pen, Point[] points) => DrawPolygon(pen, Array.ConvertAll(points, p => (PointF)p));

        public void DrawPath(Pen pen, GraphicsPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            DrawSkPath(pen, path.SKPath);
        }

        private void DrawSkPath(Pen pen, SKPath path)
        {
            if (pen == null)
            {
                throw new ArgumentNullException(nameof(pen));
            }

            if (!Prepare())
            {
                return;
            }

            using SKPaint paint = StrokePaint(pen);
            _canvas.DrawPath(path, paint);
        }

        // ------------------------------------------------------------------ images

        public void DrawImage(Image image, PointF point) => DrawImage(image, point.X, point.Y);

        public void DrawImage(Image image, Point point) => DrawImage(image, (float)point.X, point.Y);

        public void DrawImage(Image image, int x, int y) => DrawImage(image, (float)x, y);

        public void DrawImage(Image image, float x, float y)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            float w = image.Width * DpiX / image.HorizontalResolution;
            float h = image.Height * DpiY / image.VerticalResolution;
            DrawImage(image, new RectangleF(x, y, w, h), new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, null);
        }

        public void DrawImageUnscaled(Image image, int x, int y) => DrawImage(image, new RectangleF(x, y, image.Width, image.Height), new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, null);

        public void DrawImageUnscaled(Image image, Point point) => DrawImageUnscaled(image, point.X, point.Y);

        public void DrawImage(Image image, RectangleF rect) => DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawImage(Image image, Rectangle rect) => DrawImage(image, (float)rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawImage(Image image, int x, int y, int width, int height) => DrawImage(image, (float)x, y, width, height);

        public void DrawImage(Image image, float x, float y, float width, float height)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            DrawImage(image, new RectangleF(x, y, width, height), new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, null);
        }

        public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit)
            => DrawImage(image, (RectangleF)destRect, (RectangleF)srcRect, srcUnit, null);

        public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit)
            => DrawImage(image, destRect, srcRect, srcUnit, null);

        public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit)
            => DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit, null);

        public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttr)
            => DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit, imageAttr);

        public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit)
            => DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit, null);

        public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttrs)
            => DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit, imageAttrs);

        public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit, ImageAttributes imageAttr)
            => DrawImage(image, (RectangleF)destRect, (RectangleF)srcRect, srcUnit, imageAttr);

        public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes imageAttrs)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (image.IsDisposed || destRect.Width == 0 || destRect.Height == 0 || srcRect.Width <= 0 || srcRect.Height <= 0)
            {
                return;
            }

            if (!Prepare())
            {
                return;
            }

            SKImage skImage = image.GetSKImage();
            float worldScale = (float)Math.Sqrt(Math.Abs((_transform.Elements[0] * _transform.Elements[3]) - (_transform.Elements[1] * _transform.Elements[2])));
            if (worldScale <= 0)
            {
                worldScale = 1;
            }

            float shrink = srcRect.Width / (Math.Abs(destRect.Width) * worldScale);
            SKSamplingOptions sampling = GraphicsHelper.SamplingFor(InterpolationMode, shrink);

            using var paint = new SKPaint { IsAntialias = false };
            if (imageAttrs != null)
            {
                paint.ColorFilter = imageAttrs.CreateColorFilter();
            }

            if (CompositingMode == CompositingMode.SourceCopy)
            {
                paint.BlendMode = SKBlendMode.Src;
            }

            SKRect src = srcRect.ToSK();
            SKRect dst = destRect.ToSK();
            _canvas.DrawImage(skImage, src, dst, sampling, paint);
            paint.ColorFilter?.Dispose();
        }

        /// <summary>Draws the image into the parallelogram given by three points (upper-left, upper-right, lower-left).</summary>
        public void DrawImage(Image image, PointF[] destPoints)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (destPoints == null || destPoints.Length != 3)
            {
                throw new ArgumentException("Three points are required.", nameof(destPoints));
            }

            if (image.IsDisposed || !Prepare())
            {
                return;
            }

            using var m = new Matrix(new RectangleF(0, 0, image.Width, image.Height), destPoints);
            SKMatrix extra = m.ToSK();
            _canvas.Concat(extra);
            using var paint = new SKPaint();
            _canvas.DrawImage(image.GetSKImage(), 0, 0, GraphicsHelper.SamplingFor(InterpolationMode, 1f), paint);
        }

        public void DrawImage(Image image, Point[] destPoints) => DrawImage(image, Array.ConvertAll(destPoints, p => (PointF)p));

        // ------------------------------------------------------------------ text

        private bool AliasedText => TextRenderingHint is TextRenderingHint.SingleBitPerPixel or TextRenderingHint.SingleBitPerPixelGridFit;

        public void DrawString(string s, Font font, Brush brush, PointF point) => DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), null);

        public void DrawString(string s, Font font, Brush brush, PointF point, StringFormat format) => DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);

        public void DrawString(string s, Font font, Brush brush, float x, float y) => DrawString(s, font, brush, new RectangleF(x, y, 0, 0), null);

        public void DrawString(string s, Font font, Brush brush, float x, float y, StringFormat format) => DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle) => DrawString(s, font, brush, layoutRectangle, null);

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat format)
        {
            if (brush == null)
            {
                throw new ArgumentNullException(nameof(brush));
            }

            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            var info = new TextEngine.FontInfo(font, DpiY);
            TextEngine.Layout layout = TextEngine.DoLayout(s, info, format, layoutRectangle, true);
            if (layout.Lines.Count == 0 || !Prepare())
            {
                return;
            }

            bool aliased = AliasedText;
            using var paint = new SKPaint { IsAntialias = !aliased, Style = SKPaintStyle.Fill };
            brush.ConfigurePaint(paint);
            if (CompositingMode == CompositingMode.SourceCopy)
            {
                paint.BlendMode = SKBlendMode.Src;
            }

            if (layout.Clip)
            {
                _canvas.ClipRect(layout.Rect.ToSK(), SKClipOperation.Intersect, false);
            }

            foreach (TextEngine.Line line in layout.Lines)
            {
                float x = line.X;
                foreach (TextEngine.Run run in line.Runs)
                {
                    _canvas.DrawText(run.Text, x, line.Baseline, aliased ? run.FontAliased : run.Font, paint);
                    x += run.Width;
                }

                foreach (SKRect deco in TextEngine.DecorationRects(info, line))
                {
                    _canvas.DrawRect(deco, paint);
                }
            }
        }

        public SizeF MeasureString(string text, Font font) => MeasureString(text, font, new SizeF(0, 0), null, out _, out _);

        public SizeF MeasureString(string text, Font font, int width) => MeasureString(text, font, new SizeF(width, 0), null, out _, out _);

        public SizeF MeasureString(string text, Font font, SizeF layoutArea) => MeasureString(text, font, layoutArea, null, out _, out _);

        public SizeF MeasureString(string text, Font font, int width, StringFormat format) => MeasureString(text, font, new SizeF(width, 0), format, out _, out _);

        public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat) => MeasureString(text, font, layoutArea, stringFormat, out _, out _);

        public SizeF MeasureString(string text, Font font, PointF origin, StringFormat stringFormat)
        {
            return MeasureStringCore(text, font, new RectangleF(origin.X, origin.Y, 0, 0), stringFormat, out _, out _);
        }

        public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat, out int charactersFitted, out int linesFilled)
        {
            return MeasureStringCore(text, font, new RectangleF(0, 0, layoutArea.Width, layoutArea.Height), stringFormat, out charactersFitted, out linesFilled);
        }

        private SizeF MeasureStringCore(string text, Font font, RectangleF rect, StringFormat format, out int charsFitted, out int linesFilled)
        {
            charsFitted = 0;
            linesFilled = 0;
            if (string.IsNullOrEmpty(text))
            {
                return SizeF.Empty;
            }

            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            TextEngine.Layout layout = TextEngine.DoLayout(text, new TextEngine.FontInfo(font, DpiY), format, rect, false);
            charsFitted = layout.CharsFitted;
            linesFilled = layout.Lines.Count;
            return new SizeF(layout.Width, layout.Height);
        }

        public Region[] MeasureCharacterRanges(string text, Font font, RectangleF layoutRect, StringFormat stringFormat)
        {
            // Not supported: return empty regions for each requested range.
            return [];
        }

        public Color GetNearestColor(Color color) => color;

        public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize)
        {
        }
    }
}
