using System.Linq;
using SkiaSharp;

namespace System.Drawing
{
    public sealed class FontFamily : MarshalByRefObject, IDisposable
    {
        private readonly string _name;

        public FontFamily(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public FontFamily(System.Drawing.Text.GenericFontFamilies genericFamily)
        {
            _name = genericFamily switch
            {
                Text.GenericFontFamilies.Serif => "Times New Roman",
                Text.GenericFontFamilies.Monospace => "Courier New",
                _ => "Microsoft Sans Serif",
            };
        }

        public FontFamily(string name, System.Drawing.Text.FontCollection fontCollection) : this(name)
        {
        }

        public static FontFamily GenericSansSerif => new FontFamily(Text.GenericFontFamilies.SansSerif);

        public static FontFamily GenericSerif => new FontFamily(Text.GenericFontFamilies.Serif);

        public static FontFamily GenericMonospace => new FontFamily(Text.GenericFontFamilies.Monospace);

        /// <summary>All font families installed on the system (via fontconfig), sorted by name.</summary>
        public static FontFamily[] Families => FontResolver.InstalledFamilies.Select(n => new FontFamily(n)).ToArray();

        public static FontFamily[] GetFamilies(Graphics graphics) => Families;

        public string Name => _name;

        public string GetName(int language) => _name;

        public bool IsStyleAvailable(FontStyle style) => FontResolver.IsStyleAvailable(_name, style);

        private SKFont DesignFont(FontStyle style)
        {
            FontResolver.Resolved r = FontResolver.Resolve(_name, style);
            return new SKFont(r.Typeface, Math.Max(1, r.Typeface.UnitsPerEm)) { Hinting = SKFontHinting.None, Subpixel = true };
        }

        /// <summary>Design units per em.</summary>
        public int GetEmHeight(FontStyle style)
        {
            FontResolver.Resolved r = FontResolver.Resolve(_name, style);
            return Math.Max(1, r.Typeface.UnitsPerEm);
        }

        public int GetCellAscent(FontStyle style)
        {
            using SKFont f = DesignFont(style);
            return (int)Math.Round(-f.Metrics.Ascent);
        }

        public int GetCellDescent(FontStyle style)
        {
            using SKFont f = DesignFont(style);
            return (int)Math.Round(f.Metrics.Descent);
        }

        public int GetLineSpacing(FontStyle style)
        {
            using SKFont f = DesignFont(style);
            SKFontMetrics m = f.Metrics;
            return (int)Math.Round(-m.Ascent + m.Descent + m.Leading);
        }

        public void Dispose()
        {
        }

        public override bool Equals(object obj) => obj is FontFamily f && string.Equals(f._name, _name, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_name);

        public override string ToString() => $"[{nameof(FontFamily)}: Name={_name}]";
    }

    public sealed class Font : MarshalByRefObject, ICloneable, IDisposable
    {
        private readonly FontFamily _family;
        private readonly float _size;
        private readonly FontStyle _style;
        private readonly GraphicsUnit _unit;
        private readonly byte _gdiCharSet;
        private readonly bool _gdiVerticalFont;

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
        {
            _family = family ?? throw new ArgumentNullException(nameof(family));
            if (float.IsNaN(emSize) || float.IsInfinity(emSize) || emSize <= 0)
            {
                throw new ArgumentException($"Font size {emSize} is not valid.", nameof(emSize));
            }

            _size = emSize;
            _style = style;
            _unit = unit == GraphicsUnit.World ? GraphicsUnit.Pixel : unit;
            _gdiCharSet = gdiCharSet;
            _gdiVerticalFont = gdiVerticalFont;
        }

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this(family, emSize, style, unit, gdiCharSet, false)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
            : this(family, emSize, style, unit, 1, false)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style)
            : this(family, emSize, style, GraphicsUnit.Point, 1, false)
        {
        }

        public Font(FontFamily family, float emSize, GraphicsUnit unit)
            : this(family, emSize, FontStyle.Regular, unit, 1, false)
        {
        }

        public Font(FontFamily family, float emSize)
            : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point, 1, false)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
            : this(new FontFamily(familyName), emSize, style, unit, gdiCharSet, gdiVerticalFont)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this(new FontFamily(familyName), emSize, style, unit, gdiCharSet, false)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
            : this(new FontFamily(familyName), emSize, style, unit, 1, false)
        {
        }

