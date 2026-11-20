using System.IO;
using System.Text;
using XnaContentCompiler.Core.Content.Xnb;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.Core.Content.Audio;

/// <summary>
/// Writes an XNA 4.0 Song XNB. Song content does not embed audio; the XNB stores a
/// relative media file name and the duration, and the media file is copied alongside.
/// </summary>
public static class MusicXnbWriter
{
    public static void Write(
        string outputPath,
        string mediaFileName,
        int durationMilliseconds,
        GraphicsProfileSetting profile)
    {
        XnbSingleTypeWriter.Write(
            outputPath,
            new[] { XnaTypeReaders.SongReader, XnaTypeReaders.Int32Reader },
            profile == GraphicsProfileSetting.HiDef,
            body =>
            {
                body.Write(mediaFileName);

                // Matches SongWriter: WriteObject<int> prefixes the duration with the
                // Int32 type writer index (2, after the Song reader).
                Write7BitEncodedInt(body, 2);
                body.Write(durationMilliseconds);
            });
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

/// <summary>
/// Determines the playback duration of MP3 and WMA files, matching the value XNA's
/// SongContent stores.
/// </summary>
public static class SongInfoReader
{
    public static int GetDurationMilliseconds(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => GetMp3Duration(bytes),
            ".wma" => GetWmaDuration(bytes),
            _ => 0,
        };
    }

    private static int GetMp3Duration(byte[] bytes)
    {
        var position = 0;

        // Skip an ID3v2 tag when present.
        if (bytes.Length >= 10
            && bytes[0] == 'I' && bytes[1] == 'D' && bytes[2] == '3')
        {
            var size = ((bytes[6] & 0x7F) << 21)
                | ((bytes[7] & 0x7F) << 14)
                | ((bytes[8] & 0x7F) << 7)
                | (bytes[9] & 0x7F);

            position = 10 + size;
        }

        var frameStart = FindFrameSync(bytes, position);
        if (frameStart < 0 || frameStart + 4 > bytes.Length)
            return 0;

        var header = (uint)((bytes[frameStart] << 24)
            | (bytes[frameStart + 1] << 16)
            | (bytes[frameStart + 2] << 8)
            | bytes[frameStart + 3]);

        var versionBits = (header >> 19) & 0x3; // 0=MPEG2.5, 2=MPEG2, 3=MPEG1
        var layerBits = (header >> 17) & 0x3;   // 1=Layer III
        if (versionBits == 1 || layerBits != 1)
            return 0;

        var bitrateIndex = (int)((header >> 12) & 0xF);
        var sampleRateIndex = (int)((header >> 10) & 0x3);
        var padding = (int)((header >> 9) & 0x1);
        var channelMode = (int)((header >> 6) & 0x3);

        var isMpeg1 = versionBits == 3;
        var bitrate = GetBitrateKbps(isMpeg1, bitrateIndex);
        var sampleRate = GetSampleRate((int)versionBits, sampleRateIndex);

        if (bitrate <= 0 || sampleRate <= 0)
            return 0;

        var samplesPerFrame = isMpeg1 ? 1152 : 576;
        var frameLength = (isMpeg1 ? 144 : 72) * bitrate * 1000 / sampleRate + padding;

        // Xing/Info header carries the exact frame count for VBR files.
        var sideInfo = isMpeg1 ? (channelMode == 3 ? 17 : 32) : (channelMode == 3 ? 9 : 17);
        var xingOffset = frameStart + 4 + sideInfo;
        if (xingOffset + 12 <= bytes.Length
            && (IsTag(bytes, xingOffset, "Xing") || IsTag(bytes, xingOffset, "Info")))
        {
            var flags = ReadUInt32BigEndian(bytes, xingOffset + 4);
            if ((flags & 0x1) != 0)
            {
                var frames = ReadUInt32BigEndian(bytes, xingOffset + 8);
                if (frames > 0)
                    return (int)Math.Round(frames * (double)samplesPerFrame * 1000 / sampleRate);
            }
        }

        if (frameLength <= 0)
            return 0;

        var audioBytes = bytes.Length - frameStart;
        var estimatedFrames = audioBytes / frameLength;

        return (int)Math.Round(estimatedFrames * (double)samplesPerFrame * 1000 / sampleRate);
    }

    private static int FindFrameSync(byte[] bytes, int start)
    {
        for (var index = Math.Max(0, start); index + 1 < bytes.Length; index++)
        {
            if (bytes[index] == 0xFF && (bytes[index + 1] & 0xE0) == 0xE0)
                return index;
        }

        return -1;
    }

    private static int GetBitrateKbps(bool mpeg1, int index)
    {
        int[] table = mpeg1
            ? new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 }
            : new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 };

        return index >= 0 && index < table.Length ? table[index] : 0;
    }

    private static int GetSampleRate(int versionBits, int index)
    {
        int[] baseRates = { 44100, 48000, 32000 };
        if (index < 0 || index > 2)
            return 0;

        return versionBits switch
        {
            3 => baseRates[index],
            2 => baseRates[index] / 2,
            _ => baseRates[index] / 4,
        };
    }

    private static bool IsTag(byte[] bytes, int offset, string tag)
    {
        return offset + tag.Length <= bytes.Length
            && bytes[offset] == tag[0]
            && bytes[offset + 1] == tag[1]
            && bytes[offset + 2] == tag[2]
            && bytes[offset + 3] == tag[3];
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
    {
        if (offset + 4 > bytes.Length)
            return 0;

        return (uint)((bytes[offset] << 24)
            | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8)
            | bytes[offset + 3]);
    }

    private static int GetWmaDuration(byte[] bytes)
    {
        // ASF File Properties Object GUID.
        byte[] guid =
        {
            0xA1, 0xDC, 0xAB, 0x8C, 0x47, 0xA9, 0xCF, 0x11,
            0x8E, 0xE4, 0x00, 0xC0, 0x0C, 0x20, 0x53, 0x65,
        };

        for (var offset = 0; offset + 96 <= bytes.Length; offset++)
        {
            if (!Matches(bytes, offset, guid))
                continue;

            var playDuration = BitConverter.ToUInt64(bytes, offset + 64);
            var preroll = BitConverter.ToUInt64(bytes, offset + 80);

            var milliseconds = (long)(playDuration / 10000) - (long)preroll;
            return milliseconds > 0 ? (int)milliseconds : 0;
        }

        return 0;
    }

    private static bool Matches(byte[] bytes, int offset, byte[] pattern)
    {
        for (var index = 0; index < pattern.Length; index++)
        {
            if (bytes[offset + index] != pattern[index])
                return false;
        }

        return true;
    }
}
