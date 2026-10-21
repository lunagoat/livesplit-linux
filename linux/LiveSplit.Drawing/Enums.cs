using System;

namespace System.Drawing
{
    [Flags]
    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8,
    }

    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6,
    }

    public enum StringAlignment
    {
        Near = 0,
        Center = 1,
        Far = 2,
    }

    public enum StringTrimming
    {
        None = 0,
        Character = 1,
        Word = 2,
        EllipsisCharacter = 3,
        EllipsisWord = 4,
        EllipsisPath = 5,
    }

    [Flags]
    public enum StringFormatFlags
    {
        DirectionRightToLeft = 0x1,
        DirectionVertical = 0x2,
        FitBlackBox = 0x4,
        DisplayFormatControl = 0x20,
        NoFontFallback = 0x400,
        MeasureTrailingSpaces = 0x800,
        NoWrap = 0x1000,
        LineLimit = 0x2000,
        NoClip = 0x4000,
    }

    public enum ContentAlignment
    {
        TopLeft = 0x001,
        TopCenter = 0x002,
        TopRight = 0x004,
        MiddleLeft = 0x010,
        MiddleCenter = 0x020,
        MiddleRight = 0x040,
        BottomLeft = 0x100,
        BottomCenter = 0x200,
        BottomRight = 0x400,
    }

    public enum RotateFlipType
    {
        RotateNoneFlipNone = 0,
        Rotate90FlipNone = 1,
        Rotate180FlipNone = 2,
        Rotate270FlipNone = 3,
        RotateNoneFlipX = 4,
        Rotate90FlipX = 5,
        Rotate180FlipX = 6,
        Rotate270FlipX = 7,
        Rotate180FlipXY = 0,
        Rotate270FlipXY = 1,
        Rotate180FlipY = 4,
        Rotate270FlipY = 5,
        RotateNoneFlipXY = 2,
        Rotate90FlipXY = 3,
        RotateNoneFlipY = 6,
        Rotate90FlipY = 7,
    }
}

namespace System.Drawing.Text
{
    public enum TextRenderingHint
    {
        SystemDefault = 0,
        SingleBitPerPixelGridFit = 1,
        SingleBitPerPixel = 2,
        AntiAliasGridFit = 3,
        AntiAlias = 4,
        ClearTypeGridFit = 5,
    }

    public enum GenericFontFamilies
    {
        Serif = 0,
        SansSerif = 1,
        Monospace = 2,
    }

    public enum HotkeyPrefix
    {
        None = 0,
        Show = 1,
        Hide = 2,
    }
}

namespace System.Drawing.Drawing2D
{
    public enum MatrixOrder
    {
        Prepend = 0,
        Append = 1,
    }

    public enum SmoothingMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        AntiAlias = 4,
    }

    public enum InterpolationMode
    {
        Invalid = -1,
        Default = 0,
        Low = 1,
        High = 2,
        Bilinear = 3,
        Bicubic = 4,
        NearestNeighbor = 5,
        HighQualityBilinear = 6,
        HighQualityBicubic = 7,
    }

    public enum CompositingMode
    {
        SourceOver = 0,
        SourceCopy = 1,
    }

    public enum CompositingQuality
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        GammaCorrected = 3,
        AssumeLinear = 4,
    }

    public enum PixelOffsetMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        Half = 4,
    }

    public enum FillMode
    {
        Alternate = 0,
        Winding = 1,
    }

    public enum LineJoin
    {
        Miter = 0,
        Bevel = 1,
        Round = 2,
        MiterClipped = 3,
    }

    public enum LineCap
    {
        Flat = 0,
        Square = 1,
        Round = 2,
        Triangle = 3,
        NoAnchor = 0x10,
        SquareAnchor = 0x11,
        RoundAnchor = 0x12,
        DiamondAnchor = 0x13,
        ArrowAnchor = 0x14,
        AnchorMask = 0xF0,
        Custom = 0xFF,
    }

    public enum DashCap
    {
        Flat = 0,
        Round = 2,
        Triangle = 3,
    }

    public enum DashStyle
    {
        Solid = 0,
        Dash = 1,
        Dot = 2,
        DashDot = 3,
        DashDotDot = 4,
        Custom = 5,
    }

    public enum PenAlignment
    {
        Center = 0,
        Inset = 1,
        Outset = 2,
        Left = 3,
        Right = 4,
    }

    public enum PenType
    {
        SolidColor = 0,
        HatchFill = 1,
        TextureFill = 2,
        PathGradient = 3,
        LinearGradient = 4,
    }

    public enum LinearGradientMode
    {
        Horizontal = 0,
        Vertical = 1,
        ForwardDiagonal = 2,
        BackwardDiagonal = 3,
    }

    public enum WrapMode
    {
        Tile = 0,
        TileFlipX = 1,
        TileFlipY = 2,
        TileFlipXY = 3,
        Clamp = 4,
    }

    public enum CombineMode
    {
        Replace = 0,
        Intersect = 1,
        Union = 2,
        Xor = 3,
        Exclude = 4,
        Complement = 5,
    }

    public enum CoordinateSpace
    {
        World = 0,
        Page = 1,
        Device = 2,
    }

    public enum FlushIntention
    {
        Flush = 0,
        Sync = 1,
    }
}

namespace System.Drawing.Imaging
{
    [Flags]
    public enum ImageLockMode
    {
        ReadOnly = 1,
        WriteOnly = 2,
        ReadWrite = 3,
        UserInputBuffer = 4,
    }

    public enum PixelFormat
    {
        DontCare = 0,
        Undefined = 0,
        Max = 0xF,
        Indexed = 0x00010000,
        Gdi = 0x00020000,
        Format16bppRgb555 = 0x00021005,
        Format16bppRgb565 = 0x00021006,
        Format24bppRgb = 0x00021808,
        Format32bppRgb = 0x00022009,
        Format1bppIndexed = 0x00030101,
        Format4bppIndexed = 0x00030402,
        Format8bppIndexed = 0x00030803,
        Alpha = 0x00040000,
        Format16bppArgb1555 = 0x00061007,
        PAlpha = 0x00080000,
        Format32bppPArgb = 0x000E200B,
        Extended = 0x00100000,
        Format16bppGrayScale = 0x00101004,
        Format48bppRgb = 0x0010300C,
        Format64bppPArgb = 0x001A400E,
        Canonical = 0x00200000,
        Format32bppArgb = 0x0026200A,
        Format64bppArgb = 0x0034400D,
    }

    public enum ColorMatrixFlag
    {
        Default = 0,
        SkipGrays = 1,
        AltGrays = 2,
    }

    public enum ColorAdjustType
    {
        Default = 0,
        Bitmap = 1,
        Brush = 2,
        Pen = 3,
        Text = 4,
        Count = 5,
        Any = 6,
    }

    public enum ColorChannelFlag
    {
        ColorChannelC = 0,
        ColorChannelM = 1,
        ColorChannelY = 2,
        ColorChannelK = 3,
        ColorChannelLast = 4,
    }

    public enum ImageFlags
    {
        None = 0,
        Scalable = 1,
        HasAlpha = 2,
        HasTranslucent = 4,
        PartiallyScalable = 8,
        ColorSpaceRgb = 16,
        ColorSpaceCmyk = 32,
        ColorSpaceGray = 64,
        ColorSpaceYcbcr = 128,
        ColorSpaceYcck = 256,
        ReadOnly = 65536,
        Caching = 131072,
    }
}
