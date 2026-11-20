using System;
using System.IO;

namespace XnaContentCompiler.Core.Tests;

internal static class TestDirectory
{
    public static string Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "xncc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // Best-effort cleanup for temporary test folders.
        }
    }

    public static byte[] BuildWav(
        int formatTag,
        int channels,
        int sampleRate,
        int bitsPerSample,
        byte[] data,
        int? extensibleFormatTag = null,
        (uint Start, uint End)? loop = null)
    {
        var headerSize = extensibleFormatTag != null ? 40 : 16;
        var loopSize = loop != null ? 8 + 36 + 24 : 0;

        using var stream = new MemoryStream(12 + 8 + headerSize + 8 + data.Length + loopSize);
        using var writer = new BinaryWriter(stream);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(4 + 8 + headerSize + 8 + data.Length + loopSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(headerSize);
        writer.Write((ushort)(extensibleFormatTag != null ? 0xFFFE : formatTag));
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * Math.Max(1, bitsPerSample / 8));
        writer.Write((ushort)(channels * Math.Max(1, bitsPerSample / 8)));
        writer.Write((ushort)bitsPerSample);

        if (extensibleFormatTag != null)
        {
            writer.Write((ushort)22); // cbSize
            writer.Write((ushort)bitsPerSample);
            writer.Write(0); // channel mask
            writer.Write((ushort)extensibleFormatTag);
            writer.Write(new byte[14]); // remainder of the SubFormat GUID
        }

        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(data.Length);
        writer.Write(data);

        if (loop != null)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("smpl"));
            writer.Write(36 + 24);
            writer.Write(0); // manufacturer
            writer.Write(0); // product
            writer.Write(22675); // sample period (44.1 kHz, diagnostics only)
            writer.Write(60); // MIDI unity note
            writer.Write(0); // MIDI pitch fraction
            writer.Write(0); // SMPTE format
            writer.Write(0); // SMPTE offset
            writer.Write(1); // loop count
            writer.Write(0); // sampler data

            writer.Write(0); // cue point id
            writer.Write(0); // loop type: forward
            writer.Write(loop.Value.Start);
            writer.Write(loop.Value.End);
            writer.Write(0); // fraction
            writer.Write(0); // play count
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Builds a silent MPEG1 Layer III stream: 128 kbps, 44.1 kHz, stereo.
    /// Optionally writes a Xing header with the given frame count.
    /// </summary>
    public static byte[] BuildMp3(int frames, uint? frameCountInXing = null)
    {
        const int frameLength = 417; // 144 * 128000 / 44100 rounded down
        var bytes = new byte[frames * frameLength];

        for (var index = 0; index < frames; index++)
        {
            var offset = index * frameLength;
            bytes[offset] = 0xFF;
            bytes[offset + 1] = 0xFB; // MPEG1, Layer III, no CRC
            bytes[offset + 2] = 0x90; // 128 kbps, 44.1 kHz
            bytes[offset + 3] = 0x00; // stereo
        }

        if (frameCountInXing.HasValue)
        {
            // The Xing header follows the 32-byte stereo side info of the first frame.
            var xing = 4 + 32;
            System.Text.Encoding.ASCII.GetBytes("Xing").CopyTo(bytes, xing);
            new byte[] { 0x00, 0x00, 0x00, 0x01 }.CopyTo(bytes, xing + 4); // big-endian flags: frame count present
            BitConverter.GetBytes(frameCountInXing.Value).Reverse().ToArray().CopyTo(bytes, xing + 8);
        }

        return bytes;
    }
}
