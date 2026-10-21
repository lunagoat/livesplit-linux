using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Encoders for formats Skia cannot write natively (BMP, GIF, ICO).</summary>
    internal static class ImageEncoder
    {
        public static byte[] Encode(SKBitmap bitmap, ImageFormat format)
        {
            if (format.Equals(ImageFormat.Jpeg))
            {
                using SKImage img = SKImage.FromBitmap(bitmap);
                using SKData data = img.Encode(SKEncodedImageFormat.Jpeg, 90);
                return data.ToArray();
            }

            if (format.Equals(ImageFormat.Webp))
            {
                using SKImage img = SKImage.FromBitmap(bitmap);
                using SKData data = img.Encode(SKEncodedImageFormat.Webp, 90);
                return data.ToArray();
            }

            if (format.Equals(ImageFormat.Bmp))
            {
                return EncodeBmp(bitmap);
            }

            if (format.Equals(ImageFormat.Gif))
            {
                return EncodeGif(bitmap);
            }

            byte[] png;
            using (SKImage img = SKImage.FromBitmap(bitmap))
            using (SKData data = img.Encode(SKEncodedImageFormat.Png, 100))
            {
                png = data.ToArray();
            }

            return format.Equals(ImageFormat.Icon) ? WrapIco(png, bitmap.Width, bitmap.Height) : png;
        }

        private static byte[] WrapIco(byte[] png, int w, int h)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)1);
            bw.Write((byte)(w >= 256 ? 0 : w));
            bw.Write((byte)(h >= 256 ? 0 : h));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(png.Length);
            bw.Write(22);
            bw.Write(png);
            return ms.ToArray();
        }

        // ------------------------------------------------------------------ BMP

        private static byte[] EncodeBmp(SKBitmap bitmap)
        {
            int w = bitmap.Width, h = bitmap.Height;
            ReadOnlySpan<byte> px = bitmap.GetPixelSpan();
            int stride = bitmap.RowBytes;

            bool hasAlpha = false;
            for (int y = 0; y < h && !hasAlpha; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[(y * stride) + (x * 4) + 3] != 255)
                    {
                        hasAlpha = true;
                        break;
                    }
                }
            }

            int bpp = hasAlpha ? 32 : 24;
            int rowSize = (((w * bpp) + 31) / 32) * 4;
            int imageSize = rowSize * h;
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((byte)'B');
            bw.Write((byte)'M');
            bw.Write(14 + 40 + imageSize);
            bw.Write(0);
            bw.Write(14 + 40);
            bw.Write(40);
            bw.Write(w);
            bw.Write(h);
            bw.Write((ushort)1);
            bw.Write((ushort)bpp);
            bw.Write(0);
            bw.Write(imageSize);
            bw.Write(2835);
            bw.Write(2835);
            bw.Write(0);
            bw.Write(0);

            var row = new byte[rowSize];
            for (int y = h - 1; y >= 0; y--)
            {
                Array.Clear(row);
                for (int x = 0; x < w; x++)
                {
                    int o = (y * stride) + (x * 4);
                    byte b = px[o], g = px[o + 1], r = px[o + 2], a = px[o + 3];
                    if (hasAlpha && a != 0 && a != 255)
                    {
                        b = (byte)Math.Min(255, ((b * 255) + (a / 2)) / a);
                        g = (byte)Math.Min(255, ((g * 255) + (a / 2)) / a);
                        r = (byte)Math.Min(255, ((r * 255) + (a / 2)) / a);
                    }

                    int d = x * (bpp / 8);
                    row[d] = b;
                    row[d + 1] = g;
                    row[d + 2] = r;
                    if (hasAlpha)
                    {
                        row[d + 3] = a;
                    }
                }

                bw.Write(row);
            }

            return ms.ToArray();
        }

        // ------------------------------------------------------------------ GIF (single frame, fixed 252 colour palette)

        private static byte[] EncodeGif(SKBitmap bitmap)
        {
            int w = bitmap.Width, h = bitmap.Height;
            ReadOnlySpan<byte> px = bitmap.GetPixelSpan();
            int stride = bitmap.RowBytes;

            const int transparentIndex = 252;
            var indices = new byte[w * h];
            bool hasTransparency = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = (y * stride) + (x * 4);
                    byte a = px[o + 3];
                    if (a < 128)
                    {
                        indices[(y * w) + x] = transparentIndex;
                        hasTransparency = true;
                        continue;
                    }

                    int b = px[o], g = px[o + 1], r = px[o + 2];
                    if (a != 255)
                    {
                        b = Math.Min(255, b * 255 / a);
                        g = Math.Min(255, g * 255 / a);
                        r = Math.Min(255, r * 255 / a);
                    }

                    int ri = (r * 5 + 127) / 255, gi = (g * 6 + 127) / 255, bi = (b * 5 + 127) / 255;
                    indices[(y * w) + x] = (byte)((ri * 7 * 6) + (gi * 6) + bi);
                }
            }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
            bw.Write((ushort)w);
            bw.Write((ushort)h);
            bw.Write((byte)0xF7); // global colour table, 256 entries
            bw.Write((byte)0);
            bw.Write((byte)0);
            for (int i = 0; i < 256; i++)
            {
                if (i < 252)
                {
                    int ri = i / 42, gi = (i / 6) % 7, bi = i % 6;
                    bw.Write((byte)(ri * 255 / 5));
                    bw.Write((byte)(gi * 255 / 6));
                    bw.Write((byte)(bi * 255 / 5));
                }
                else
                {
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                }
            }

            if (hasTransparency)
            {
                bw.Write(new byte[] { 0x21, 0xF9, 0x04, 0x01, 0, 0, transparentIndex, 0 });
            }

            bw.Write((byte)0x2C);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)w);
            bw.Write((ushort)h);
            bw.Write((byte)0);
            bw.Write((byte)8);
            byte[] lzw = Lzw(indices, 8);
            for (int i = 0; i < lzw.Length; i += 255)
            {
                int n = Math.Min(255, lzw.Length - i);
                bw.Write((byte)n);
                bw.Write(lzw, i, n);
            }

            bw.Write((byte)0);
            bw.Write((byte)0x3B);
            return ms.ToArray();
        }

        private static byte[] Lzw(byte[] data, int minCodeSize)
        {
            int clear = 1 << minCodeSize, eoi = clear + 1;
            var output = new List<byte>(data.Length / 2);
            int bitBuf = 0, bitCnt = 0;

            void Emit(int code, int size)
            {
                bitBuf |= code << bitCnt;
                bitCnt += size;
                while (bitCnt >= 8)
                {
                    output.Add((byte)(bitBuf & 0xFF));
                    bitBuf >>= 8;
                    bitCnt -= 8;
                }
            }

            var dict = new Dictionary<(int, byte), int>();
            int next = eoi + 1;
            int codeSize = minCodeSize + 1;
            Emit(clear, codeSize);
            int prefix = -1;
            foreach (byte b in data)
            {
                if (prefix < 0)
                {
                    prefix = b;
                    continue;
                }

                if (dict.TryGetValue((prefix, b), out int code))
                {
                    prefix = code;
                    continue;
                }

                Emit(prefix, codeSize);
                if (next < 4096)
                {
                    dict[(prefix, b)] = next++;
                    if (next > (1 << codeSize) && codeSize < 12)
                    {
                        codeSize++;
                    }
                }
                else
                {
                    Emit(clear, codeSize);
                    dict.Clear();
                    next = eoi + 1;
                    codeSize = minCodeSize + 1;
                }

                prefix = b;
            }

            if (prefix >= 0)
            {
                Emit(prefix, codeSize);
            }

            Emit(eoi, codeSize);
            if (bitCnt > 0)
            {
                output.Add((byte)(bitBuf & 0xFF));
            }

            return output.ToArray();
        }
    }
}
