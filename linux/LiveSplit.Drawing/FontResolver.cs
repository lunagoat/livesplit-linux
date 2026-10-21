using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Resolves Windows-style font family names to installed (fontconfig) typefaces.</summary>
    internal static class FontResolver
    {
        public sealed class Resolved
        {
            public SKTypeface Typeface;
            public bool SyntheticBold;
            public bool SyntheticItalic;
            public string ActualFamily;
        }

        private static readonly object Lock = new object();
        private static readonly Dictionary<(string, FontStyle), Resolved> Cache = new();
        private static string[] _families;
        private static HashSet<string> _familySet;

        // Windows font names that are commonly used by LiveSplit layouts and have no direct Linux equivalent.
        private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Segoe UI"] = ["Noto Sans", "Open Sans", "Cantarell", "DejaVu Sans", "Liberation Sans"],
            ["Segoe UI Semibold"] = ["Noto Sans", "Open Sans", "DejaVu Sans"],
            ["Segoe UI Light"] = ["Noto Sans", "Open Sans", "DejaVu Sans"],
            ["Microsoft Sans Serif"] = ["Liberation Sans", "Arimo", "Arial", "DejaVu Sans"],
            ["MS Sans Serif"] = ["Liberation Sans", "Arimo", "Arial", "DejaVu Sans"],
            ["MS Shell Dlg"] = ["Liberation Sans", "Noto Sans", "DejaVu Sans"],
            ["MS Shell Dlg 2"] = ["Noto Sans", "Liberation Sans", "DejaVu Sans"],
            ["Tahoma"] = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Verdana"] = ["DejaVu Sans", "Bitstream Vera Sans", "Noto Sans"],
            ["Arial"] = ["Liberation Sans", "Arimo", "Helvetica", "Nimbus Sans", "DejaVu Sans"],
            ["Helvetica"] = ["Liberation Sans", "Nimbus Sans", "Arimo", "DejaVu Sans"],
            ["Calibri"] = ["Carlito", "Noto Sans", "Open Sans", "DejaVu Sans"],
            ["Cambria"] = ["Caladea", "Noto Serif", "DejaVu Serif"],
            ["Century Gothic"] = ["URW Gothic", "TeX Gyre Adventor", "Noto Sans", "DejaVu Sans"],
            ["Franklin Gothic Medium"] = ["Noto Sans", "Liberation Sans", "DejaVu Sans"],
            ["Trebuchet MS"] = ["Noto Sans", "DejaVu Sans"],
            ["Times New Roman"] = ["Liberation Serif", "Tinos", "Nimbus Roman", "DejaVu Serif"],
            ["Georgia"] = ["DejaVu Serif", "Noto Serif", "Liberation Serif"],
            ["Courier New"] = ["Liberation Mono", "Cousine", "Nimbus Mono PS", "DejaVu Sans Mono"],
            ["Consolas"] = ["DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono"],
            ["Lucida Console"] = ["DejaVu Sans Mono", "Liberation Mono"],
            ["Impact"] = ["Anton", "Noto Sans", "DejaVu Sans"],
            ["Comic Sans MS"] = ["Comic Neue", "Noto Sans", "DejaVu Sans"],
            ["Microsoft YaHei"] = ["Noto Sans CJK SC", "Noto Sans SC", "Noto Sans"],
            ["Meiryo"] = ["Noto Sans CJK JP", "Noto Sans JP", "Noto Sans"],
            ["MS Gothic"] = ["Noto Sans CJK JP", "Noto Sans Mono CJK JP", "Noto Sans JP"],
            ["Sans Serif"] = ["Noto Sans", "DejaVu Sans", "Liberation Sans"],
            ["sans-serif"] = ["Noto Sans", "DejaVu Sans", "Liberation Sans"],
            ["Serif"] = ["Noto Serif", "DejaVu Serif", "Liberation Serif"],
            ["serif"] = ["Noto Serif", "DejaVu Serif", "Liberation Serif"],
            ["Monospace"] = ["DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono"],
            ["monospace"] = ["DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono"],
        };

        private static readonly string[] LastResort = ["Noto Sans", "DejaVu Sans", "Liberation Sans", "Open Sans", "Cantarell"];

        public static string[] InstalledFamilies
        {
            get
            {
                EnsureFamilies();
                return _families;
            }
        }

        private static void EnsureFamilies()
        {
            if (_families != null)
            {
                return;
            }

            lock (Lock)
            {
                if (_families != null)
                {
                    return;
                }

                string[] raw;
                try
                {
                    raw = SKFontManager.Default.FontFamilies.ToArray();
                }
                catch
                {
                    raw = [];
                }

                _families = raw
                    .Where(n => !string.IsNullOrWhiteSpace(n) && !n.EndsWith(".pcf", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".pcf.gz", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                _familySet = new HashSet<string>(_families, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static bool IsInstalled(string name)
        {
            EnsureFamilies();
            return _familySet.Contains(name);
        }

        private static SKFontStyle ToSKStyle(FontStyle style)
        {
            return new SKFontStyle(
                (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        }

        private static SKTypeface TryExact(string family, FontStyle style)
        {
            if (string.IsNullOrEmpty(family))
            {
                return null;
            }

            SKTypeface tf = SKFontManager.Default.MatchFamily(family, ToSKStyle(style));
            if (tf == null)
            {
                return null;
            }

            if (string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                return tf;
            }

            tf.Dispose();
            return null;
        }

        public static Resolved Resolve(string family, FontStyle style)
        {
            style &= FontStyle.Bold | FontStyle.Italic;
            lock (Lock)
            {
                var key = (family ?? string.Empty, style);
                if (Cache.TryGetValue(key, out Resolved cached))
                {
                    return cached;
                }

                Resolved r = ResolveUncached(family, style);
                Cache[key] = r;
                return r;
            }
        }

        private static Resolved ResolveUncached(string family, FontStyle style)
        {
            SKTypeface tf = TryExact(family, style);

            if (tf == null && family != null && Aliases.TryGetValue(family, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    tf = TryExact(alias, style);
                    if (tf != null)
                    {
                        break;
                    }
                }
            }

            if (tf == null && !string.IsNullOrEmpty(family))
            {
                // Let fontconfig substitute (it may map e.g. "Arial" to a metric-compatible font).
                tf = SKFontManager.Default.MatchFamily(family, ToSKStyle(style));
            }

            if (tf == null)
            {
                foreach (string name in LastResort)
                {
                    tf = TryExact(name, style);
                    if (tf != null)
                    {
                        break;
                    }
                }
            }

            tf ??= SKFontManager.Default.MatchFamily(null, ToSKStyle(style)) ?? SKTypeface.Default;

            var res = new Resolved
            {
                Typeface = tf,
                ActualFamily = tf.FamilyName,
                SyntheticBold = (style & FontStyle.Bold) != 0 && tf.FontWeight < 600,
                SyntheticItalic = (style & FontStyle.Italic) != 0 && tf.FontSlant == SKFontStyleSlant.Upright,
            };
            return res;
        }

        /// <summary>Whether the installed family offers a real face for the style (bold/italic/regular).</summary>
        public static bool IsStyleAvailable(string family, FontStyle style)
        {
            Resolved r = Resolve(family, style & (FontStyle.Bold | FontStyle.Italic));
            if (r == null)
            {
                return false;
            }

            if ((style & (FontStyle.Bold | FontStyle.Italic)) == 0)
            {
                // Regular is available whenever the family exists (as in GDI+, fall back to the closest face).
                return true;
            }

            bool bold = (style & FontStyle.Bold) != 0;
            bool italic = (style & FontStyle.Italic) != 0;
            return (!bold || !r.SyntheticBold) && (!italic || !r.SyntheticItalic);
        }
    }
}
