using System.Drawing.Imaging;
using System.Formats.Nrbf;
using System.IO;
using System.Text;

namespace System.Drawing
{
    /// <summary>
    /// Reads and writes the BinaryFormatter (NRBF) blobs that LiveSplit layouts (.lsl) use to store
    /// <see cref="Font"/> and <see cref="Image"/> values, byte-compatible with the Windows System.Drawing types.
    /// BinaryFormatter itself is gone in .NET 9+, so SettingsHelper has to call these instead.
    /// </summary>
    public static class LegacySerialization
    {
        private const string DrawingAssembly = "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        // ------------------------------------------------------------------ Font

        public static Font DeserializeFont(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("No data.", nameof(data));
            }

            using var ms = new MemoryStream(data);
            if (NrbfDecoder.Decode(ms) is not ClassRecord record)
            {
                throw new ArgumentException("Not a serialized System.Drawing.Font.", nameof(data));
            }

            string name = record.GetString("Name");
            float size = record.GetSingle("Size");
            FontStyle style = (FontStyle)ReadEnum(record, "Style");
            GraphicsUnit unit = (GraphicsUnit)ReadEnum(record, "Unit");
            return new Font(name, size, style, unit);
        }

        public static bool TryDeserializeFont(byte[] data, out Font font)
        {
            try
            {
                font = DeserializeFont(data);
                return true;
            }
            catch (Exception)
            {
                font = null;
                return false;
            }
        }

        private static int ReadEnum(ClassRecord record, string member)
        {
            // Enums are stored as nested class records with a single "value__" member; be tolerant of a flat int too.
            SerializationRecord raw = record.GetSerializationRecord(member);
            if (raw is ClassRecord nested)
            {
                return nested.GetInt32("value__");
            }

            return record.GetInt32(member);
        }

        public static byte[] SerializeFont(Font font)
        {
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            WriteHeader(w);
            w.Write((byte)12); // BinaryLibrary
            w.Write(2);
            w.Write(DrawingAssembly);

            w.Write((byte)5); // ClassWithMembersAndTypes
            w.Write(1);
            w.Write("System.Drawing.Font");
            w.Write(4);
            w.Write("Name");
            w.Write("Size");
            w.Write("Style");
            w.Write("Unit");
            w.Write((byte)1); // String
            w.Write((byte)0); // Primitive
            w.Write((byte)4); // Class
            w.Write((byte)4); // Class
            w.Write((byte)11); // Single
            w.Write("System.Drawing.FontStyle");
            w.Write(2);
            w.Write("System.Drawing.GraphicsUnit");
            w.Write(2);
            w.Write(2); // library id

            w.Write((byte)6); // BinaryObjectString
            w.Write(3);
            w.Write(font.Name);
            w.Write(font.Size);
            WriteEnum(w, -4, "System.Drawing.FontStyle", (int)font.Style);
            WriteEnum(w, -5, "System.Drawing.GraphicsUnit", (int)font.Unit);
            w.Write((byte)11); // MessageEnd
            w.Flush();
            return ms.ToArray();
        }

        private static void WriteEnum(BinaryWriter w, int objectId, string typeName, int value)
        {
            w.Write((byte)5);
            w.Write(objectId);
            w.Write(typeName);
            w.Write(1);
            w.Write("value__");
            w.Write((byte)0); // Primitive
            w.Write((byte)8); // Int32
            w.Write(2);
            w.Write(value);
        }

        private static void WriteHeader(BinaryWriter w)
        {
            w.Write((byte)0);
            w.Write(1);
            w.Write(-1);
            w.Write(1);
            w.Write(0);
        }

        // ------------------------------------------------------------------ Image

        public static Image DeserializeImage(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("No data.", nameof(data));
            }

            using var ms = new MemoryStream(data);
            if (NrbfDecoder.Decode(ms) is not ClassRecord record)
            {
                throw new ArgumentException("Not a serialized System.Drawing image.", nameof(data));
            }

            SerializationRecord raw = record.GetSerializationRecord("Data");
            if (raw is not SZArrayRecord<byte> bytes)
            {
                throw new ArgumentException("Image data not found.", nameof(data));
            }

            return Image.FromBytes(bytes.GetArray());
        }

        public static bool TryDeserializeImage(byte[] data, out Image image)
        {
            try
            {
                image = DeserializeImage(data);
                return true;
            }
            catch (Exception)
            {
                image = null;
                return false;
            }
        }

        public static byte[] SerializeImage(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            using var png = new MemoryStream();
            image.Save(png, image.RawFormat.Equals(ImageFormat.Jpeg) ? ImageFormat.Jpeg : ImageFormat.Png);
            byte[] bytes = png.ToArray();

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, true);
            WriteHeader(w);
            w.Write((byte)12);
            w.Write(2);
            w.Write(DrawingAssembly);

            w.Write((byte)5);
            w.Write(1);
            w.Write("System.Drawing.Bitmap");
            w.Write(1);
            w.Write("Data");
            w.Write((byte)7); // PrimitiveArray
            w.Write((byte)2); // Byte
            w.Write(2);

            w.Write((byte)9); // MemberReference
            w.Write(3);
            w.Write((byte)15); // ArraySinglePrimitive
            w.Write(3);
            w.Write(bytes.Length);
            w.Write((byte)2);
            w.Write(bytes);
            w.Write((byte)11);
            w.Flush();
            return ms.ToArray();
        }
    }
}
