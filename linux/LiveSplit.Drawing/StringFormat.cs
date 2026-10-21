using System.Drawing.Text;

namespace System.Drawing
{
    public sealed class StringFormat : MarshalByRefObject, ICloneable, IDisposable
    {
        public StringFormat()
        {
        }

        public StringFormat(StringFormatFlags options)
        {
            FormatFlags = options;
        }

        public StringFormat(StringFormatFlags options, int language)
        {
            FormatFlags = options;
        }

        public StringFormat(StringFormat format)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            Alignment = format.Alignment;
            LineAlignment = format.LineAlignment;
            FormatFlags = format.FormatFlags;
            Trimming = format.Trimming;
            HotkeyPrefix = format.HotkeyPrefix;
            _tabStops = (float[])format._tabStops?.Clone();
            FirstTabOffset = format.FirstTabOffset;
        }

        private float[] _tabStops;

        public StringAlignment Alignment { get; set; }

        public StringAlignment LineAlignment { get; set; }

        public StringFormatFlags FormatFlags { get; set; }

        public StringTrimming Trimming { get; set; }

        public HotkeyPrefix HotkeyPrefix { get; set; }

        public int DigitSubstitutionLanguage => 0;

        internal float FirstTabOffset { get; private set; }

        internal float[] TabStops => _tabStops;

        public static StringFormat GenericDefault => new StringFormat();

        public static StringFormat GenericTypographic => new StringFormat(StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip)
        {
            Trimming = StringTrimming.None,
        };

        public void SetTabStops(float firstTabOffset, float[] tabStops)
        {
            FirstTabOffset = firstTabOffset;
            _tabStops = tabStops;
        }

        public float[] GetTabStops(out float firstTabOffset)
        {
            firstTabOffset = FirstTabOffset;
            return _tabStops ?? [];
        }

        public object Clone() => new StringFormat(this);

        public void Dispose()
        {
        }

        public override string ToString() => $"[StringFormat, FormatFlags={FormatFlags}]";
    }
}
