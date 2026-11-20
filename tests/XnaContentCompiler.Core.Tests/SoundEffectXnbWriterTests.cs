using System.Buffers.Binary;
using XnaContentCompiler.Core.Content.Audio;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class SoundEffectXnbWriterTests
{
    [Fact]
    public void WritesXnaSoundEffectXnb()
    {
        var root = TestDirectory.Create();
        try
        {
            var sound = new SoundEffectData
            {
                SampleRate = 8000,
                Channels = 1,
                PcmData = new byte[] { 0x01, 0x02, 0x03, 0x04 },
                DurationMilliseconds = 100,
                LoopStart = 4,
                LoopLength = 8,
            };

            var output = Path.Combine(root, "sound.xnb");
            SoundEffectXnbWriter.Write(sound, output);

            var info = XnbManifestRewriter.ReadInfo(output);
            Assert.Equal((byte)'w', info.Platform);
            Assert.Equal(5, info.FormatVersion);
            Assert.Equal(0, info.Flags);
            Assert.Single(info.Readers);
            Assert.Equal(XnaTypeReaders.SoundEffectReader, info.Readers[0].Name);

            var bytes = File.ReadAllBytes(output);
            Assert.Equal(BitConverter.ToUInt32(bytes, 6), (uint)bytes.Length);

            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position)); // type writer index

            var formatSize = BitConverter.ToInt32(bytes, position);
            position += 4;
            Assert.Equal(18, formatSize);

            Assert.Equal(1, BitConverter.ToUInt16(bytes, position)); // PCM
            Assert.Equal(1, BitConverter.ToUInt16(bytes, position + 2)); // channels
            Assert.Equal(8000, BitConverter.ToInt32(bytes, position + 4)); // sample rate
            position += formatSize;

            var dataSize = BitConverter.ToInt32(bytes, position);
            position += 4;
            Assert.Equal(4, dataSize);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 },
                bytes[position..(position + dataSize)]);
            position += dataSize;
            Assert.Equal(4, BitConverter.ToInt32(bytes, position)); // loop start
            Assert.Equal(8, BitConverter.ToInt32(bytes, position + 4)); // loop length
            Assert.Equal(100, BitConverter.ToInt32(bytes, position + 8)); // duration
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
