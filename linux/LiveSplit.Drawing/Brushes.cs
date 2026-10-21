using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing
{
    public abstract class Brush : MarshalByRefObject, ICloneable, IDisposable
    {
        public abstract object Clone();

        /// <summary>Configures an SKPaint (fill style) so that it paints with this brush.</summary>
        internal abstract void ConfigurePaint(SKPaint paint);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }

    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; set; }

        public override object Clone() => new SolidBrush(Color);

        internal override void ConfigurePaint(SKPaint paint)
        {
            paint.Shader = null;
            paint.Color = Color.ToSK();
        }
    }

    public sealed class TextureBrush : Brush
    {
        private Image _image;

        public TextureBrush(Image bitmap) : this(bitmap, WrapMode.Tile)
        {
        }

        public TextureBrush(Image image, WrapMode wrapMode)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            WrapMode = wrapMode;
        }

        public Image Image => _image;

        public WrapMode WrapMode { get; set; }

        public Matrix Transform { get; set; } = new Matrix();

        public void ResetTransform() => Transform = new Matrix();

        public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend) => Transform.Translate(dx, dy, order);

        public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend) => Transform.Scale(sx, sy, order);

        public void RotateTransform(float angle, MatrixOrder order = MatrixOrder.Prepend) => Transform.Rotate(angle, order);

        public void MultiplyTransform(Matrix matrix, MatrixOrder order = MatrixOrder.Prepend) => Transform.Multiply(matrix, order);

        public override object Clone()
        {
            var b = new TextureBrush(_image, WrapMode) { Transform = Transform.Clone() };
            return b;
        }

        internal override void ConfigurePaint(SKPaint paint)
        {
            SKShaderTileMode mode = WrapMode == WrapMode.Clamp ? SKShaderTileMode.Clamp : SKShaderTileMode.Repeat;
            if (WrapMode is WrapMode.TileFlipX or WrapMode.TileFlipY or WrapMode.TileFlipXY)
            {
                mode = SKShaderTileMode.Mirror;
            }

            paint.Shader = _image.GetSKImage().ToShader(mode, mode, new SKSamplingOptions(SKFilterMode.Linear), Transform.ToSK());
        }
    }

    public static class Brushes
    {
        private static SolidBrush Make(Color c) => new SolidBrush(c);

        public static Brush Transparent => Make(Color.Transparent);
        public static Brush AliceBlue => Make(Color.AliceBlue);
        public static Brush AntiqueWhite => Make(Color.AntiqueWhite);
        public static Brush Aqua => Make(Color.Aqua);
        public static Brush Aquamarine => Make(Color.Aquamarine);
        public static Brush Azure => Make(Color.Azure);
        public static Brush Beige => Make(Color.Beige);
        public static Brush Bisque => Make(Color.Bisque);
        public static Brush Black => Make(Color.Black);
        public static Brush BlanchedAlmond => Make(Color.BlanchedAlmond);
        public static Brush Blue => Make(Color.Blue);
        public static Brush BlueViolet => Make(Color.BlueViolet);
        public static Brush Brown => Make(Color.Brown);
        public static Brush BurlyWood => Make(Color.BurlyWood);
        public static Brush CadetBlue => Make(Color.CadetBlue);
        public static Brush Chartreuse => Make(Color.Chartreuse);
        public static Brush Chocolate => Make(Color.Chocolate);
        public static Brush Coral => Make(Color.Coral);
        public static Brush CornflowerBlue => Make(Color.CornflowerBlue);
        public static Brush Cornsilk => Make(Color.Cornsilk);
        public static Brush Crimson => Make(Color.Crimson);
        public static Brush Cyan => Make(Color.Cyan);
        public static Brush DarkBlue => Make(Color.DarkBlue);
        public static Brush DarkCyan => Make(Color.DarkCyan);
        public static Brush DarkGoldenrod => Make(Color.DarkGoldenrod);
        public static Brush DarkGray => Make(Color.DarkGray);
        public static Brush DarkGreen => Make(Color.DarkGreen);
        public static Brush DarkKhaki => Make(Color.DarkKhaki);
        public static Brush DarkMagenta => Make(Color.DarkMagenta);
        public static Brush DarkOliveGreen => Make(Color.DarkOliveGreen);
        public static Brush DarkOrange => Make(Color.DarkOrange);
        public static Brush DarkOrchid => Make(Color.DarkOrchid);
        public static Brush DarkRed => Make(Color.DarkRed);
        public static Brush DarkSalmon => Make(Color.DarkSalmon);
        public static Brush DarkSeaGreen => Make(Color.DarkSeaGreen);
        public static Brush DarkSlateBlue => Make(Color.DarkSlateBlue);
        public static Brush DarkSlateGray => Make(Color.DarkSlateGray);
        public static Brush DarkTurquoise => Make(Color.DarkTurquoise);
        public static Brush DarkViolet => Make(Color.DarkViolet);
        public static Brush DeepPink => Make(Color.DeepPink);
        public static Brush DeepSkyBlue => Make(Color.DeepSkyBlue);
        public static Brush DimGray => Make(Color.DimGray);
        public static Brush DodgerBlue => Make(Color.DodgerBlue);
        public static Brush Firebrick => Make(Color.Firebrick);
        public static Brush FloralWhite => Make(Color.FloralWhite);
        public static Brush ForestGreen => Make(Color.ForestGreen);
        public static Brush Fuchsia => Make(Color.Fuchsia);
        public static Brush Gainsboro => Make(Color.Gainsboro);
        public static Brush GhostWhite => Make(Color.GhostWhite);
        public static Brush Gold => Make(Color.Gold);
        public static Brush Goldenrod => Make(Color.Goldenrod);
        public static Brush Gray => Make(Color.Gray);
        public static Brush Green => Make(Color.Green);
        public static Brush GreenYellow => Make(Color.GreenYellow);
        public static Brush Honeydew => Make(Color.Honeydew);
        public static Brush HotPink => Make(Color.HotPink);
        public static Brush IndianRed => Make(Color.IndianRed);
        public static Brush Indigo => Make(Color.Indigo);
        public static Brush Ivory => Make(Color.Ivory);
        public static Brush Khaki => Make(Color.Khaki);
        public static Brush Lavender => Make(Color.Lavender);
        public static Brush LavenderBlush => Make(Color.LavenderBlush);
        public static Brush LawnGreen => Make(Color.LawnGreen);
        public static Brush LemonChiffon => Make(Color.LemonChiffon);
        public static Brush LightBlue => Make(Color.LightBlue);
        public static Brush LightCoral => Make(Color.LightCoral);
        public static Brush LightCyan => Make(Color.LightCyan);
        public static Brush LightGoldenrodYellow => Make(Color.LightGoldenrodYellow);
        public static Brush LightGray => Make(Color.LightGray);
        public static Brush LightGreen => Make(Color.LightGreen);
        public static Brush LightPink => Make(Color.LightPink);
        public static Brush LightSalmon => Make(Color.LightSalmon);
        public static Brush LightSeaGreen => Make(Color.LightSeaGreen);
        public static Brush LightSkyBlue => Make(Color.LightSkyBlue);
        public static Brush LightSlateGray => Make(Color.LightSlateGray);
        public static Brush LightSteelBlue => Make(Color.LightSteelBlue);
        public static Brush LightYellow => Make(Color.LightYellow);
        public static Brush Lime => Make(Color.Lime);
        public static Brush LimeGreen => Make(Color.LimeGreen);
        public static Brush Linen => Make(Color.Linen);
        public static Brush Magenta => Make(Color.Magenta);
        public static Brush Maroon => Make(Color.Maroon);
        public static Brush MediumAquamarine => Make(Color.MediumAquamarine);
        public static Brush MediumBlue => Make(Color.MediumBlue);
        public static Brush MediumOrchid => Make(Color.MediumOrchid);
        public static Brush MediumPurple => Make(Color.MediumPurple);
        public static Brush MediumSeaGreen => Make(Color.MediumSeaGreen);
        public static Brush MediumSlateBlue => Make(Color.MediumSlateBlue);
        public static Brush MediumSpringGreen => Make(Color.MediumSpringGreen);
        public static Brush MediumTurquoise => Make(Color.MediumTurquoise);
        public static Brush MediumVioletRed => Make(Color.MediumVioletRed);
        public static Brush MidnightBlue => Make(Color.MidnightBlue);
        public static Brush MintCream => Make(Color.MintCream);
        public static Brush MistyRose => Make(Color.MistyRose);
        public static Brush Moccasin => Make(Color.Moccasin);
        public static Brush NavajoWhite => Make(Color.NavajoWhite);
        public static Brush Navy => Make(Color.Navy);
        public static Brush OldLace => Make(Color.OldLace);
        public static Brush Olive => Make(Color.Olive);
        public static Brush OliveDrab => Make(Color.OliveDrab);
        public static Brush Orange => Make(Color.Orange);
        public static Brush OrangeRed => Make(Color.OrangeRed);
        public static Brush Orchid => Make(Color.Orchid);
        public static Brush PaleGoldenrod => Make(Color.PaleGoldenrod);
        public static Brush PaleGreen => Make(Color.PaleGreen);
        public static Brush PaleTurquoise => Make(Color.PaleTurquoise);
        public static Brush PaleVioletRed => Make(Color.PaleVioletRed);
        public static Brush PapayaWhip => Make(Color.PapayaWhip);
        public static Brush PeachPuff => Make(Color.PeachPuff);
        public static Brush Peru => Make(Color.Peru);
        public static Brush Pink => Make(Color.Pink);
        public static Brush Plum => Make(Color.Plum);
        public static Brush PowderBlue => Make(Color.PowderBlue);
        public static Brush Purple => Make(Color.Purple);
        public static Brush Red => Make(Color.Red);
        public static Brush RosyBrown => Make(Color.RosyBrown);
        public static Brush RoyalBlue => Make(Color.RoyalBlue);
        public static Brush SaddleBrown => Make(Color.SaddleBrown);
        public static Brush Salmon => Make(Color.Salmon);
        public static Brush SandyBrown => Make(Color.SandyBrown);
        public static Brush SeaGreen => Make(Color.SeaGreen);
        public static Brush SeaShell => Make(Color.SeaShell);
        public static Brush Sienna => Make(Color.Sienna);
        public static Brush Silver => Make(Color.Silver);
        public static Brush SkyBlue => Make(Color.SkyBlue);
        public static Brush SlateBlue => Make(Color.SlateBlue);
        public static Brush SlateGray => Make(Color.SlateGray);
        public static Brush Snow => Make(Color.Snow);
        public static Brush SpringGreen => Make(Color.SpringGreen);
        public static Brush SteelBlue => Make(Color.SteelBlue);
        public static Brush Tan => Make(Color.Tan);
        public static Brush Teal => Make(Color.Teal);
        public static Brush Thistle => Make(Color.Thistle);
        public static Brush Tomato => Make(Color.Tomato);
        public static Brush Turquoise => Make(Color.Turquoise);
        public static Brush Violet => Make(Color.Violet);
        public static Brush Wheat => Make(Color.Wheat);
        public static Brush White => Make(Color.White);
        public static Brush WhiteSmoke => Make(Color.WhiteSmoke);
        public static Brush Yellow => Make(Color.Yellow);
        public static Brush YellowGreen => Make(Color.YellowGreen);
    }
}
