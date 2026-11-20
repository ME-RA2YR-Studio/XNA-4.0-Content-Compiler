using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class XnbTextureInspectorTests
{
    [Fact]
    public void ReadsTextureDimensions()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "pixel.xnb");
            WriteTextureXnb(path, 32, 16);

            Assert.True(XnbTextureInspector.TryReadSize(path, out var width, out var height));
            Assert.Equal(32, width);
            Assert.Equal(16, height);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReturnsFalseForNonXnbData()
    {
        Assert.False(XnbTextureInspector.TryReadSize(new byte[] { 1, 2, 3, 4 }, out _, out _));
    }

    private static void WriteTextureXnb(string path, int width, int height)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Header.
        writer.Write((byte)'X');
        writer.Write((byte)'N');
        writer.Write((byte)'B');
        writer.Write((byte)'w');
        writer.Write((byte)5);
        writer.Write((byte)0);
        writer.Write(0u); // patched below

        // Type manifest.
        writer.Write((byte)1);
        var name = System.Text.Encoding.UTF8.GetBytes(
            "Microsoft.Xna.Framework.Content.Texture2DReader");
        writer.Write((byte)name.Length);
        writer.Write(name);
        writer.Write(0); // reader version
        writer.Write((byte)0); // shared resources
        writer.Write((byte)1); // type writer index

        // Texture payload.
        writer.Write(0); // SurfaceFormat.Color
        writer.Write(width);
        writer.Write(height);
        writer.Write(1); // mip count
        var data = new byte[width * height * 4];
        writer.Write(data.Length);
        writer.Write(data);

        writer.Flush();
        var bytes = stream.ToArray();
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 6);
        File.WriteAllBytes(path, bytes);
    }
}
