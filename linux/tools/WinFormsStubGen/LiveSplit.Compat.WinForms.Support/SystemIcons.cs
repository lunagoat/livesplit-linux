using System;
using System.Drawing;
using System.IO;

namespace System.Drawing
{
    public static class SystemIcons
    {
        // 1x1 transparent PNG bytes that decode to a valid Bitmap in Skia
        private static readonly byte[] TinyPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        private static Icon CreateIcon()
        {
            return new Icon(new MemoryStream(TinyPng));
        }

        private static Icon _application;
        private static Icon _asterisk;
        private static Icon _error;
        private static Icon _exclamation;
        private static Icon _hand;
        private static Icon _information;
        private static Icon _question;
        private static Icon _shield;
        private static Icon _warning;
        private static Icon _winLogo;

        public static Icon Application => _application ??= CreateIcon();
        public static Icon Asterisk => _asterisk ??= CreateIcon();
        public static Icon Error => _error ??= CreateIcon();
        public static Icon Exclamation => _exclamation ??= CreateIcon();
        public static Icon Hand => _hand ??= CreateIcon();
        public static Icon Information => _information ??= CreateIcon();
        public static Icon Question => _question ??= CreateIcon();
        public static Icon Shield => _shield ??= CreateIcon();
        public static Icon Warning => _warning ??= CreateIcon();
        public static Icon WinLogo => _winLogo ??= CreateIcon();
    }
}
