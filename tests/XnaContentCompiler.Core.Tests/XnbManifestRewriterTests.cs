using XnaContentCompiler.Core.Content.Audio;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class XnbManifestRewriterTests
{
    [Fact]
    public void RewritesReaderNamesAndPreservesPayload()
    {
        var root = TestDirectory.Create();
        try
        {
            var input = Path.Combine(root, "input.xnb");
            var output = Path.Combine(root, "output.xnb");

            XnbSingleTypeWriter.Write(
                input,
                "Microsoft.Xna.Framework.Content.Texture2DReader",
                highDefinition: false,
                writer => writer.Write(1234));

            var result = XnbManifestRewriter.RewriteToXna(input, output);
            Assert.True(result.Changed);

            var info = XnbManifestRewriter.ReadInfo(output);
            Assert.Single(info.Readers);
            Assert.Equal(XnaTypeReaders.Texture2DReader, info.Readers[0].Name);

            var bytes = File.ReadAllBytes(output);
            var position = info.BodyOffset;
            Assert.Equal(1, Read7Bit(bytes, ref position));
            Assert.Equal(1234, BitConverter.ToInt32(bytes, position));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void RejectsCompressedXnbFiles()
    {
        var bytes = new byte[10];
        bytes[0] = (byte)'X';
        bytes[1] = (byte)'N';
        bytes[2] = (byte)'B';
        bytes[3] = (byte)'w';
        bytes[4] = 5;
        bytes[5] = 0x80;
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 6);

        Assert.Throws<XnbCompatibilityException>(() => XnbManifestRewriter.ReadInfo(bytes));
    }

    [Fact]
    public void RejectsNonXnbFiles()
    {
        Assert.Throws<InvalidDataException>(() => XnbManifestRewriter.ReadInfo(new byte[10]));
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
