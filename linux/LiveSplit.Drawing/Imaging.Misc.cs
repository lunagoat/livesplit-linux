using System.Collections.Generic;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing.Imaging
{
    /// <summary>5x5 GDI+ colour matrix (row vector [R G B A 1] times matrix).</summary>
    public sealed class ColorMatrix
    {
        private readonly float[][] _m;

        public ColorMatrix()
        {
            _m = new float[5][];
            for (int i = 0; i < 5; i++)
            {
                _m[i] = new float[5];
                _m[i][i] = 1f;
            }
        }

        public ColorMatrix(float[][] newColorMatrix)
        {
            if (newColorMatrix == null || newColorMatrix.Length != 5)
            {
                throw new ArgumentException("A 5x5 matrix is required.", nameof(newColorMatrix));
            }

            _m = new float[5][];
            for (int i = 0; i < 5; i++)
            {
                if (newColorMatrix[i] == null || newColorMatrix[i].Length != 5)
                {
                    throw new ArgumentException("A 5x5 matrix is required.", nameof(newColorMatrix));
                }

                _m[i] = (float[])newColorMatrix[i].Clone();
            }
        }

        public float this[int row, int column]
        {
            get => _m[row][column];
            set => _m[row][column] = value;
        }

        public float Matrix00
        {
            get => _m[0][0];
            set => _m[0][0] = value;
        }

        public float Matrix01
        {
            get => _m[0][1];
            set => _m[0][1] = value;
        }

        public float Matrix02
        {
            get => _m[0][2];
            set => _m[0][2] = value;
        }

        public float Matrix03
        {
            get => _m[0][3];
            set => _m[0][3] = value;
        }

        public float Matrix04
        {
            get => _m[0][4];
            set => _m[0][4] = value;
        }

        public float Matrix10
        {
            get => _m[1][0];
            set => _m[1][0] = value;
        }

        public float Matrix11
        {
            get => _m[1][1];
            set => _m[1][1] = value;
        }

        public float Matrix12
        {
            get => _m[1][2];
            set => _m[1][2] = value;
        }

        public float Matrix13
        {
            get => _m[1][3];
            set => _m[1][3] = value;
        }

        public float Matrix14
        {
            get => _m[1][4];
            set => _m[1][4] = value;
        }

        public float Matrix20
        {
            get => _m[2][0];
            set => _m[2][0] = value;
        }

        public float Matrix21
        {
            get => _m[2][1];
            set => _m[2][1] = value;
        }

        public float Matrix22
        {
            get => _m[2][2];
            set => _m[2][2] = value;
        }

        public float Matrix23
        {
            get => _m[2][3];
            set => _m[2][3] = value;
        }

        public float Matrix24
        {
            get => _m[2][4];
            set => _m[2][4] = value;
        }

        public float Matrix30
        {
            get => _m[3][0];
            set => _m[3][0] = value;
        }

        public float Matrix31
        {
            get => _m[3][1];
            set => _m[3][1] = value;
        }

        public float Matrix32
        {
            get => _m[3][2];
            set => _m[3][2] = value;
        }

        public float Matrix33
        {
            get => _m[3][3];
            set => _m[3][3] = value;
        }

        public float Matrix34
        {
            get => _m[3][4];
            set => _m[3][4] = value;
        }

        public float Matrix40
        {
            get => _m[4][0];
            set => _m[4][0] = value;
        }

        public float Matrix41
        {
            get => _m[4][1];
            set => _m[4][1] = value;
        }

        public float Matrix42
        {
            get => _m[4][2];
            set => _m[4][2] = value;
        }

        public float Matrix43
        {
            get => _m[4][3];
            set => _m[4][3] = value;
        }

        public float Matrix44
        {
            get => _m[4][4];
            set => _m[4][4] = value;
        }

        /// <summary>Converts to Skia's 4x5 row-major colour matrix (translation in 0..1 range).</summary>
        internal float[] ToSkiaMatrix()
        {
            var s = new float[20];
            for (int outC = 0; outC < 4; outC++)
            {
                for (int inC = 0; inC < 4; inC++)
                {
                    s[(outC * 5) + inC] = _m[inC][outC];
                }

                s[(outC * 5) + 4] = _m[4][outC];
            }

            return s;
        }

        internal bool IsIdentity
        {
            get
            {
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        if (_m[r][c] != (r == c ? 1f : 0f))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }

    public sealed class ImageAttributes : ICloneable, IDisposable
    {
        private ColorMatrix _matrix;
        private float? _gamma;
        private WrapMode? _wrapMode;
        private Color? _colorKeyLow, _colorKeyHigh;

        public ImageAttributes()
        {
        }

        private WrapMode WrapModeValue => _wrapMode ?? WrapMode.Tile;

        public void SetColorMatrix(ColorMatrix newColorMatrix) => SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

        public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag flags) => SetColorMatrix(newColorMatrix, flags, ColorAdjustType.Default);

        public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type)
        {
            _matrix = newColorMatrix ?? throw new ArgumentNullException(nameof(newColorMatrix));
        }

        public void ClearColorMatrix() => _matrix = null;

        public void ClearColorMatrix(ColorAdjustType type) => _matrix = null;

        public void SetGamma(float gamma) => _gamma = gamma;

        public void SetGamma(float gamma, ColorAdjustType type) => _gamma = gamma;

        public void ClearGamma() => _gamma = null;

        public void ClearGamma(ColorAdjustType type) => _gamma = null;

        public void SetColorKey(Color colorLow, Color colorHigh)
        {
            _colorKeyLow = colorLow;
            _colorKeyHigh = colorHigh;
        }

        public void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type) => SetColorKey(colorLow, colorHigh);

        public void ClearColorKey()
        {
            _colorKeyLow = _colorKeyHigh = null;
        }

        public void ClearColorKey(ColorAdjustType type) => ClearColorKey();

        public void SetWrapMode(WrapMode mode) => _wrapMode = mode;

        public void SetWrapMode(WrapMode mode, Color color) => _wrapMode = mode;

        public void SetWrapMode(WrapMode mode, Color color, bool clamp) => _wrapMode = mode;

        public void SetNoOp()
        {
        }

        public void ClearNoOp()
        {
        }

        public void SetThreshold(float threshold)
        {
        }

        public void SetRemapTable(ColorAdjustType type, ColorMap[] map)
        {
        }

        public void ClearRemapTable()
        {
        }

        public void ClearBrushRemapTable()
        {
        }

        public void ClearAll()
        {
            _matrix = null;
            _gamma = null;
            _wrapMode = null;
            ClearColorKey();
        }

        public object Clone()
        {
            return new ImageAttributes
            {
                _matrix = _matrix == null ? null : new ColorMatrix(Rows(_matrix)),
                _gamma = _gamma,
                _wrapMode = _wrapMode,
                _colorKeyLow = _colorKeyLow,
                _colorKeyHigh = _colorKeyHigh,
            };
        }

        private static float[][] Rows(ColorMatrix m)
        {
            var rows = new float[5][];
            for (int r = 0; r < 5; r++)
            {
                rows[r] = new float[5];
                for (int c = 0; c < 5; c++)
                {
                    rows[r][c] = m[r, c];
                }
            }

            return rows;
        }

        public void Dispose()
        {
        }

        internal bool ClampEdges => _wrapMode == WrapMode.Clamp;

        /// <summary>Builds the Skia colour filter for the attributes (null if they are a no-op).</summary>
        internal SKColorFilter CreateColorFilter()
        {
            SKColorFilter filter = null;
            if (_matrix != null && !_matrix.IsIdentity)
            {
                filter = SKColorFilter.CreateColorMatrix(_matrix.ToSkiaMatrix());
            }

            if (_gamma.HasValue && Math.Abs(_gamma.Value - 1f) > 1e-4f)
            {
                var table = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    table[i] = (byte)Math.Clamp((int)Math.Round(255.0 * Math.Pow(i / 255.0, 1.0 / _gamma.Value)), 0, 255);
                }

                SKColorFilter gamma = SKColorFilter.CreateTable(null, table, table, table);
                filter = filter == null ? gamma : SKColorFilter.CreateCompose(gamma, filter);
            }

            return filter;
        }
    }

    public sealed class ColorMap
    {
        public Color OldColor { get; set; }

        public Color NewColor { get; set; }
    }

    public sealed class BitmapData
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int Stride { get; set; }

        public PixelFormat PixelFormat { get; set; }

        public IntPtr Scan0 { get; set; }

        public int Reserved { get; set; }
    }

    public sealed class FrameDimension
    {
        private static readonly FrameDimension TimeDim = new FrameDimension(new Guid("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));
        private static readonly FrameDimension ResolutionDim = new FrameDimension(new Guid("84236f7b-3bd3-428f-8dab-4ea1439ca315"));
        private static readonly FrameDimension PageDim = new FrameDimension(new Guid("7462dc86-6180-4c7e-8e3f-ee7333a7a483"));

        public FrameDimension(Guid guid)
        {
            Guid = guid;
        }

        public Guid Guid { get; }

        public static FrameDimension Time => TimeDim;

        public static FrameDimension Resolution => ResolutionDim;

        public static FrameDimension Page => PageDim;

        public override bool Equals(object o) => o is FrameDimension f && f.Guid == Guid;

        public override int GetHashCode() => Guid.GetHashCode();

        public override string ToString()
        {
            if (this == TimeDim || Guid == TimeDim.Guid)
            {
                return "Time";
            }

            return Guid == ResolutionDim.Guid ? "Resolution" : Guid == PageDim.Guid ? "Page" : "[FrameDimension: " + Guid + "]";
        }
    }

    public sealed class ImageFormat
    {
        private static readonly ImageFormat MemoryBmpFormat = new ImageFormat(new Guid("b96b3caa-0728-11d3-9d7b-0000f81ef32e"), "MemoryBMP");
        private static readonly ImageFormat BmpFormat = new ImageFormat(new Guid("b96b3cab-0728-11d3-9d7b-0000f81ef32e"), "Bmp");
        private static readonly ImageFormat EmfFormat = new ImageFormat(new Guid("b96b3cac-0728-11d3-9d7b-0000f81ef32e"), "Emf");
        private static readonly ImageFormat WmfFormat = new ImageFormat(new Guid("b96b3cad-0728-11d3-9d7b-0000f81ef32e"), "Wmf");
        private static readonly ImageFormat JpegFormat = new ImageFormat(new Guid("b96b3cae-0728-11d3-9d7b-0000f81ef32e"), "Jpeg");
        private static readonly ImageFormat PngFormat = new ImageFormat(new Guid("b96b3caf-0728-11d3-9d7b-0000f81ef32e"), "Png");
        private static readonly ImageFormat GifFormat = new ImageFormat(new Guid("b96b3cb0-0728-11d3-9d7b-0000f81ef32e"), "Gif");
        private static readonly ImageFormat TiffFormat = new ImageFormat(new Guid("b96b3cb1-0728-11d3-9d7b-0000f81ef32e"), "Tiff");
        private static readonly ImageFormat ExifFormat = new ImageFormat(new Guid("b96b3cb2-0728-11d3-9d7b-0000f81ef32e"), "Exif");
        private static readonly ImageFormat IconFormat = new ImageFormat(new Guid("b96b3cb5-0728-11d3-9d7b-0000f81ef32e"), "Icon");
        private static readonly ImageFormat WebpFormat = new ImageFormat(new Guid("b96b3cb7-0728-11d3-9d7b-0000f81ef32e"), "Webp");

        private readonly string _name;

        public ImageFormat(Guid guid)
            : this(guid, null)
        {
        }

        private ImageFormat(Guid guid, string name)
        {
            Guid = guid;
            _name = name;
        }

        public Guid Guid { get; }

        public static ImageFormat MemoryBmp => MemoryBmpFormat;

        public static ImageFormat Bmp => BmpFormat;

        public static ImageFormat Emf => EmfFormat;

        public static ImageFormat Wmf => WmfFormat;

        public static ImageFormat Gif => GifFormat;

        public static ImageFormat Jpeg => JpegFormat;

        public static ImageFormat Png => PngFormat;

        public static ImageFormat Tiff => TiffFormat;

        public static ImageFormat Exif => ExifFormat;

        public static ImageFormat Icon => IconFormat;

        /// <summary>Extension (not in System.Drawing): WebP.</summary>
        public static ImageFormat Webp => WebpFormat;

        public override bool Equals(object o) => o is ImageFormat f && f.Guid == Guid;

        public override int GetHashCode() => Guid.GetHashCode();

        public override string ToString()
        {
            if (_name != null)
            {
                return _name;
            }

            foreach (ImageFormat f in new[] { MemoryBmpFormat, BmpFormat, EmfFormat, WmfFormat, GifFormat, JpegFormat, PngFormat, TiffFormat, ExifFormat, IconFormat, WebpFormat })
            {
                if (f.Guid == Guid)
                {
                    return f._name;
                }
            }

            return "[ImageFormat: " + Guid + "]";
        }

        internal static ImageFormat FromExtension(string path)
        {
            string ext = System.IO.Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" or ".jpe" or ".jfif" => JpegFormat,
                ".gif" => GifFormat,
                ".bmp" or ".dib" => BmpFormat,
                ".ico" => IconFormat,
                ".tif" or ".tiff" => TiffFormat,
                ".webp" => WebpFormat,
                _ => PngFormat,
            };
        }
    }
}
