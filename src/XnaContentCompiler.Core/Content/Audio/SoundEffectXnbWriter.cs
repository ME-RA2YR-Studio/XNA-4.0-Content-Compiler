using System.IO;
using System.Text;
using XnaContentCompiler.Core.Content.Xnb;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.Core.Content.Audio;

/// <summary>
/// Writes an XNA 4.0 SoundEffect XNB file.
/// </summary>
public static class SoundEffectXnbWriter
{
    public static void Write(
        SoundEffectData sound,
        string outputPath,
        GraphicsProfileSetting profile = GraphicsProfileSetting.Reach)
    {
        var format = BuildWaveFormat(sound);

        XnbSingleTypeWriter.Write(
            outputPath,
            XnaTypeReaders.SoundEffectReader,
            profile == GraphicsProfileSetting.HiDef,
            body =>
            {
                // Format header: WAVEFORMATEX including cbSize.
                body.Write(format.Length);
                body.Write(format);

                body.Write(sound.PcmData.Length);
                body.Write(sound.PcmData);

                // Loop region in bytes, preserved from the WAV 'smpl' chunk.
                body.Write(sound.LoopStart);
                body.Write(sound.LoopLength);

                body.Write(sound.DurationMilliseconds);
            });
    }

    private static byte[] BuildWaveFormat(SoundEffectData sound)
    {
        using var stream = new MemoryStream(18);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((ushort)1); // WAVE_FORMAT_PCM
        writer.Write((ushort)sound.Channels);
        writer.Write(sound.SampleRate);
        writer.Write(sound.BytesPerSecond);
        writer.Write((ushort)sound.BlockAlign);
        writer.Write((ushort)16);
        writer.Write((ushort)0); // cbSize

        return stream.ToArray();
    }
}

/// <summary>
/// Minimal XNB writer for content types that consist of a single object with no
/// shared resources. Used for the audio path, which is produced without the
/// MonoGame pipeline.
/// </summary>
public static class XnbSingleTypeWriter
{
    private const byte PlatformWindows = (byte)'w';
    private const byte FormatVersion = 5;
    private const byte HiDefFlag = 0x01;

    public static void Write(
        string outputPath,
        string readerName,
        bool highDefinition,
        Action<BinaryWriter> writeBody)
    {
        Write(outputPath, new[] { readerName }, highDefinition, writeBody);
    }

    public static void Write(
        string outputPath,
        IReadOnlyList<string> readerNames,
        bool highDefinition,
        Action<BinaryWriter> writeBody)
    {
        if (readerNames.Count == 0)
            throw new ArgumentException("At least one type reader is required.", nameof(readerNames));

        using var bodyStream = new MemoryStream();
        using (var bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, leaveOpen: true))
        {
            Write7BitEncodedInt(bodyWriter, 1); // writer index + 1
            writeBody(bodyWriter);
        }

        using var manifestStream = new MemoryStream();
        using (var manifestWriter = new BinaryWriter(manifestStream, Encoding.UTF8, leaveOpen: true))
        {
            Write7BitEncodedInt(manifestWriter, readerNames.Count);
            foreach (var readerName in readerNames)
            {
                var name = Encoding.UTF8.GetBytes(readerName);
                Write7BitEncodedInt(manifestWriter, name.Length);
                manifestWriter.Write(name);
                manifestWriter.Write(0); // type reader version
            }

            Write7BitEncodedInt(manifestWriter, 0); // shared resource count
        }

        var manifest = manifestStream.ToArray();
        var body = bodyStream.ToArray();

        using var output = new MemoryStream(10 + manifest.Length + body.Length);
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)'X');
            writer.Write((byte)'N');
            writer.Write((byte)'B');
            writer.Write(PlatformWindows);
            writer.Write(FormatVersion);
            writer.Write(highDefinition ? HiDefFlag : (byte)0);
            writer.Write((uint)(10 + manifest.Length + body.Length));
            writer.Write(manifest);
            writer.Write(body);
        }

        AtomicFile.WriteAllBytes(outputPath, output.ToArray());
    }

    private static void Write7BitEncodedInt(BinaryWriter writer, int value)
    {
        var remaining = (uint)value;
        while (remaining >= 0x80)
        {
            writer.Write((byte)(remaining | 0x80));
            remaining >>= 7;
        }
        writer.Write((byte)remaining);
    }
}