        public Font(string familyName, float emSize, FontStyle style)
            : this(new FontFamily(familyName), emSize, style, GraphicsUnit.Point, 1, false)
        {
        }

        public Font(string familyName, float emSize, GraphicsUnit unit)
            : this(new FontFamily(familyName), emSize, FontStyle.Regular, unit, 1, false)
        {
        }

        public Font(string familyName, float emSize)
            : this(new FontFamily(familyName), emSize, FontStyle.Regular, GraphicsUnit.Point, 1, false)
        {
        }

        public Font(Font prototype, FontStyle newStyle)
            : this(prototype?.FontFamily, prototype?.Size ?? 0, newStyle, prototype?.Unit ?? GraphicsUnit.Point, prototype?.GdiCharSet ?? 1, prototype?.GdiVerticalFont ?? false)
        {
        }

        public FontFamily FontFamily => _family;

        public string Name => _family.Name;

        public string OriginalFontName => _family.Name;

        public float Size => _size;

        public GraphicsUnit Unit => _unit;

        public FontStyle Style => _style;

        public bool Bold => (_style & FontStyle.Bold) != 0;

        public bool Italic => (_style & FontStyle.Italic) != 0;

        public bool Underline => (_style & FontStyle.Underline) != 0;

        public bool Strikeout => (_style & FontStyle.Strikeout) != 0;

        public byte GdiCharSet => _gdiCharSet;

        public bool GdiVerticalFont => _gdiVerticalFont;

        public bool IsSystemFont => false;

        public string SystemFontName => string.Empty;

        /// <summary>Size in points (pixel sizes are converted at 96 dpi).</summary>
        public float SizeInPoints
        {
            get
            {
                return _unit switch
                {
                    GraphicsUnit.Point => _size,
                    GraphicsUnit.Inch => _size * 72f,
                    GraphicsUnit.Document => _size * 72f / 300f,
                    GraphicsUnit.Millimeter => _size * 72f / 25.4f,
                    _ => _size * 72f / 96f,
                };
            }
        }

        /// <summary>Em height in device pixels at the given resolution.</summary>
        public float GetSizeInPixels(float dpi)
        {
            return _unit switch
            {
                GraphicsUnit.Point => _size * dpi / 72f,
                GraphicsUnit.Inch => _size * dpi,
                GraphicsUnit.Document => _size * dpi / 300f,
                GraphicsUnit.Millimeter => _size * dpi / 25.4f,
                _ => _size,
            };
        }

        /// <summary>Line spacing (ascent + descent + line gap) in pixels at the given resolution.</summary>
        public float GetHeight(float dpi)
        {
            return TextEngine.LineSpacing(new TextEngine.FontInfo(this, dpi));
        }

        public float GetHeight() => GetHeight(96f);

        public float GetHeight(Graphics graphics)
        {
            if (graphics == null)
            {
                throw new ArgumentNullException(nameof(graphics));
            }

            return GetHeight(graphics.DpiY);
        }

        public int Height => (int)Math.Ceiling(GetHeight());

        public object Clone() => new Font(_family, _size, _style, _unit, _gdiCharSet, _gdiVerticalFont);

        public void Dispose()
        {
        }

        public override string ToString() => $"[{nameof(Font)}: Name={Name}, Size={_size}, Units={(int)_unit}, GdiCharSet={_gdiCharSet}, GdiVerticalFont={_gdiVerticalFont}]";
    }

    public static class SystemFonts
    {
        public static Font DefaultFont => new Font("Segoe UI", 9f);

        public static Font DialogFont => new Font("Segoe UI", 9f);

        public static Font CaptionFont => new Font("Segoe UI", 9f);

        public static Font SmallCaptionFont => new Font("Segoe UI", 9f);

        public static Font MenuFont => new Font("Segoe UI", 9f);

        public static Font StatusFont => new Font("Segoe UI", 9f);

        public static Font MessageBoxFont => new Font("Segoe UI", 9f);

        public static Font IconTitleFont => new Font("Segoe UI", 9f);

        public static Font GetFontByName(string systemFontName) => new Font("Segoe UI", 9f);
    }
}

namespace System.Drawing.Text
{
    public abstract class FontCollection : IDisposable
    {
        public abstract FontFamily[] Families { get; }

        public void Dispose()
        {
        }
    }

    public sealed class InstalledFontCollection : FontCollection
    {
        public override FontFamily[] Families => FontFamily.Families;
    }
}
