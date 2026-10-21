using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Nrbf;
using System.IO;
using System.Text;

namespace LiveSplit.UI;

public static class DrawingSerialization
{
    private const string DrawingAssembly = "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
    private const string FontTypeName = "System.Drawing.Font";
    private const string BitmapTypeName = "System.Drawing.Bitmap";
    private const string FontStyleTypeName = "System.Drawing.FontStyle";
    private const string GraphicsUnitTypeName = "System.Drawing.GraphicsUnit";

    public static byte[] SerializeFont(Font font)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);

        WriteHeader(writer);
        WriteLibrary(writer, 2);

        writer.Write((byte)5);
        writer.Write(1);
        writer.Write(FontTypeName);
        writer.Write(4);
        writer.Write("Name");
        writer.Write("Size");
        writer.Write("Style");
        writer.Write("Unit");
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)4);
        writer.Write((byte)4);
        writer.Write((byte)11);
        writer.Write(FontStyleTypeName);
        writer.Write(2);
        writer.Write(GraphicsUnitTypeName);
        writer.Write(2);
        writer.Write(2);

        writer.Write((byte)6);
        writer.Write(3);
        writer.Write(font.Name);
        writer.Write(font.Size);
        WriteEnum(writer, -4, FontStyleTypeName, (int)font.Style);
        WriteEnum(writer, -5, GraphicsUnitTypeName, (int)font.Unit);

        writer.Write((byte)11);
        writer.Flush();
        return stream.ToArray();
    }

    public static Font DeserializeFont(byte[] data)
    {
        using var stream = new MemoryStream(data);
        ClassRecord record = NrbfDecoder.DecodeClassRecord(stream);

        string name = record.GetString("Name");
        float size = record.GetSingle("Size");
        var style = (FontStyle)GetEnumValue(record, "Style");
        var unit = (GraphicsUnit)GetEnumValue(record, "Unit");

        return new Font(name, size, style, unit);
    }

    public static byte[] SerializeImage(Image image)
    {
        using var imageStream = new MemoryStream();
        image.Save(imageStream, ImageFormat.Png);
        byte[] imageData = imageStream.ToArray();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);

        WriteHeader(writer);
        WriteLibrary(writer, 2);

        writer.Write((byte)5);
        writer.Write(1);
        writer.Write(BitmapTypeName);
        writer.Write(1);
        writer.Write("Data");
        writer.Write((byte)7);
        writer.Write((byte)2);
        writer.Write(2);

        writer.Write((byte)9);
        writer.Write(3);

        writer.Write((byte)15);
        writer.Write(3);
        writer.Write(imageData.Length);
        writer.Write((byte)2);
        writer.Write(imageData);

        writer.Write((byte)11);
        writer.Flush();
        return stream.ToArray();
    }

    public static Image DeserializeImage(byte[] data)
    {
        using var stream = new MemoryStream(data);
        ClassRecord record = NrbfDecoder.DecodeClassRecord(stream);

        if (record.GetSerializationRecord("Data") is not SZArrayRecord<byte> array)
        {
            throw new InvalidDataException("The serialized image does not contain image data.");
        }

        byte[] imageData = array.GetArray();
        return Image.FromStream(new MemoryStream(imageData));
    }

    private static int GetEnumValue(ClassRecord record, string memberName)
    {
        SerializationRecord member = record.GetSerializationRecord(memberName);
        return member is ClassRecord enumRecord
            ? enumRecord.GetInt32("value__")
            : ((PrimitiveTypeRecord<int>)member).Value;
    }

    private static void WriteHeader(BinaryWriter writer)
    {
        writer.Write((byte)0);
        writer.Write(1);
        writer.Write(-1);
        writer.Write(1);
        writer.Write(0);
    }

    private static void WriteLibrary(BinaryWriter writer, int id)
    {
        writer.Write((byte)12);
        writer.Write(id);
        writer.Write(DrawingAssembly);
    }

    private static void WriteEnum(BinaryWriter writer, int objectId, string typeName, int value)
    {
        writer.Write((byte)5);
        writer.Write(objectId);
        writer.Write(typeName);
        writer.Write(1);
        writer.Write("value__");
        writer.Write((byte)0);
        writer.Write((byte)8);
        writer.Write(2);
        writer.Write(value);
    }
}
