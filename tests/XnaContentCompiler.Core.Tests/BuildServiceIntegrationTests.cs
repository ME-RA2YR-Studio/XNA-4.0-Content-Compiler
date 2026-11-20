using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class BuildServiceIntegrationTests
{
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string FourPixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAATSURBVBhXY/jPwPAfGZMswPAfADxAH+HkFd+zAAAAAElFTkSuQmCC";

    [Fact]
    public void BuildsTextureSoundAndSpriteFont()
    {
        var root = TestDirectory.Create();
        try
        {
            var pixelPath = Path.Combine(root, "pixel.png");
            File.WriteAllBytes(pixelPath, Convert.FromBase64String(OnePixelPng));

            var soundData = new byte[1600 * 2];
            for (var index = 0; index < 1600; index++)
            {
                var value = (short)(index % 400);
                BitConverter.TryWriteBytes(soundData.AsSpan(index * 2), value);
            }
            var wavPath = Path.Combine(root, "beep.wav");
            File.WriteAllBytes(wavPath, TestDirectory.BuildWav(1, 1, 8000, 16, soundData));

            var fontPath = Path.Combine(root, "default.spritefont");
            var fontAvailable = File.Exists(@"C:\Windows\Fonts\arial.ttf");
            if (fontAvailable)
            {
                File.WriteAllText(fontPath, """
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>Arial</FontName>
    <Size>18</Size>
    <Spacing>0</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <DefaultCharacter>*</DefaultCharacter>
    <CharacterRegions>
      <CharacterRegion>
        <Start>&#32;</Start>
        <End>&#126;</End>
      </CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
""");
            }

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };

            var pixel = new ContentItem(pixelPath);
            pixel.OutputName = "pixel";
            project.AddItem(pixel);

            var sound = new ContentItem(wavPath);
            sound.OutputName = "beep";
            project.AddItem(sound);

            if (fontAvailable)
            {
                var font = new ContentItem(fontPath);
                font.OutputName = "default";
                project.AddItem(font);
            }

            var service = new ContentBuildService();
            var summary = service.Build(project);

            Assert.False(summary.Canceled);
            Assert.Equal(0, summary.Failed);
            Assert.Equal(fontAvailable ? 3 : 2, summary.Succeeded);

            var textureInfo = XnbManifestRewriter.ReadInfo(Path.Combine(project.OutputDirectory, "pixel.xnb"));
            Assert.Single(textureInfo.Readers);
            Assert.Equal(XnaTypeReaders.Texture2DReader, textureInfo.Readers[0].Name);

            var textureBytes = File.ReadAllBytes(Path.Combine(project.OutputDirectory, "pixel.xnb"));
            var position = textureInfo.BodyOffset;
            Assert.Equal(1, Read7Bit(textureBytes, ref position));
            Assert.Equal(0, BitConverter.ToInt32(textureBytes, position)); // SurfaceFormat.Color
            Assert.Equal(1, BitConverter.ToInt32(textureBytes, position + 4));
            Assert.Equal(1, BitConverter.ToInt32(textureBytes, position + 8));
            Assert.Equal(1, BitConverter.ToInt32(textureBytes, position + 12)); // one mip

            var soundInfo = XnbManifestRewriter.ReadInfo(Path.Combine(project.OutputDirectory, "beep.xnb"));
            Assert.Single(soundInfo.Readers);
            Assert.Equal(XnaTypeReaders.SoundEffectReader, soundInfo.Readers[0].Name);

            if (fontAvailable)
            {
                var fontInfo = XnbManifestRewriter.ReadInfo(
                    Path.Combine(project.OutputDirectory, "default.xnb"));
                Assert.Equal(8, fontInfo.Readers.Count);
                Assert.Equal(XnaTypeReaders.SpriteFontReader, fontInfo.Readers[0].Name);
                Assert.Equal(XnaTypeReaders.Texture2DReader, fontInfo.Readers[1].Name);
            }
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void BuildsSongAndCopiesTheMediaFile()
    {
        var root = TestDirectory.Create();
        try
        {
            var mp3Path = Path.Combine(root, "theme.mp3");
            File.WriteAllBytes(mp3Path, TestDirectory.BuildMp3(frames: 100));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            var song = new ContentItem(mp3Path) { OutputName = "music/theme" };
            project.AddItem(song);

            var summary = new ContentBuildService().Build(project);

            Assert.Equal(0, summary.Failed);
            Assert.Equal(1, summary.Succeeded);

            var xnbPath = Path.Combine(project.OutputDirectory, "music", "theme.xnb");
            var mediaPath = Path.Combine(project.OutputDirectory, "music", "theme.mp3");
            Assert.True(File.Exists(xnbPath));
            Assert.True(File.Exists(mediaPath));

            var info = XnbManifestRewriter.ReadInfo(xnbPath);
            Assert.Equal(2, info.Readers.Count);
            Assert.Equal(XnaTypeReaders.SongReader, info.Readers[0].Name);
            Assert.Equal(XnaTypeReaders.Int32Reader, info.Readers[1].Name);

            var bytes = File.ReadAllBytes(xnbPath);
            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position)); // type writer index

            var nameLength = Read7Bit(bytes, ref position);
            Assert.Equal("theme.mp3", System.Text.Encoding.UTF8.GetString(bytes, position, nameLength));
            position += nameLength;

            Assert.Equal(2, Read7Bit(bytes, ref position)); // WriteObject<int> writer index
            Assert.Equal(2612, BitConverter.ToInt32(bytes, position));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void HiDefProfileSetsTheXnbHeaderFlag()
    {
        var root = TestDirectory.Create();
        try
        {
            var soundData = new byte[160];
            var wavPath = Path.Combine(root, "beep.wav");
            File.WriteAllBytes(wavPath, TestDirectory.BuildWav(1, 1, 8000, 16, soundData));

            var project = new ContentProject
            {
                OutputDirectory = Path.Combine(root, "out"),
                Profile = GraphicsProfileSetting.HiDef,
            };
            var sound = new ContentItem(wavPath) { OutputName = "beep" };
            project.AddItem(sound);

            var summary = new ContentBuildService().Build(project);
            Assert.Equal(0, summary.Failed);

            var info = XnbManifestRewriter.ReadInfo(
                Path.Combine(project.OutputDirectory, "beep.xnb"));
            Assert.Equal(1, info.Flags);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void NoChangeTextureFormatKeepsTheSourceSurfaceFormat()
    {
        var root = TestDirectory.Create();
        try
        {
            var pixelPath = Path.Combine(root, "pixel.png");
            File.WriteAllBytes(pixelPath, Convert.FromBase64String(OnePixelPng));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            var pixel = new ContentItem(pixelPath) { OutputName = "pixel" };
            pixel.Texture.OutputFormat = TextureOutputFormat.NoChange;
            project.AddItem(pixel);

            var summary = new ContentBuildService().Build(project);
            Assert.Equal(0, summary.Failed);

            var info = XnbManifestRewriter.ReadInfo(
                Path.Combine(project.OutputDirectory, "pixel.xnb"));
            Assert.Equal(XnaTypeReaders.Texture2DReader, info.Readers[0].Name);

            var bytes = File.ReadAllBytes(Path.Combine(project.OutputDirectory, "pixel.xnb"));
            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position));
            Assert.Equal(0, BitConverter.ToInt32(bytes, position)); // SurfaceFormat.Color kept
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void DxtCompressedTextureUsesTheXnaSurfaceFormat()
    {
        var root = TestDirectory.Create();
        try
        {
            var pixelPath = Path.Combine(root, "tile.png");
            File.WriteAllBytes(pixelPath, Convert.FromBase64String(FourPixelPng));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            var tile = new ContentItem(pixelPath) { OutputName = "tile" };
            tile.Texture.OutputFormat = TextureOutputFormat.DxtCompressed;
            project.AddItem(tile);

            var summary = new ContentBuildService().Build(project);
            Assert.Equal(0, summary.Failed);

            var output = Path.Combine(project.OutputDirectory, "tile.xnb");
            var info = XnbManifestRewriter.ReadInfo(output);
            Assert.Equal(XnaTypeReaders.Texture2DReader, info.Readers[0].Name);

            var bytes = File.ReadAllBytes(output);
            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position));

            var format = BitConverter.ToInt32(bytes, position);
            Assert.True(format is 4 or 6, $"Unexpected SurfaceFormat {format}."); // Dxt1 or Dxt5
            Assert.Equal(4, BitConverter.ToInt32(bytes, position + 4));
            Assert.Equal(4, BitConverter.ToInt32(bytes, position + 8));
            Assert.Equal(1, BitConverter.ToInt32(bytes, position + 12)); // one mip

            var dataSize = BitConverter.ToInt32(bytes, position + 16);
            Assert.Equal(8, dataSize); // 4x4 DXT1/DXT5 block data
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    private static int Read7Bit(byte[] bytes, ref int position)
    {
        var value = 0;
        var shift = 0;
        while (true)
        {
            var current = bytes[position++];
            value |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
            shift += 7;
        }
    }
}
