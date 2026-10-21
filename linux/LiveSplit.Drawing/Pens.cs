using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing
{
    public sealed class Pen : MarshalByRefObject, ICloneable, IDisposable
    {
        private Brush _brush;
        private Color _color;
        private float[] _dashPattern;

        public Pen(Color color) : this(color, 1f)
        {
        }

        public Pen(Color color, float width)
        {
            _color = color;
            Width = width;
        }

        public Pen(Brush brush) : this(brush, 1f)
        {
        }

        public Pen(Brush brush, float width)
        {
            _brush = brush ?? throw new ArgumentNullException(nameof(brush));
            if (brush is SolidBrush sb)
            {
                _color = sb.Color;
            }

            Width = width;
        }

        public float Width { get; set; }

        public Color Color
        {
            get => _brush is SolidBrush sb ? sb.Color : _color;
            set
            {
                _color = value;
                _brush = null;
            }
        }

        public Brush Brush
        {
            get => _brush ?? new SolidBrush(_color);
            set => _brush = value ?? throw new ArgumentNullException(nameof(value));
        }

        public PenType PenType => _brush == null || _brush is SolidBrush ? PenType.SolidColor
            : _brush is LinearGradientBrush ? PenType.LinearGradient : PenType.TextureFill;

        public LineCap StartCap { get; set; } = LineCap.Flat;

        public LineCap EndCap { get; set; } = LineCap.Flat;

        public DashCap DashCap { get; set; } = DashCap.Flat;

        public LineJoin LineJoin { get; set; } = LineJoin.Miter;

        public float MiterLimit { get; set; } = 10f;

        public PenAlignment Alignment { get; set; } = PenAlignment.Center;

        public DashStyle DashStyle { get; set; } = DashStyle.Solid;

        public float DashOffset { get; set; }

        public float[] DashPattern
        {
            get => _dashPattern ?? DashStyle switch
            {
                DashStyle.Dash => [3f, 1f],
                DashStyle.Dot => [1f, 1f],
                DashStyle.DashDot => [3f, 1f, 1f, 1f],
                DashStyle.DashDotDot => [3f, 1f, 1f, 1f, 1f, 1f],
                _ => [],
            };
            set
            {
                _dashPattern = value;
                DashStyle = DashStyle.Custom;
            }
        }

        public Matrix Transform { get; set; } = new Matrix();

        public void ResetTransform() => Transform = new Matrix();

        public void SetLineCap(LineCap startCap, LineCap endCap, DashCap dashCap)
        {
            StartCap = startCap;
            EndCap = endCap;
            DashCap = dashCap;
        }

        public object Clone()
        {
            return new Pen(Color, Width)
            {
                _brush = _brush is SolidBrush ? null : (Brush)_brush?.Clone(),
                StartCap = StartCap,
                EndCap = EndCap,
                DashCap = DashCap,
                LineJoin = LineJoin,
                MiterLimit = MiterLimit,
                Alignment = Alignment,
                DashStyle = DashStyle,
                DashOffset = DashOffset,
                _dashPattern = (float[])_dashPattern?.Clone(),
                Transform = Transform.Clone(),
            };
        }

        public void Dispose()
        {
        }

        /// <summary>Sets stroke width/caps/join/dash on the paint (does not touch colour/shader).</summary>
        internal void ConfigureStroke(SKPaint paint)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = Width <= 0 ? 0 : Width;
            paint.StrokeMiter = MiterLimit;
            paint.StrokeJoin = LineJoin switch
            {
                LineJoin.Round => SKStrokeJoin.Round,
                LineJoin.Bevel => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter,
            };

            // GDI+ pens have separate caps; Skia only one. Use the start cap (they are equal in all LiveSplit uses).
            LineCap cap = StartCap == LineCap.Flat ? EndCap : StartCap;
            paint.StrokeCap = (cap & ~LineCap.AnchorMask) switch
            {
                LineCap.Round => SKStrokeCap.Round,
                LineCap.Square => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt,
            };

            if (DashStyle != DashStyle.Solid)
            {
                float[] pattern = DashPattern;
                if (pattern.Length >= 2)
                {
                    var intervals = new float[pattern.Length % 2 == 0 ? pattern.Length : pattern.Length * 2];
                    for (int i = 0; i < intervals.Length; i++)
                    {
                        intervals[i] = Math.Max(0.0001f, pattern[i % pattern.Length] * Math.Max(Width, 1f));
                    }

                    paint.PathEffect = SKPathEffect.CreateDash(intervals, DashOffset * Math.Max(Width, 1f));
                }
            }
        }

        internal void ConfigurePaint(SKPaint paint)
        {
            ConfigureStroke(paint);
            if (_brush != null)
            {
                _brush.ConfigurePaint(paint);
            }
            else
            {
                paint.Shader = null;
                paint.Color = _color.ToSK();
            }
        }
    }

    public static class Pens
    {
        private static Pen Make(Color c) => new Pen(c);

        public static Pen Transparent => Make(Color.Transparent);
        public static Pen AliceBlue => Make(Color.AliceBlue);
        public static Pen AntiqueWhite => Make(Color.AntiqueWhite);
        public static Pen Aqua => Make(Color.Aqua);
        public static Pen Aquamarine => Make(Color.Aquamarine);
        public static Pen Azure => Make(Color.Azure);
        public static Pen Beige => Make(Color.Beige);
        public static Pen Bisque => Make(Color.Bisque);
        public static Pen Black => Make(Color.Black);
        public static Pen BlanchedAlmond => Make(Color.BlanchedAlmond);
        public static Pen Blue => Make(Color.Blue);
        public static Pen BlueViolet => Make(Color.BlueViolet);
        public static Pen Brown => Make(Color.Brown);
        public static Pen BurlyWood => Make(Color.BurlyWood);
        public static Pen CadetBlue => Make(Color.CadetBlue);
        public static Pen Chartreuse => Make(Color.Chartreuse);
        public static Pen Chocolate => Make(Color.Chocolate);
        public static Pen Coral => Make(Color.Coral);
        public static Pen CornflowerBlue => Make(Color.CornflowerBlue);
        public static Pen Cornsilk => Make(Color.Cornsilk);
        public static Pen Crimson => Make(Color.Crimson);
        public static Pen Cyan => Make(Color.Cyan);
        public static Pen DarkBlue => Make(Color.DarkBlue);
        public static Pen DarkCyan => Make(Color.DarkCyan);
        public static Pen DarkGoldenrod => Make(Color.DarkGoldenrod);
        public static Pen DarkGray => Make(Color.DarkGray);
        public static Pen DarkGreen => Make(Color.DarkGreen);
        public static Pen DarkKhaki => Make(Color.DarkKhaki);
        public static Pen DarkMagenta => Make(Color.DarkMagenta);
        public static Pen DarkOliveGreen => Make(Color.DarkOliveGreen);
        public static Pen DarkOrange => Make(Color.DarkOrange);
        public static Pen DarkOrchid => Make(Color.DarkOrchid);
        public static Pen DarkRed => Make(Color.DarkRed);
        public static Pen DarkSalmon => Make(Color.DarkSalmon);
        public static Pen DarkSeaGreen => Make(Color.DarkSeaGreen);
        public static Pen DarkSlateBlue => Make(Color.DarkSlateBlue);
        public static Pen DarkSlateGray => Make(Color.DarkSlateGray);
        public static Pen DarkTurquoise => Make(Color.DarkTurquoise);
        public static Pen DarkViolet => Make(Color.DarkViolet);
        public static Pen DeepPink => Make(Color.DeepPink);
        public static Pen DeepSkyBlue => Make(Color.DeepSkyBlue);
        public static Pen DimGray => Make(Color.DimGray);
        public static Pen DodgerBlue => Make(Color.DodgerBlue);
        public static Pen Firebrick => Make(Color.Firebrick);
        public static Pen FloralWhite => Make(Color.FloralWhite);
        public static Pen ForestGreen => Make(Color.ForestGreen);
        public static Pen Fuchsia => Make(Color.Fuchsia);
        public static Pen Gainsboro => Make(Color.Gainsboro);
        public static Pen GhostWhite => Make(Color.GhostWhite);
        public static Pen Gold => Make(Color.Gold);
        public static Pen Goldenrod => Make(Color.Goldenrod);
        public static Pen Gray => Make(Color.Gray);
        public static Pen Green => Make(Color.Green);
        public static Pen GreenYellow => Make(Color.GreenYellow);
        public static Pen Honeydew => Make(Color.Honeydew);
        public static Pen HotPink => Make(Color.HotPink);
        public static Pen IndianRed => Make(Color.IndianRed);
        public static Pen Indigo => Make(Color.Indigo);
        public static Pen Ivory => Make(Color.Ivory);
        public static Pen Khaki => Make(Color.Khaki);
        public static Pen Lavender => Make(Color.Lavender);
        public static Pen LavenderBlush => Make(Color.LavenderBlush);
        public static Pen LawnGreen => Make(Color.LawnGreen);
        public static Pen LemonChiffon => Make(Color.LemonChiffon);
        public static Pen LightBlue => Make(Color.LightBlue);
        public static Pen LightCoral => Make(Color.LightCoral);
        public static Pen LightCyan => Make(Color.LightCyan);
        public static Pen LightGoldenrodYellow => Make(Color.LightGoldenrodYellow);
        public static Pen LightGray => Make(Color.LightGray);
        public static Pen LightGreen => Make(Color.LightGreen);
        public static Pen LightPink => Make(Color.LightPink);
        public static Pen LightSalmon => Make(Color.LightSalmon);
        public static Pen LightSeaGreen => Make(Color.LightSeaGreen);
        public static Pen LightSkyBlue => Make(Color.LightSkyBlue);
        public static Pen LightSlateGray => Make(Color.LightSlateGray);
        public static Pen LightSteelBlue => Make(Color.LightSteelBlue);
        public static Pen LightYellow => Make(Color.LightYellow);
        public static Pen Lime => Make(Color.Lime);
        public static Pen LimeGreen => Make(Color.LimeGreen);
        public static Pen Linen => Make(Color.Linen);
        public static Pen Magenta => Make(Color.Magenta);
        public static Pen Maroon => Make(Color.Maroon);
        public static Pen MediumAquamarine => Make(Color.MediumAquamarine);
        public static Pen MediumBlue => Make(Color.MediumBlue);
        public static Pen MediumOrchid => Make(Color.MediumOrchid);
        public static Pen MediumPurple => Make(Color.MediumPurple);
        public static Pen MediumSeaGreen => Make(Color.MediumSeaGreen);
        public static Pen MediumSlateBlue => Make(Color.MediumSlateBlue);
        public static Pen MediumSpringGreen => Make(Color.MediumSpringGreen);
        public static Pen MediumTurquoise => Make(Color.MediumTurquoise);
        public static Pen MediumVioletRed => Make(Color.MediumVioletRed);
        public static Pen MidnightBlue => Make(Color.MidnightBlue);
        public static Pen MintCream => Make(Color.MintCream);
        public static Pen MistyRose => Make(Color.MistyRose);
        public static Pen Moccasin => Make(Color.Moccasin);
        public static Pen NavajoWhite => Make(Color.NavajoWhite);
        public static Pen Navy => Make(Color.Navy);
        public static Pen OldLace => Make(Color.OldLace);
        public static Pen Olive => Make(Color.Olive);
        public static Pen OliveDrab => Make(Color.OliveDrab);
        public static Pen Orange => Make(Color.Orange);
        public static Pen OrangeRed => Make(Color.OrangeRed);
        public static Pen Orchid => Make(Color.Orchid);
        public static Pen PaleGoldenrod => Make(Color.PaleGoldenrod);
        public static Pen PaleGreen => Make(Color.PaleGreen);
        public static Pen PaleTurquoise => Make(Color.PaleTurquoise);
        public static Pen PaleVioletRed => Make(Color.PaleVioletRed);
        public static Pen PapayaWhip => Make(Color.PapayaWhip);
        public static Pen PeachPuff => Make(Color.PeachPuff);
        public static Pen Peru => Make(Color.Peru);
        public static Pen Pink => Make(Color.Pink);
        public static Pen Plum => Make(Color.Plum);
        public static Pen PowderBlue => Make(Color.PowderBlue);
        public static Pen Purple => Make(Color.Purple);
        public static Pen Red => Make(Color.Red);
        public static Pen RosyBrown => Make(Color.RosyBrown);
        public static Pen RoyalBlue => Make(Color.RoyalBlue);
        public static Pen SaddleBrown => Make(Color.SaddleBrown);
        public static Pen Salmon => Make(Color.Salmon);
        public static Pen SandyBrown => Make(Color.SandyBrown);
        public static Pen SeaGreen => Make(Color.SeaGreen);
        public static Pen SeaShell => Make(Color.SeaShell);
        public static Pen Sienna => Make(Color.Sienna);
        public static Pen Silver => Make(Color.Silver);
        public static Pen SkyBlue => Make(Color.SkyBlue);
        public static Pen SlateBlue => Make(Color.SlateBlue);
        public static Pen SlateGray => Make(Color.SlateGray);
        public static Pen Snow => Make(Color.Snow);
        public static Pen SpringGreen => Make(Color.SpringGreen);
        public static Pen SteelBlue => Make(Color.SteelBlue);
        public static Pen Tan => Make(Color.Tan);
        public static Pen Teal => Make(Color.Teal);
        public static Pen Thistle => Make(Color.Thistle);
        public static Pen Tomato => Make(Color.Tomato);
        public static Pen Turquoise => Make(Color.Turquoise);
        public static Pen Violet => Make(Color.Violet);
        public static Pen Wheat => Make(Color.Wheat);
        public static Pen White => Make(Color.White);
        public static Pen WhiteSmoke => Make(Color.WhiteSmoke);
        public static Pen Yellow => Make(Color.Yellow);
        public static Pen YellowGreen => Make(Color.YellowGreen);
    }
}
