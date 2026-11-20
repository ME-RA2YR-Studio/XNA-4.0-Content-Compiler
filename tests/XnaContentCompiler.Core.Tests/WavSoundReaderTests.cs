using System.Buffers.Binary;
using XnaContentCompiler.Core.Content.Audio;

namespace XnaContentCompiler.Core.Tests;

public sealed class WavSoundReaderTests
{
    [Fact]
    public void Parses16BitStereoPcm()
    {
        var samples = new short[] { 100, -100, 2000, -2000 };
        var data = new byte[samples.Length * 2];
        for (var index = 0; index < samples.Length; index++)
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * 2), samples[index]);

        var wav = TestDirectory.BuildWav(1, 2, 44100, 16, data);
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(44100, sound.SampleRate);
        Assert.Equal(2, sound.Channels);
        Assert.Equal(1, sound.DurationMilliseconds); // 2 frames rounded up to 1 ms
        Assert.Equal(data, sound.PcmData);
        Assert.Equal(4, sound.BlockAlign);
    }

    [Fact]
    public void ConvertsUnsigned8BitToPcm16()
    {
        var wav = TestDirectory.BuildWav(1, 1, 8000, 8, new byte[] { 0, 128, 255 });
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(6, sound.PcmData.Length);
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(0)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(2)));
        Assert.Equal((short)32512, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(4)));
    }

    [Fact]
    public void Converts24BitPcmToPcm16()
    {
        var data = new byte[]
        {
            0xFF, 0xFF, 0xFF, // -1
            0x00, 0x00, 0x00, // 0
            0xFF, 0xFF, 0x7F, // max positive
        };
        var wav = TestDirectory.BuildWav(1, 1, 8000, 24, data);
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(-1, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(0)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(2)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(4)));
    }

    [Fact]
    public void ConvertsFloatPcmToPcm16()
    {
        var samples = new[] { 0f, 0.5f, -1f, 1f };
        var data = new byte[samples.Length * 4];
        for (var index = 0; index < samples.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(index * 4), samples[index]);

        var wav = TestDirectory.BuildWav(3, 1, 8000, 32, data);
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(0)));
        Assert.Equal(16384, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(2)));
        Assert.Equal(-32767, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(4)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(sound.PcmData.AsSpan(6)));
    }

    [Fact]
    public void ParsesWaveFormatExtensiblePcm()
    {
        var data = new byte[] { 0x34, 0x12 };
        var wav = TestDirectory.BuildWav(0, 1, 8000, 16, data, extensibleFormatTag: 1);
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(0x34, sound.PcmData[0]);
        Assert.Equal(0x12, sound.PcmData[1]);
    }

    [Fact]
    public void PreservesSampleLoopPointsFromSmplChunk()
    {
        // 8 stereo frames at 16-bit (4 bytes per frame).
        var wav = TestDirectory.BuildWav(1, 2, 8000, 16, new byte[32], loop: (2, 6));
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(2 * 4, sound.LoopStart);
        Assert.Equal((6 - 2) * 4, sound.LoopLength);
    }

    [Fact]
    public void ClampsLoopPointsToTheAvailableFrames()
    {
        // 4 mono frames, loop end past the end of the data.
        var wav = TestDirectory.BuildWav(1, 1, 8000, 16, new byte[8], loop: (1, 100));
        var sound = WavSoundReader.Parse(wav, "test.wav");

        Assert.Equal(2, sound.LoopStart);
        Assert.Equal((4 - 1) * 2, sound.LoopLength);
    }

    [Fact]
    public void IgnoresLoopWhenTheLoopCountIsZero()
    {
        var bytes = TestDirectory.BuildWav(1, 1, 8000, 16, new byte[8], loop: (1, 3));

        // Zero out the loop count at the start of the smpl header (offset +28).
        var position = IndexOfSmpl(bytes);
        Assert.True(position >= 0);
        bytes.AsSpan(position + 8 + 28, 4).Clear();

        var sound = WavSoundReader.Parse(bytes, "test.wav");
        Assert.Equal(0, sound.LoopStart);
        Assert.Equal(0, sound.LoopLength);
    }

    private static int IndexOfSmpl(byte[] bytes)
    {
        for (var index = 12; index + 4 <= bytes.Length; index++)
        {
            if (bytes[index] == 's'
                && bytes[index + 1] == 'm'
                && bytes[index + 2] == 'p'
                && bytes[index + 3] == 'l')
            {
                return index;
            }
        }

        return -1;
    }

    [Theory]
    [InlineData(2, 1, 8000, 4)] // MS ADPCM
    [InlineData(17, 1, 8000, 4)] // IMA ADPCM
    public void RejectsCompressedFormats(int formatTag, int channels, int sampleRate, int bits)
    {
        var wav = TestDirectory.BuildWav(formatTag, channels, sampleRate, bits, new byte[16]);
        Assert.Throws<NotSupportedException>(() => WavSoundReader.Parse(wav, "test.wav"));
    }

    [Fact]
    public void RejectsMoreThanTwoChannels()
    {
        var wav = TestDirectory.BuildWav(1, 3, 44100, 16, new byte[12]);
        Assert.Throws<NotSupportedException>(() => WavSoundReader.Parse(wav, "test.wav"));
    }

    [Fact]
    public void RejectsMissingDataChunk()
    {
        var wav = TestDirectory.BuildWav(1, 1, 8000, 16, Array.Empty<byte>());
        wav[^8..].AsSpan().Clear();
        Assert.Throws<InvalidDataException>(() => WavSoundReader.Parse(wav, "test.wav"));
    }
}
