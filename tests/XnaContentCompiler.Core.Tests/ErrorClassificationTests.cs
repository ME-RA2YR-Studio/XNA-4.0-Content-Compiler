using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;
using XnaContentCompiler.Core.Content.Xnb;

namespace XnaContentCompiler.Core.Tests;

public sealed class ErrorClassificationTests
{
    [Fact]
    public void UnsupportedWaveFormatIsReportedAsAudioFailure()
    {
        var root = TestDirectory.Create();
        try
        {
            var wavPath = Path.Combine(root, "adpcm.wav");
            File.WriteAllBytes(wavPath, TestDirectory.BuildWav(2, 1, 8000, 4, new byte[64]));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            var item = new ContentItem(wavPath) { OutputName = "adpcm" };
            project.AddItem(item);

            var summary = new ContentBuildService().Build(project);

            Assert.Equal(0, summary.Succeeded);
            Assert.Equal(1, summary.Failed);
            Assert.Equal(BuildErrorCode.AudioBuildFailed, summary.Items[0].ErrorCode);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void MalformedSpriteFontIsReportedAsFontFailure()
    {
        var root = TestDirectory.Create();
        try
        {
            var fontPath = Path.Combine(root, "broken.spritefont");
            File.WriteAllText(fontPath, "<XnaContent><Asset Type=\"Graphics:FontDescription\">");

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            var item = new ContentItem(fontPath) { OutputName = "broken" };
            project.AddItem(item);

            var summary = new ContentBuildService().Build(project);

            Assert.Equal(0, summary.Succeeded);
            Assert.Equal(1, summary.Failed);
            Assert.Equal(BuildErrorCode.FontBuildFailed, summary.Items[0].ErrorCode);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void CompressedXnbRaisesDedicatedCompatibilityException()
    {
        var bytes = new byte[10];
        bytes[0] = (byte)'X';
        bytes[1] = (byte)'N';
        bytes[2] = (byte)'B';
        bytes[3] = (byte)'w';
        bytes[4] = 5;
        bytes[5] = 0x40;
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 6);

        Assert.Throws<XnbCompatibilityException>(() => XnbManifestRewriter.ReadInfo(bytes));
    }

    [Fact]
    public void TruncatedReaderNameDoesNotThrowFromInspector()
    {
        var bytes = new byte[]
        {
            (byte)'X', (byte)'N', (byte)'B', (byte)'w', 5, 0,
            0, 0, 0, 0,      // size (unused by the inspector)
            1,               // one reader
            100,             // claims a 100 byte name that is not present
        };

        Assert.False(XnbTextureInspector.TryReadSize(bytes, out _, out _));
    }

    [Fact]
    public void TruncatedReaderNameThrowsForManifestRewrite()
    {
        var bytes = new byte[]
        {
            (byte)'X', (byte)'N', (byte)'B', (byte)'w', 5, 0,
            0, 0, 0, 0,
            1,
            100,
        };

        Assert.Throws<InvalidDataException>(() => XnbManifestRewriter.ReadInfo(bytes));
    }
}
