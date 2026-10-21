using System;
using System.Drawing;
using System.IO;

namespace LiveSplit.Drawing.Tests
{
    internal static class TestUtil
    {
        /// <summary>Directory for artifacts (PNGs) produced by tests. Override with LS_DRAWING_OUT.</summary>
        public static string OutDir
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("LS_DRAWING_OUT");
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(Path.GetTempPath(), "livesplit-drawing-tests");
                }

                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static void SavePng(Bitmap bmp, string name) => bmp.Save(Path.Combine(OutDir, name), System.Drawing.Imaging.ImageFormat.Png);

        public static bool Near(Color a, Color b, int tol = 2)
        {
            return Math.Abs(a.A - b.A) <= tol && Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;
        }

        public static Bitmap White(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using Graphics g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            return bmp;
        }
    }
}
