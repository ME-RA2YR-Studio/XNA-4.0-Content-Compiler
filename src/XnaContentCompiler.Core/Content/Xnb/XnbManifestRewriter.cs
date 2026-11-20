using System.IO;
using System.Text;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.Core.Content.Xnb;

/// <summary>
/// Raised when a compiled XNB cannot be rewritten to the XNA 4.0 type identity
/// (compressed payload, unsupported format version or a corrupt header).
/// </summary>
public sealed class XnbCompatibilityException : NotSupportedException
{
    public XnbCompatibilityException(string message)
        : base(message)
    {
    }
}

public sealed class XnbReaderEntry
{
    public XnbReaderEntry(string name, int version)
    {
        Name = name;
        Version = version;
    }

    public string Name { get; }

    public int Version { get; }
}

public sealed class XnbInfo
{
    public byte Platform { get; init; }
    public byte FormatVersion { get; init; }
    public byte Flags { get; init; }
    public List<XnbReaderEntry> Readers { get; init; } = new();
    public int SharedResourceCount { get; init; }
    public int BodyOffset { get; init; }
}

public sealed class XnbRewriteResult
{
    public bool Changed { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Rewrites the type reader manifest of an uncompressed XNB file produced by the
/// MonoGame pipeline so the file references the canonical XNA 4.0 reader identities.
/// The payload bytes are copied verbatim.
/// </summary>
public static class XnbManifestRewriter
{
    private const byte XnaCompressedFlag = 0x80;
    private const byte MonoGameLz4Flag = 0x40;

    public static XnbInfo ReadInfo(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return ReadInfo(bytes);
    }

    public static XnbInfo ReadInfo(byte[] bytes)
    {
        ValidateHeader(bytes);
        var position = 10;

        var readerCount = Read7BitEncodedInt(bytes, ref position);
        if (readerCount < 0 || readerCount > 4096)
            throw new InvalidDataException($"Invalid XNB type reader count ({readerCount}).");

        var readers = new List<XnbReaderEntry>(readerCount);
        for (var index = 0; index < readerCount; index++)
        {
            var length = Read7BitEncodedInt(bytes, ref position);
            if (length < 0 || length > bytes.Length - position)
                throw new InvalidDataException("Truncated XNB type reader string.");

            var name = Encoding.UTF8.GetString(bytes, position, length);
            position += length;

            if (position + 4 > bytes.Length)
                throw new InvalidDataException("Truncated XNB type reader version.");

            var version = BitConverter.ToInt32(bytes, position);
            position += 4;
            readers.Add(new XnbReaderEntry(name, version));
        }

        var sharedResourceCount = Read7BitEncodedInt(bytes, ref position);

        return new XnbInfo
        {
            Platform = bytes[3],
            FormatVersion = bytes[4],
            Flags = bytes[5],
            Readers = readers,
            SharedResourceCount = sharedResourceCount,
            BodyOffset = position,
        };
    }

    public static XnbRewriteResult RewriteToXna(string sourcePath, string destinationPath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        ValidateHeader(bytes);

        var warnings = new List<string>();
        var declaredSize = BitConverter.ToUInt32(bytes, 6);
        if (declaredSize != bytes.Length)
            warnings.Add($"XNB header size {declaredSize} does not match the file length {bytes.Length}.");

        var position = 10;
        var readerCount = Read7BitEncodedInt(bytes, ref position);
        if (readerCount < 0 || readerCount > 4096)
            throw new InvalidDataException($"Invalid XNB type reader count ({readerCount}).");

        var rewritten = new List<XnbReaderEntry>(readerCount);
        var changed = false;

        for (var index = 0; index < readerCount; index++)
        {
            var length = Read7BitEncodedInt(bytes, ref position);
            if (length < 0 || length > bytes.Length - position)
                throw new InvalidDataException("Truncated XNB type reader string.");

            var name = Encoding.UTF8.GetString(bytes, position, length);
            position += length;

            if (position + 4 > bytes.Length)
                throw new InvalidDataException("Truncated XNB type reader version.");

            var version = BitConverter.ToInt32(bytes, position);
            position += 4;

            if (XnaTypeReaders.TryMap(name, out var mapped))
            {
                if (!string.Equals(mapped, name, StringComparison.Ordinal))
                    changed = true;
                rewritten.Add(new XnbReaderEntry(mapped, version));
            }
            else
            {
                warnings.Add($"Unknown content type reader '{name}' was left unchanged.");
                rewritten.Add(new XnbReaderEntry(name, version));
            }
        }

        var sharedResourceCount = Read7BitEncodedInt(bytes, ref position);
        var body = new byte[bytes.Length - position];
        Array.Copy(bytes, position, body, 0, body.Length);

        using var manifestStream = new MemoryStream();
        using (var manifestWriter = new BinaryWriter(manifestStream, Encoding.UTF8, leaveOpen: true))
        {
            Write7BitEncodedInt(manifestWriter, rewritten.Count);
            foreach (var reader in rewritten)
            {
                var encoded = Encoding.UTF8.GetBytes(reader.Name);
                Write7BitEncodedInt(manifestWriter, encoded.Length);
                manifestWriter.Write(encoded);
                manifestWriter.Write(reader.Version);
            }
            Write7BitEncodedInt(manifestWriter, sharedResourceCount);
        }

        var manifest = manifestStream.ToArray();

        using var outputStream = new MemoryStream(10 + manifest.Length + body.Length);
        using (var writer = new BinaryWriter(outputStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)'X');
            writer.Write((byte)'N');
            writer.Write((byte)'B');
            writer.Write(bytes[3]);
            writer.Write(bytes[4]);
            writer.Write(bytes[5]);
            writer.Write((uint)(10 + manifest.Length + body.Length));
            writer.Write(manifest);
            writer.Write(body);
        }

        AtomicFile.WriteAllBytes(destinationPath, outputStream.ToArray());

        return new XnbRewriteResult
        {
            Changed = changed,
            Warnings = warnings,
        };
    }

    private static void ValidateHeader(byte[] bytes)
    {
        if (bytes.Length < 10)
            throw new InvalidDataException("The file is too small to be an XNB file.");

        if (bytes[0] != 'X' || bytes[1] != 'N' || bytes[2] != 'B')
            throw new InvalidDataException("The file is not an XNB file.");

        if (bytes[4] != 5)
        {
            throw new XnbCompatibilityException(
                $"Only XNB format version 5 (XNA 4.0) is supported; found version {bytes[4]}.");
        }

        if ((bytes[5] & XnaCompressedFlag) != 0)
        {
            throw new XnbCompatibilityException(
                "Compressed XNB files (LZX) cannot be rewritten. Disable content compression and rebuild.");
        }

        if ((bytes[5] & MonoGameLz4Flag) != 0)
        {
            throw new XnbCompatibilityException(
                "Compressed XNB files (LZ4) cannot be rewritten. Disable content compression and rebuild.");
        }
    }

    private static int Read7BitEncodedInt(byte[] bytes, ref int position)
    {
        var value = 0;
        var shift = 0;

        while (true)
        {
            if (position >= bytes.Length || shift > 28)
                throw new InvalidDataException("Invalid 7-bit encoded value in the XNB file.");

            var current = bytes[position++];
            value |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;

            shift += 7;
        }
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
