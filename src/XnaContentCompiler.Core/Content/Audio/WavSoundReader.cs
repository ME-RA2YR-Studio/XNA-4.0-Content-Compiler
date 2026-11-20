using System.IO;
using System.Text;

namespace XnaContentCompiler.Core.Content.Audio;

/// <summary>
/// A mono/stereo 16-bit PCM sound ready to be written as an XNA SoundEffect asset.
/// </summary>
public sealed class SoundEffectData
{
    public required int SampleRate { get; init; }
    public required int Channels { get; init; }
    public required byte[] PcmData { get; init; }
    public required int DurationMilliseconds { get; init; }

    /// <summary>Loop start in bytes, taken from the WAV 'smpl' chunk when present.</summary>
    public int LoopStart { get; init; }

    /// <summary>Loop length in bytes, taken from the WAV 'smpl' chunk when present.</summary>
    public int LoopLength { get; init; }

    public int BytesPerSecond => SampleRate * Channels * 2;
    public int BlockAlign => Channels * 2;
}

/// <summary>
/// Reads PCM and IEEE-float WAV files and converts them to 16-bit PCM, matching the
/// format expected by XNA's SoundEffect content type.
/// </summary>
public static class WavSoundReader
{
    public static SoundEffectData Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Parse(bytes, Path.GetFileName(path));
    }

    public static SoundEffectData Parse(byte[] bytes, string fileName)
    {
        if (bytes.Length < 12
            || !HasId(bytes, 0, "RIFF")
            || !HasId(bytes, 8, "WAVE"))
        {
            throw new InvalidDataException($"{fileName} is not a RIFF WAVE file.");
        }

        WaveFormat? format = null;
        byte[]? data = null;
        (uint Start, uint End)? loopFrames = null;

        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(bytes, position, 4);
            var chunkSize = BitConverter.ToUInt32(bytes, position + 4);
            var chunkData = position + 8;

            if (chunkSize > int.MaxValue)
                throw new InvalidDataException($"{fileName} contains an oversized '{chunkId.Trim()}' chunk.");

            if ((long)chunkData + chunkSize > bytes.Length)
                throw new InvalidDataException($"{fileName} contains a truncated '{chunkId.Trim()}' chunk.");

            if (chunkId == "fmt ")
                format = ParseFormat(bytes, chunkData, (int)chunkSize, fileName);
            else if (chunkId == "data")
                data = bytes[chunkData..(chunkData + (int)chunkSize)];
            else if (chunkId == "smpl")
                loopFrames = ParseLoopPoints(bytes, chunkData, (int)chunkSize);

            position = chunkData + (int)chunkSize;
            if (chunkSize % 2 != 0)
                position++;
        }

        if (format is null)
            throw new InvalidDataException($"{fileName} does not contain a 'fmt ' chunk.");

        if (data is null)
            throw new InvalidDataException($"{fileName} does not contain a 'data' chunk.");

        return Convert(format, data, fileName, loopFrames);
    }

    /// <summary>
    /// Reads the first sample loop of a RIFF 'smpl' chunk. Loop start/end are expressed
    /// in sample frames, matching how XNA's content pipeline interprets them.
    /// </summary>
    private static (uint Start, uint End)? ParseLoopPoints(byte[] bytes, int offset, int size)
    {
        const int LoopHeaderSize = 36;
        const int LoopRecordSize = 24;

        if (size < LoopHeaderSize + LoopRecordSize || offset + size > bytes.Length)
            return null;

        var loopCount = BitConverter.ToUInt32(bytes, offset + 28);
        if (loopCount == 0)
            return null;

        var start = BitConverter.ToUInt32(bytes, offset + LoopHeaderSize + 8);
        var end = BitConverter.ToUInt32(bytes, offset + LoopHeaderSize + 12);

        if (end <= start)
            return null;

        return (start, end);
    }

    private static WaveFormat ParseFormat(byte[] bytes, int offset, int size, string fileName)
    {
        if (size < 16 || offset + size > bytes.Length)
            throw new InvalidDataException($"{fileName} has an invalid 'fmt ' chunk.");

        var tag = BitConverter.ToUInt16(bytes, offset);
        var channels = BitConverter.ToUInt16(bytes, offset + 2);
        var sampleRate = (int)BitConverter.ToUInt32(bytes, offset + 4);
        var bitsPerSample = BitConverter.ToUInt16(bytes, offset + 14);

        if (tag == 0xFFFE && size >= 40)
            tag = BitConverter.ToUInt16(bytes, offset + 24);

        if (channels is < 1 or > 2)
        {
            throw new NotSupportedException(
                $"{fileName}: only mono and stereo audio is supported (found {channels} channels).");
        }

        if (sampleRate is < 8000 or > 48000)
        {
            throw new NotSupportedException(
                $"{fileName}: sample rate {sampleRate} Hz is outside the supported 8000-48000 Hz range.");
        }

        if (tag == 1 && bitsPerSample is not (8 or 16 or 24 or 32))
        {
            throw new NotSupportedException(
                $"{fileName}: {bitsPerSample}-bit PCM is not supported. Use 8, 16, 24 or 32-bit PCM.");
        }

        if (tag == 3 && bitsPerSample != 32)
        {
            throw new NotSupportedException(
                $"{fileName}: only 32-bit IEEE float WAV files are supported.");
        }

        if (tag is not (1 or 3))
        {
            throw new NotSupportedException(
                $"{fileName}: compressed WAV formats are not supported. Convert the file to 16-bit PCM first.");
        }

        return new WaveFormat(tag, channels, sampleRate, bitsPerSample);
    }

    private static SoundEffectData Convert(
        WaveFormat format,
        byte[] data,
        string fileName,
        (uint Start, uint End)? loopFrames)
    {
        var bytesPerSample = format.BitsPerSample / 8;
        var frameSize = bytesPerSample * format.Channels;

        if (frameSize <= 0 || data.Length % frameSize != 0)
        {
            throw new InvalidDataException(
                $"{fileName}: the audio data length is not a multiple of the sample size.");
        }

        var frameCount = data.Length / frameSize;
        if (frameCount == 0)
            throw new InvalidDataException($"{fileName} does not contain any audio samples.");

        var pcm = new byte[frameCount * format.Channels * 2];
        var output = 0;

        for (var offset = 0; offset < data.Length; offset += bytesPerSample)
        {
            short sample;
            if (format.FormatTag == 3)
            {
                var value = BitConverter.ToSingle(data, offset);
                sample = (short)Math.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);
            }
            else
            {
                switch (format.BitsPerSample)
                {
                    case 8:
                        sample = (short)((data[offset] - 128) * 256);
                        break;
                    case 16:
                        sample = BitConverter.ToInt16(data, offset);
                        break;
                    case 24:
                        sample = (short)((data[offset]
                            | (data[offset + 1] << 8)
                            | (data[offset + 2] << 16)) >> 8);
                        break;
                    case 32:
                        sample = (short)(BitConverter.ToInt32(data, offset) >> 16);
                        break;
                    default:
                        throw new NotSupportedException($"{fileName}: unsupported PCM bit depth.");
                }
            }

            BitConverter.TryWriteBytes(pcm.AsSpan(output), sample);
            output += 2;
        }

        var duration = (int)Math.Round(frameCount * 1000.0 / format.SampleRate);

        var loopStart = 0;
        var loopLength = 0;

        if (loopFrames is { } loop)
        {
            var blockAlign = format.Channels * 2;
            var startFrame = Math.Min(loop.Start, (uint)frameCount);
            var endFrame = Math.Min(loop.End, (uint)frameCount);

            if (endFrame > startFrame)
            {
                loopStart = (int)startFrame * blockAlign;
                loopLength = (int)(endFrame - startFrame) * blockAlign;
            }
        }

        return new SoundEffectData
        {
            SampleRate = format.SampleRate,
            Channels = format.Channels,
            PcmData = pcm,
            DurationMilliseconds = Math.Max(1, duration),
            LoopStart = loopStart,
            LoopLength = loopLength,
        };
    }

    private static bool HasId(byte[] bytes, int offset, string id)
    {
        return offset + id.Length <= bytes.Length
            && bytes[offset] == id[0]
            && bytes[offset + 1] == id[1]
            && bytes[offset + 2] == id[2]
            && bytes[offset + 3] == id[3];
    }

    private sealed record WaveFormat(int FormatTag, int Channels, int SampleRate, int BitsPerSample);
}
