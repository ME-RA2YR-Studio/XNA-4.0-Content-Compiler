using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class BuildServiceValidationTests
{
    [Fact]
    public void ReportsValidationFailuresWithoutRunningThePipeline()
    {
        var root = TestDirectory.Create();
        try
        {
            var output = Path.Combine(root, "out");
            File.WriteAllBytes(Path.Combine(root, "a.wav"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(root, "b.wav"), Array.Empty<byte>());
            File.WriteAllText(Path.Combine(root, "file.txt"), "unsupported");

            var project = new ContentProject { OutputDirectory = output };

            var missing = new ContentItem
            {
                SourcePath = Path.Combine(root, "missing.png"),
                OutputName = "missing",
                Kind = ContentKind.Texture,
            };
            var duplicateA = new ContentItem
            {
                SourcePath = Path.Combine(root, "a.wav"),
                OutputName = "dup",
                Kind = ContentKind.SoundEffect,
            };
            var duplicateB = new ContentItem
            {
                SourcePath = Path.Combine(root, "b.wav"),
                OutputName = "dup",
                Kind = ContentKind.SoundEffect,
            };
            var unsupported = new ContentItem
            {
                SourcePath = Path.Combine(root, "file.txt"),
                OutputName = "unsupported",
                Kind = ContentKind.Unsupported,
            };

            project.AddItem(missing);
            project.AddItem(duplicateA);
            project.AddItem(duplicateB);
            project.AddItem(unsupported);

            var service = new ContentBuildService();
            var summary = service.Build(project);

            Assert.Equal(0, summary.Succeeded);
            Assert.Equal(4, summary.Failed);
            Assert.False(summary.Canceled);

            Assert.Equal(BuildErrorCode.SourceNotFound, summary.Items[0].ErrorCode);
            Assert.Equal(BuildErrorCode.DuplicateOutputName, summary.Items[1].ErrorCode);
            Assert.Equal(BuildErrorCode.DuplicateOutputName, summary.Items[2].ErrorCode);
            Assert.Equal(BuildErrorCode.UnsupportedContentType, summary.Items[3].ErrorCode);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ThrowsForMissingOutputDirectory()
    {
        var project = new ContentProject { OutputDirectory = "" };
        var service = new ContentBuildService();

        var error = Assert.Throws<BuildConfigurationException>(() => service.Build(project));
        Assert.Equal(BuildErrorCode.InvalidOutputDirectory, error.Code);
    }
}
