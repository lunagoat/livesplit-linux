using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace System.Drawing.Text
{
    public class PrivateFontCollection : FontCollection
    {
        private readonly List<FontFamily> _families = new List<FontFamily>();

        public PrivateFontCollection()
        {
        }

        public void AddMemoryFont(IntPtr memory, int length)
        {
        }

        public void AddFontFile(string filename)
        {
        }

        public override FontFamily[] Families => _families.Count > 0 ? _families.ToArray() : new[] { FontFamily.GenericSansSerif };
    }
}
