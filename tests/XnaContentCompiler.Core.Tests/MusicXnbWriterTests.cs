using System.Text;
using XnaContentCompiler.Core.Content;
using XnaContentCompiler.Core.Content.Audio;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class MusicXnbWriterTests
{
    [Fact]
    public void WritesSongXnbWithMediaNameAndDuration()
    {
        var root = TestDirectory.Create();
        try
        {
            var output = Path.Combine(root, "theme.xnb");
            MusicXnbWriter.Write(output, "theme.mp3", 12345, GraphicsProfileSetting.Reach);

            var info = XnbManifestRewriter.ReadInfo(output);
            Assert.Equal((byte)'w', info.Platform);
            Assert.Equal(5, info.FormatVersion);
            Assert.Equal(0, info.Flags);
            Assert.Equal(2, info.Readers.Count);
            Assert.Equal(XnaTypeReaders.SongReader, info.Readers[0].Name);
            Assert.Equal(XnaTypeReaders.Int32Reader, info.Readers[1].Name);

            var bytes = File.ReadAllBytes(output);
            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position)); // type writer index

            var nameLength = Read7Bit(bytes, ref position);
            Assert.Equal("theme.mp3", Encoding.UTF8.GetString(bytes, position, nameLength));
            position += nameLength;

            Assert.Equal(2, Read7Bit(bytes, ref position)); // WriteObject<int> writer index
            Assert.Equal(12345, BitConverter.ToInt32(bytes, position));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void HiDefSetsTheHeaderFlag()
    {
        var root = TestDirectory.Create();
        try
        {
            var output = Path.Combine(root, "theme.xnb");
            MusicXnbWriter.Write(output, "theme.mp3", 1000, GraphicsProfileSetting.HiDef);

            var info = XnbManifestRewriter.ReadInfo(output);
            Assert.Equal(1, info.Flags);
            Assert.Equal(XnaTypeReaders.SongReader, info.Readers[0].Name);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReadsDurationOfCbrMp3()
    {
        var root = TestDirectory.Create();
        try
        {
            // 100 MPEG1 Layer III frames, 128 kbps, 44.1 kHz.
            var path = Path.Combine(root, "cbr.mp3");
            File.WriteAllBytes(path, TestDirectory.BuildMp3(frames: 100));

            var duration = SongInfoReader.GetDurationMilliseconds(path);

            // 100 frames * 1152 samples / 44100 Hz = 2612 ms.
            Assert.Equal(2612, duration);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReadsDurationFromXingFrameCount()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "vbr.mp3");
            File.WriteAllBytes(path, TestDirectory.BuildMp3(frames: 10, frameCountInXing: 60));

            var duration = SongInfoReader.GetDurationMilliseconds(path);

            // 60 frames * 1152 samples / 44100 Hz = 1567 ms.
            Assert.Equal(1567, duration);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void FallsBackToCbrEstimateWhenXingHasNoFrameCount()
    {
        var root = TestDirectory.Create();
        try
        {
            var bytes = TestDirectory.BuildMp3(frames: 100, frameCountInXing: 60);

            // Clear the Xing frame-count flag; the stored count must then be ignored.
            bytes.AsSpan(4 + 32 + 4, 4).Clear();

            var path = Path.Combine(root, "vbr-notag.mp3");
            File.WriteAllBytes(path, bytes);

            Assert.Equal(2612, SongInfoReader.GetDurationMilliseconds(path));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReadsDurationOfWma()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "song.wma");

            // File Properties object: play duration in 100 ns units minus preroll.
            var bytes = new byte[128];
            byte[] guid =
            {
                0xA1, 0xDC, 0xAB, 0x8C, 0x47, 0xA9, 0xCF, 0x11,
                0x8E, 0xE4, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65,
            };
            guid.CopyTo(bytes, 0);
            BitConverter.GetBytes(30_000_000UL).CopyTo(bytes, 64); // 3 s
            BitConverter.GetBytes(500u).CopyTo(bytes, 80); // 500 ms preroll
            File.WriteAllBytes(path, bytes);

            Assert.Equal(2500, SongInfoReader.GetDurationMilliseconds(path));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReturnsZeroForUnsupportedExtensions()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "sound.ogg");
            File.WriteAllBytes(path, new byte[64]);
            Assert.Equal(0, SongInfoReader.GetDurationMilliseconds(path));
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
