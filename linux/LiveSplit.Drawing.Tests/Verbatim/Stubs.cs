using System;
using System.Drawing;

namespace LiveSplit.Options
{
    public static class Log
    {
        public static void Error(Exception ex) => Console.Error.WriteLine(ex);
    }
}

namespace System.Windows.Forms
{
    [Flags]
    public enum TextFormatFlags { Default = 0, NoPadding = 0x10000000 }

    // Stub standing in for WinForms' TextRenderer (SimpleLabel's monospaced mode uses it).
    public static class TextRenderer
    {
        public static Size MeasureText(Graphics g, string text, Font font, Size proposedSize, TextFormatFlags flags)
        {
            SizeF s = g.MeasureString(text, font, new PointF(0, 0), StringFormat.GenericTypographic);
            return new Size((int)Math.Ceiling(s.Width), (int)Math.Ceiling(s.Height));
        }
    }
}
