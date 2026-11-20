using System.IO;

namespace XnaContentCompiler.Core.Content.Xnb;

/// <summary>
/// Reads the header of a compiled Texture2D XNB so the tool can warn about sizes the
/// XNA Reach profile cannot load.
/// </summary>
public static class XnbTextureInspector
{
    /// <summary>Maximum texture dimension supported by the XNA Reach profile.</summary>
    public const int ReachMaxDimension = 2048;

    /// <summary>Maximum texture dimension supported by the XNA HiDef profile.</summary>
    public const int HiDefMaxDimension = 4096;

    public static bool TryReadSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            return TryReadSize(File.ReadAllBytes(path), out width, out height);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryReadSize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            if (bytes.Length < 10
                || bytes[0] != 'X' || bytes[1] != 'N' || bytes[2] != 'B'
                || bytes[4] != 5
                || (bytes[5] & 0xC0) != 0)
            {
                return false;
            }

            var position = 10;

            var readerCount = Read7BitEncodedInt(bytes, ref position);
            if (readerCount < 0 || readerCount > 4096)
                return false;

            for (var index = 0; index < readerCount; index++)
            {
                var nameLength = Read7BitEncodedInt(bytes, ref position);
                if (nameLength < 0 || nameLength > bytes.Length - position)
                    return false;

                position += nameLength;

                if (position + 4 > bytes.Length)
                    return false;

                position += 4;
            }

            _ = Read7BitEncodedInt(bytes, ref position); // shared resources
            _ = Read7BitEncodedInt(bytes, ref position); // type writer index

            position += 4; // SurfaceFormat
            width = ReadInt32(bytes, ref position);
            height = ReadInt32(bytes, ref position);

            return width > 0 && height > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static int ReadInt32(byte[] bytes, ref int position)
    {
        if (position + 4 > bytes.Length)
            throw new InvalidDataException("Truncated XNB payload.");

        var value = BitConverter.ToInt32(bytes, position);
        position += 4;
        return value;
    }

    private static int Read7BitEncodedInt(byte[] bytes, ref int position)
    {
        var value = 0;
        var shift = 0;

        while (true)
        {
            if (position >= bytes.Length || shift > 28)
                throw new InvalidDataException("Invalid 7-bit encoded value.");

            var current = bytes[position++];
            value |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;

            shift += 7;
        }
    }
}
