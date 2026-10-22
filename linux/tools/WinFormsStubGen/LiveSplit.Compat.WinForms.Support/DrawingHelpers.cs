using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace System.Drawing
{
    public class ImageConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(byte[]) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(byte[]) || destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is byte[] bytes)
            {
                return new Bitmap(new MemoryStream(bytes));
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(byte[]) && value is Image img)
            {
                using var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            if (destinationType == typeof(string))
            {
                return value?.ToString() ?? string.Empty;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
