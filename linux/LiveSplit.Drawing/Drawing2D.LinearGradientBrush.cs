using SkiaSharp;

namespace System.Drawing.Drawing2D
{
    public sealed class ColorBlend
    {
        public ColorBlend()
        {
            Colors = new Color[1];
            Positions = new float[1];
        }

        public ColorBlend(int count)
        {
            Colors = new Color[count];
            Positions = new float[count];
        }

        public Color[] Colors { get; set; }

        public float[] Positions { get; set; }
    }

    public sealed class Blend
    {
        public Blend()
        {
            Factors = new float[1];
            Positions = new float[1];
        }

        public Blend(int count)
        {
            Factors = new float[count];
            Positions = new float[count];
        }

        public float[] Factors { get; set; }

        public float[] Positions { get; set; }
    }

    /// <summary>
    /// Two (or more, via <see cref="InterpolationColors"/>) colour linear gradient. Like GDI+, the gradient
    /// repeats (WrapMode.Tile) outside of the gradient line unless WrapMode is Clamp.
    /// </summary>
    public sealed class LinearGradientBrush : Brush
    {
        private PointF _p1, _p2;
        private RectangleF _rect;
        private Color[] _colors = new Color[2];
        private ColorBlend _blend;

        public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
        {
            _p1 = point1;
            _p2 = point2;
            _rect = RectangleF.FromLTRB(Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y), Math.Max(point1.X, point2.X), Math.Max(point1.Y, point2.Y));
            _colors = [color1, color2];
        }

        public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
            : this((PointF)point1, (PointF)point2, color1, color2)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        {
            if (rect.Width == 0 || rect.Height == 0)
            {
                throw new ArgumentException("Rectangle must not be empty.", nameof(rect));
            }

            _rect = rect;
            _colors = [color1, color2];
            switch (linearGradientMode)
            {
                case LinearGradientMode.Horizontal:
                    _p1 = new PointF(rect.Left, rect.Top);
                    _p2 = new PointF(rect.Right, rect.Top);
                    break;
                case LinearGradientMode.Vertical:
                    _p1 = new PointF(rect.Left, rect.Top);
                    _p2 = new PointF(rect.Left, rect.Bottom);
                    break;
                case LinearGradientMode.ForwardDiagonal:
                    _p1 = new PointF(rect.Left, rect.Top);
                    _p2 = new PointF(rect.Right, rect.Bottom);
                    break;
                default:
                    _p1 = new PointF(rect.Right, rect.Top);
                    _p2 = new PointF(rect.Left, rect.Bottom);
                    break;
            }
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
            : this((RectangleF)rect, color1, color2, linearGradientMode)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
            : this(rect, color1, color2, angle, false)
        {
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
            : this((RectangleF)rect, color1, color2, angle, false)
        {
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
            : this((RectangleF)rect, color1, color2, angle, isAngleScaleable)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        {
            if (rect.Width == 0 || rect.Height == 0)
            {
                throw new ArgumentException("Rectangle must not be empty.", nameof(rect));
            }

            _rect = rect;
            _colors = [color1, color2];
            double rad = angle * Math.PI / 180.0;
            double dx = Math.Cos(rad), dy = Math.Sin(rad);
            double len = Math.Abs(rect.Width * dx) + Math.Abs(rect.Height * dy);
            float cx = rect.Left + (rect.Width / 2f), cy = rect.Top + (rect.Height / 2f);
            _p1 = new PointF((float)(cx - (dx * len / 2)), (float)(cy - (dy * len / 2)));
            _p2 = new PointF((float)(cx + (dx * len / 2)), (float)(cy + (dy * len / 2)));
        }

        public RectangleF Rectangle => _rect;

        public Color[] LinearColors
        {
            get => [_colors[0], _colors[1]];
            set
            {
                if (value == null || value.Length < 2)
                {
                    throw new ArgumentException("Two colors are required.");
                }

                _colors = [value[0], value[1]];
                _blend = null;
            }
        }

        public ColorBlend InterpolationColors
        {
            get
            {
                return _blend ?? new ColorBlend(2) { Colors = [_colors[0], _colors[1]], Positions = [0f, 1f] };
            }
            set => _blend = value;
        }

        public WrapMode WrapMode { get; set; } = WrapMode.Tile;

        public bool GammaCorrection { get; set; }

        public Blend Blend { get; set; }

        public Matrix Transform { get; set; } = new Matrix();

        public void ResetTransform() => Transform = new Matrix();

        public void TranslateTransform(float dx, float dy) => Transform.Translate(dx, dy);

        public void TranslateTransform(float dx, float dy, MatrixOrder order) => Transform.Translate(dx, dy, order);

        public void ScaleTransform(float sx, float sy) => Transform.Scale(sx, sy);

        public void ScaleTransform(float sx, float sy, MatrixOrder order) => Transform.Scale(sx, sy, order);

        public void RotateTransform(float angle) => Transform.Rotate(angle);

        public void RotateTransform(float angle, MatrixOrder order) => Transform.Rotate(angle, order);

        public void MultiplyTransform(Matrix matrix) => Transform.Multiply(matrix);

        public void MultiplyTransform(Matrix matrix, MatrixOrder order) => Transform.Multiply(matrix, order);

        public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, 1f);

        public void SetBlendTriangularShape(float focus, float scale)
        {
            Color a = _colors[0], b = _colors[1];
            Color peak = Lerp(a, b, scale);
            _blend = focus <= 0f ? new ColorBlend(2) { Colors = [peak, b], Positions = [0f, 1f] }
                : focus >= 1f ? new ColorBlend(2) { Colors = [a, peak], Positions = [0f, 1f] }
                : new ColorBlend(3) { Colors = [a, peak, b], Positions = [0f, focus, 1f] };
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            static int L(int x, int y, float t) => (int)Math.Round(x + ((y - x) * t));
            return Color.FromArgb(L(a.A, b.A, t), L(a.R, b.R, t), L(a.G, b.G, t), L(a.B, b.B, t));
        }

        public override object Clone()
        {
            return new LinearGradientBrush(_p1, _p2, _colors[0], _colors[1])
            {
                _rect = _rect,
                _blend = _blend == null ? null : new ColorBlend { Colors = (Color[])_blend.Colors.Clone(), Positions = (float[])_blend.Positions.Clone() },
                WrapMode = WrapMode,
                GammaCorrection = GammaCorrection,
                Transform = Transform.Clone(),
            };
        }

        internal override void ConfigurePaint(SKPaint paint)
        {
            Color[] colors = _blend != null ? _blend.Colors : _colors;
            float[] positions = _blend?.Positions;

            if (_p1 == _p2)
            {
                // GDI+ rejects degenerate gradients; a flat first colour is the sensible rendering.
                paint.Shader = null;
                paint.Color = colors[0].ToSK();
                return;
            }

            var skColors = new SKColor[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                skColors[i] = colors[i].ToSK();
            }

            SKShaderTileMode mode = WrapMode switch
            {
                WrapMode.Clamp => SKShaderTileMode.Clamp,
                WrapMode.TileFlipX or WrapMode.TileFlipY or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
                _ => SKShaderTileMode.Repeat,
            };

            paint.Shader = SKShader.CreateLinearGradient(_p1.ToSK(), _p2.ToSK(), skColors, positions, mode, Transform.ToSK());
        }
    }
}
