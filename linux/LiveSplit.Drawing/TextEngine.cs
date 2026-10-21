using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>GDI+-like text layout (wrapping, trimming, alignment, em/6 side bearings) on top of Skia fonts.</summary>
    internal static class TextEngine
    {
        public readonly struct FontInfo
        {
            public readonly string FamilyName;
            public readonly FontStyle Style;
            public readonly float EmPx;

            public FontInfo(FontFamily family, FontStyle style, float emPx)
            {
                FamilyName = family.Name;
                Style = style;
                EmPx = emPx;
            }

            public FontInfo(Font font, float dpi)
            {
                FamilyName = font.FontFamily.Name;
                Style = font.Style;
                EmPx = font.GetSizeInPixels(dpi);
            }
        }

        public sealed class Run
        {
            public string Text;
            public SKFont Font;
            public SKFont FontAliased;
            public float Width;
        }

        private static Run MakeRun(string text, SKTypeface tf, float em, bool synthBold, bool synthItalic)
        {
            SKFont aa = GetSKFont(tf, em, synthBold, synthItalic, false);
            return new Run
            {
                Text = text,
                Font = aa,
                FontAliased = GetSKFont(tf, em, synthBold, synthItalic, true),
                Width = aa.MeasureText(text),
            };
        }

        public sealed class Line
        {
            public List<Run> Runs = new();
            public string Text;
            public float X;
            public float Baseline;
            public float Width;
        }

        public sealed class Layout
        {
            public List<Line> Lines = new();
            public float Width;
            public float Height;
            public float LineHeight;
            public float Ascent;
            public float Margin;
            public int CharsFitted;
            public RectangleF Rect;
            public bool Clip;
        }

        private const float Unbounded = 1e7f;

        // ---------------------------------------------------------------- fonts

        private static readonly object FontLock = new();
        private static readonly Dictionary<(SKTypeface, int, bool, bool, bool), SKFont> FontCache = new();
        private static readonly Dictionary<int, SKTypeface> FallbackByCodepoint = new();

        public static SKFont GetSKFont(SKTypeface tf, float emPx, bool synthBold, bool synthItalic, bool aliased)
        {
            // Quantise to 1/64 px so equal sizes share a font.
            int q = (int)Math.Round(Math.Max(0.01f, emPx) * 64f);
            lock (FontLock)
            {
                var key = (tf, q, synthBold, synthItalic, aliased);
                if (!FontCache.TryGetValue(key, out SKFont font))
                {
                    if (FontCache.Count > 1024)
                    {
                        FontCache.Clear();
                    }

                    font = new SKFont(tf, q / 64f)
                    {
                        Edging = aliased ? SKFontEdging.Alias : SKFontEdging.Antialias,
                        Hinting = SKFontHinting.Slight,
                        Subpixel = true,
                        LinearMetrics = true,
                        ForceAutoHinting = false,
                        Embolden = synthBold,
                        SkewX = synthItalic ? -0.25f : 0f,
                    };
                    FontCache[key] = font;
                }

                return font;
            }
        }

        public static SKFont GetSKFont(FontInfo info, bool aliased = false)
        {
            FontResolver.Resolved r = FontResolver.Resolve(info.FamilyName, info.Style);
            return GetSKFont(r.Typeface, info.EmPx, r.SyntheticBold, r.SyntheticItalic, aliased);
        }

        public static float LineSpacing(FontInfo info)
        {
            SKFontMetrics m = GetSKFont(info).Metrics;
            return -m.Ascent + m.Descent + m.Leading;
        }

        private static SKTypeface FallbackFor(int codepoint, FontStyle style)
        {
            lock (FontLock)
            {
                if (FallbackByCodepoint.TryGetValue(codepoint, out SKTypeface tf))
                {
                    return tf;
                }

                try
                {
                    var skStyle = new SKFontStyle(
                        (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                        SKFontStyleWidth.Normal,
                        (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
                    tf = SKFontManager.Default.MatchCharacter(null, skStyle, null, codepoint);
                }
                catch
                {
                    tf = null;
                }

                if (FallbackByCodepoint.Count > 4096)
                {
                    FallbackByCodepoint.Clear();
                }

                FallbackByCodepoint[codepoint] = tf;
                return tf;
            }
        }

        private static int CodePointAt(string s, int i, out int len)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                len = 2;
                return char.ConvertToUtf32(s[i], s[i + 1]);
            }

            len = 1;
            return s[i];
        }

        private static bool NeedsNoGlyph(int cp) => cp < 0x20 || (cp >= 0x7F && cp < 0xA0) || (cp >= 0x200B && cp <= 0x200F) || cp == 0xFEFF || cp == 0x2028 || cp == 0x2029;

        // ---------------------------------------------------------------- shaping

        /// <summary>Splits a line into runs that each use a font that actually has the glyphs (fallback fonts for the rest).</summary>
        public static List<Run> Shape(string text, FontInfo info, bool aliased)
        {
            var runs = new List<Run>();
            if (string.IsNullOrEmpty(text))
            {
                return runs;
            }

            FontResolver.Resolved r = FontResolver.Resolve(info.FamilyName, info.Style);
            SKFont main = GetSKFont(r.Typeface, info.EmPx, r.SyntheticBold, r.SyntheticItalic, false);

            ushort[] glyphs = main.GetGlyphs(text);
            bool allPresent = true;
            int cpCount = 0;
            for (int i = 0; i < text.Length; cpCount++)
            {
                int cp = CodePointAt(text, i, out int len);
                if (cpCount < glyphs.Length && glyphs[cpCount] == 0 && !NeedsNoGlyph(cp))
                {
                    allPresent = false;
                }

                i += len;
            }

            if (allPresent || glyphs.Length != cpCount)
            {
                runs.Add(MakeRun(text, r.Typeface, info.EmPx, r.SyntheticBold, r.SyntheticItalic));
                return runs;
            }

            var sb = new System.Text.StringBuilder();
            SKTypeface curTf = null;
            bool curMain = true;
            int index = 0;
            for (int i = 0; i < text.Length; index++)
            {
                int cp = CodePointAt(text, i, out int len);
                string piece = text.Substring(i, len);
                SKTypeface tf = r.Typeface;
                bool isMain = true;
                if (glyphs[index] == 0 && !NeedsNoGlyph(cp))
                {
                    SKTypeface fb = FallbackFor(cp, info.Style);
                    if (fb != null)
                    {
                        tf = fb;
                        isMain = false;
                    }
                }

                if (curTf != null && (!ReferenceEquals(tf, curTf)))
                {
                    runs.Add(MakeRun(sb.ToString(), curTf, info.EmPx, curMain && r.SyntheticBold, curMain && r.SyntheticItalic));
                    sb.Clear();
                }

                curTf = tf;
                curMain = isMain;
                sb.Append(piece);
                i += len;
            }

            if (sb.Length > 0)
            {
                runs.Add(MakeRun(sb.ToString(), curTf, info.EmPx, curMain && r.SyntheticBold, curMain && r.SyntheticItalic));
            }

            return runs;
        }

        private static float Measure(string text, FontInfo info)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            float w = 0;
            foreach (Run run in Shape(text, info, false))
            {
                w += run.Width;
            }

            return w;
        }

        // ---------------------------------------------------------------- layout

        public static Layout DoLayout(string text, FontInfo info, StringFormat format, RectangleF rect, bool forDraw)
        {
            format ??= StringFormat.GenericDefault;
            var layout = new Layout();
            SKFontMetrics m = GetSKFont(info).Metrics;
            float asc = -m.Ascent;
            float lineH = asc + m.Descent + m.Leading;
            layout.LineHeight = lineH;
            layout.Ascent = asc;

            bool typographic = (format.FormatFlags & StringFormatFlags.FitBlackBox) != 0;
            float margin = typographic ? 0f : info.EmPx / 6f;
            layout.Margin = margin;

            float rx = float.IsNaN(rect.X) ? 0 : rect.X;
            float ry = float.IsNaN(rect.Y) ? 0 : rect.Y;
            float rw = float.IsNaN(rect.Width) ? 0 : Math.Min(Math.Max(rect.Width, 0), Unbounded);
            float rh = float.IsNaN(rect.Height) ? 0 : Math.Min(Math.Max(rect.Height, 0), Unbounded);
            bool boundedW = rw > 0 && rw < Unbounded;
            bool boundedH = rh > 0 && rh < Unbounded;
            layout.Rect = new RectangleF(rx, ry, rw, rh);
            layout.Clip = boundedW && boundedH && (format.FormatFlags & StringFormatFlags.NoClip) == 0;

            if (string.IsNullOrEmpty(text))
            {
                return layout;
            }

            float availW = boundedW ? Math.Max(0, rw - (2 * margin)) : float.PositiveInfinity;
            bool wrap = (format.FormatFlags & StringFormatFlags.NoWrap) == 0 && boundedW;
            bool keepTrailing = (format.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) != 0;

            // Break into paragraphs on explicit newlines.
            var lines = new List<string>();
            foreach (string para in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string p = para.Replace("\t", "    ");
                if (!wrap)
                {
                    lines.Add(TrimLine(p, info, format.Trimming, availW));
                }
                else
                {
                    WrapParagraph(p, info, availW, lines);
                }
            }

            // Drop trailing empty line produced by a terminating newline (GDI+ keeps it as an empty line; keep it only if not last).
            float maxW = 0;
            float totalHeight = 0;
            int emitted = 0;
            bool lineLimit = (format.FormatFlags & StringFormatFlags.LineLimit) != 0;
            for (int i = 0; i < lines.Count; i++)
            {
                float lineTop = i * lineH;
                if (boundedH && i > 0)
                {
                    if (lineTop >= rh)
                    {
                        break;
                    }

                    if (lineLimit && lineTop + lineH > rh)
                    {
                        break;
                    }
                }

                string s = lines[i];
                var line = new Line { Text = s, Runs = Shape(s, info, false) };
                float w = 0;
                foreach (Run r in line.Runs)
                {
                    w += r.Width;
                }

                line.Width = w;
                layout.Lines.Add(line);
                float measured = keepTrailing ? w : w - TrailingSpaceWidth(s, info);
                maxW = Math.Max(maxW, measured);
                emitted++;
                layout.CharsFitted += s.Length;
                totalHeight = emitted * lineH;
            }

            if (emitted == lines.Count)
            {
                layout.CharsFitted = text.Length;
            }

            layout.Height = totalHeight;
            float boxW = maxW + (2 * margin);
            layout.Width = boxW;
            if (boundedW)
            {
                layout.Width = Math.Min(boxW, rw);
            }

            if (boundedH)
            {
                layout.Height = Math.Min(totalHeight, Math.Max(rh, lineH));
            }

            // Position lines.
            float top = ry;
            float spaceV = boundedH ? rh : 0;
            if (format.LineAlignment == StringAlignment.Center)
            {
                top = ry + ((spaceV - totalHeight) / 2f);
            }
            else if (format.LineAlignment == StringAlignment.Far)
            {
                top = ry + spaceV - totalHeight;
            }

            for (int i = 0; i < layout.Lines.Count; i++)
            {
                Line line = layout.Lines[i];
                float spaceW = boundedW ? rw : 0;
                float shown = line.Width - (keepTrailing ? 0 : TrailingSpaceWidth(line.Text, info));
                float x;
                switch (format.Alignment)
                {
                    case StringAlignment.Center:
                        x = rx + ((spaceW - shown) / 2f);
                        break;
                    case StringAlignment.Far:
                        x = rx + spaceW - margin - shown;
                        break;
                    default:
                        x = rx + margin;
                        break;
                }

                line.X = x;
                line.Baseline = top + (i * lineH) + asc;
            }

            return layout;
        }

        private static float TrailingSpaceWidth(string s, FontInfo info)
        {
            int n = s.Length;
            int k = n;
            while (k > 0 && s[k - 1] == ' ')
            {
                k--;
            }

            return k == n ? 0 : Measure(s.Substring(k), info);
        }

        private static void WrapParagraph(string para, FontInfo info, float availW, List<string> output)
        {
            if (para.Length == 0)
            {
                output.Add(string.Empty);
                return;
            }

            if (Measure(para.TrimEnd(' '), info) <= availW)
            {
                output.Add(para);
                return;
            }

            int pos = 0;
            while (pos < para.Length)
            {
                // Skip leading spaces on continuation lines.
                if (output.Count > 0 && pos > 0)
                {
                    while (pos < para.Length && para[pos] == ' ')
                    {
                        pos++;
                    }

                    if (pos >= para.Length)
                    {
                        break;
                    }
                }

                int lineStart = pos;
                int lastGood = -1; // end index (exclusive, excluding trailing spaces) of the last word that fit
                int i = pos;
                bool brokeForWidth = false;
                while (i < para.Length)
                {
                    int wordEnd = i;
                    while (wordEnd < para.Length && para[wordEnd] != ' ')
                    {
                        wordEnd++;
                    }

                    string candidate = para.Substring(lineStart, wordEnd - lineStart);
                    if (Measure(candidate, info) <= availW)
                    {
                        lastGood = wordEnd;
                        i = wordEnd;
                        while (i < para.Length && para[i] == ' ')
                        {
                            i++;
                        }

                        if (i >= para.Length)
                        {
                            break;
                        }
                    }
                    else
                    {
                        brokeForWidth = true;
                        break;
                    }
                }

                if (!brokeForWidth && lastGood >= 0)
                {
                    output.Add(para.Substring(lineStart));
                    break;
                }

                if (lastGood < 0)
                {
                    // A single word does not fit: break it by text element.
                    int wordEnd = lineStart;
                    while (wordEnd < para.Length && para[wordEnd] != ' ')
                    {
                        wordEnd++;
                    }

                    int end = lineStart;
                    int e = lineStart;
                    while (e < wordEnd)
                    {
                        int len = StringInfo.GetNextTextElementLength(para, e);
                        if (Measure(para.Substring(lineStart, e + len - lineStart), info) > availW)
                        {
                            break;
                        }

                        e += len;
                        end = e;
                    }

                    if (end == lineStart)
                    {
                        // GDI+ always places at least one character on a line.
                        end = lineStart + StringInfo.GetNextTextElementLength(para, lineStart);
                    }

                    output.Add(para.Substring(lineStart, end - lineStart));
                    pos = end;
                }
                else
                {
                    output.Add(para.Substring(lineStart, lastGood - lineStart));
                    pos = lastGood;
                }
            }
        }

        private static string TrimLine(string line, FontInfo info, StringTrimming trimming, float availW)
        {
            if (float.IsPositiveInfinity(availW) || trimming == StringTrimming.None || line.Length == 0)
            {
                return line;
            }

            if (Measure(line, info) <= availW)
            {
                return line;
            }

            bool ellipsis = trimming is StringTrimming.EllipsisCharacter or StringTrimming.EllipsisWord or StringTrimming.EllipsisPath;
            string ell = ellipsis ? "…" : string.Empty;
            float ellW = ellipsis ? Measure(ell, info) : 0;
            if (ellipsis && ellW <= 0)
            {
                ell = "...";
                ellW = Measure(ell, info);
            }

            if (trimming == StringTrimming.EllipsisPath)
            {
                return TrimPath(line, info, availW, ell);
            }

            // Longest prefix (in text elements) that fits.
            var boundaries = new List<int>();
            for (int i = 0; i < line.Length;)
            {
                i += StringInfo.GetNextTextElementLength(line, i);
                boundaries.Add(i);
            }

            int lo = 0, hi = boundaries.Count; // prefix of 'lo' elements fits
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                float w = Measure(line.Substring(0, boundaries[mid - 1]), info) + ellW;
                if (w <= availW)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            int cut = lo == 0 ? 0 : boundaries[lo - 1];
            if (trimming is StringTrimming.Word or StringTrimming.EllipsisWord && cut > 0 && cut < line.Length)
            {
                int space = line.LastIndexOf(' ', Math.Max(0, cut - 1), cut);
                if (space > 0)
                {
                    cut = space;
                }
            }

            string head = line.Substring(0, cut).TrimEnd(' ');
            if (cut == 0 && !ellipsis)
            {
                // GDI+ always shows at least one character.
                head = line.Substring(0, boundaries[0]);
            }

            return head + ell;
        }

        private static string TrimPath(string line, FontInfo info, float availW, string ell)
        {
            int keepL = line.Length / 2, keepR = line.Length - keepL;
            string Build(int l, int r) => line.Substring(0, l) + ell + line.Substring(line.Length - r);
            int slash = Math.Max(line.LastIndexOf('\\'), line.LastIndexOf('/'));
            if (slash > 0)
            {
                keepR = line.Length - slash;
                keepL = slash;
            }

            while ((keepL > 0 || keepR > 0) && Measure(Build(keepL, keepR), info) > availW)
            {
                if (keepL >= keepR && keepL > 0)
                {
                    keepL--;
                }
                else if (keepR > 0)
                {
                    keepR--;
                }
            }

            return Build(keepL, keepR);
        }

        // ---------------------------------------------------------------- decorations

        public static IEnumerable<SKRect> DecorationRects(FontInfo info, Line line)
        {
            if ((info.Style & (FontStyle.Underline | FontStyle.Strikeout)) == 0 || line.Width <= 0)
            {
                yield break;
            }

            SKFontMetrics m = GetSKFont(info).Metrics;
            float em = info.EmPx;
            if ((info.Style & FontStyle.Underline) != 0)
            {
                float thick = Math.Max(1f, m.UnderlineThickness ?? em * 0.05f);
                float pos = m.UnderlinePosition ?? em * 0.1f;
                yield return new SKRect(line.X, line.Baseline + pos - (thick / 2), line.X + line.Width, line.Baseline + pos + (thick / 2));
            }

            if ((info.Style & FontStyle.Strikeout) != 0)
            {
                float thick = Math.Max(1f, m.StrikeoutThickness ?? em * 0.05f);
                float pos = m.StrikeoutPosition ?? -em * 0.3f;
                yield return new SKRect(line.X, line.Baseline + pos - (thick / 2), line.X + line.Width, line.Baseline + pos + (thick / 2));
            }
        }

        public static void AddDecorationPaths(SKPath path, FontInfo info, Line line, Layout layout)
        {
            foreach (SKRect r in DecorationRects(info, line))
            {
                path.AddRect(r);
            }
        }
    }
}
