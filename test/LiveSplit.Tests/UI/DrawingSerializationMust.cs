using LiveSplit.UI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LiveSplit.Tests.UI;

public class DrawingSerializationMust
{
    private const string WindowsSegoeUiFont =
        "AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABNTeXN0ZW0uRHJhd2luZy5Gb250BAAAAAROYW1lBFNpemUFU3R5bGUEVW5pdAEABAQLGFN5c3RlbS5EcmF3aW5nLkZvbnRTdHlsZQIAAAAbU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AgAAAAIAAAAGAwAAAA5DZW50dXJ5IEdvdGhpYwAAL0IF/P///xhTeXN0ZW0uRHJhd2luZy5Gb250U3R5bGUBAAAAB3ZhbHVlX18ACAIAAAABAAAABfv///8bU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AQAAAAd2YWx1ZV9fAAgCAAAAAgAAAAs=";

    private static string ReadLayoutElement(string name)
    {
        string path = RepositoryFiles.Find("src", "LiveSplit.View", "Resources", "DefaultLayout.lsl");
        Match match = Regex.Match(File.ReadAllText(path), $@"<{name}><!\[CDATA\[([^\]]+)\]\]>");
        Assert.True(match.Success);
        return match.Groups[1].Value;
    }

    [Fact]
    public void ReadFontsWrittenByWindowsLiveSplit()
    {
        Font font = DrawingSerialization.DeserializeFont(Convert.FromBase64String(WindowsSegoeUiFont));

        Assert.Equal("Century Gothic", font.Name);
        Assert.Equal(43.75f, font.Size);
        Assert.Equal(FontStyle.Bold, font.Style);
        Assert.Equal(GraphicsUnit.Pixel, font.Unit);
    }

    [Fact]
    public void WriteFontsByteForByteLikeBinaryFormatter()
    {
        byte[] original = Convert.FromBase64String(WindowsSegoeUiFont);
        Font font = DrawingSerialization.DeserializeFont(original);

        byte[] written = DrawingSerialization.SerializeFont(font);

        Assert.Equal(original, written);
    }

    [Theory]
    [InlineData("TimerFont")]
    [InlineData("TimesFont")]
    [InlineData("TextFont")]
    public void RoundtripEveryFontOfTheDefaultLayout(string element)
    {
        byte[] original = Convert.FromBase64String(ReadLayoutElement(element));

        Font font = DrawingSerialization.DeserializeFont(original);
        byte[] written = DrawingSerialization.SerializeFont(font);

        Assert.Equal(original, written);
    }

    [Fact]
    public void WriteImagesWithTheHeaderLiveSplitCoreExpects()
    {
        using var bitmap = new Bitmap(4, 3);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Red);

        byte[] written = DrawingSerialization.SerializeImage(bitmap);

        string base64 = Convert.ToBase64String(written);
        string windowsPrefix = Convert.ToBase64String(Convert.FromBase64String("AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABVTeXN0ZW0uRHJhd2luZy5CaXRtYXABAAAABERhdGEHAgIAAAAJAwAAAA8DAAAA"));
        Assert.StartsWith(windowsPrefix[..^8], base64);
        Assert.Equal(0x0B, written[^1]);
        Assert.Equal(0x89, written[161]);
        Assert.Equal((byte)'P', written[162]);
    }

    [Fact]
    public void RoundtripImages()
    {
        using var bitmap = new Bitmap(5, 7);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(255, 10, 200, 30));
        }

        Image restored = DrawingSerialization.DeserializeImage(DrawingSerialization.SerializeImage(bitmap));

        Assert.Equal(5, restored.Width);
        Assert.Equal(7, restored.Height);
    }

    [Fact]
    public void ReadImagesWrittenByWindowsLiveSplit()
    {
        string path = RepositoryFiles.Find("lib", "livesplit-core", "tests", "layout_files", "WithBackgroundImage.lsl");
        Match match = Regex.Match(File.ReadAllText(path), @"<BackgroundImage><!\[CDATA\[([^\]]+)\]\]>");
        Assert.True(match.Success);

        Image image = DrawingSerialization.DeserializeImage(Convert.FromBase64String(match.Groups[1].Value));

        Assert.True(image.Width > 0);
        Assert.True(image.Height > 0);
    }
}
