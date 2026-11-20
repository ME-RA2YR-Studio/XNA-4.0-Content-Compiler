using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class BuildScopeTests
{
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void BuildsOnlySelectedItems()
    {
        var root = TestDirectory.Create();
        try
        {
            var firstPath = Path.Combine(root, "first.png");
            var secondPath = Path.Combine(root, "second.png");
            File.WriteAllBytes(firstPath, Convert.FromBase64String(OnePixelPng));
            File.WriteAllBytes(secondPath, Convert.FromBase64String(OnePixelPng));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };

            var first = new ContentItem(firstPath) { OutputName = "first" };
            var second = new ContentItem(secondPath) { OutputName = "second" };
            project.AddItem(first);
            project.AddItem(second);

            var service = new ContentBuildService();
            var summary = service.Build(project, new[] { second });

            Assert.Equal(1, summary.Succeeded);
            Assert.Equal(0, summary.Failed);
            Assert.True(File.Exists(Path.Combine(project.OutputDirectory, "second.xnb")));
            Assert.False(File.Exists(Path.Combine(project.OutputDirectory, "first.xnb")));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void SubsetIgnoresDuplicateNamesOutsideTheSelection()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "pixel.png");
            File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPng));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };

            var selected = new ContentItem(path) { OutputName = "shared" };
            var other = new ContentItem(path) { OutputName = "shared" };
            project.AddItem(selected);
            project.AddItem(other);

            var service = new ContentBuildService();
            var summary = service.Build(project, new[] { selected });

            Assert.Equal(1, summary.Succeeded);
            Assert.Equal(0, summary.Failed);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void ReplacesExistingOutputAtomically()
    {
        var root = TestDirectory.Create();
        try
        {
            var path = Path.Combine(root, "pixel.png");
            File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPng));

            var project = new ContentProject { OutputDirectory = Path.Combine(root, "out") };
            Directory.CreateDirectory(project.OutputDirectory);

            var output = Path.Combine(project.OutputDirectory, "pixel.xnb");
            File.WriteAllBytes(output, new byte[] { 1, 2, 3 });

            var item = new ContentItem(path) { OutputName = "pixel" };
            project.AddItem(item);

            var service = new ContentBuildService();
            var summary = service.Build(project);

            Assert.Equal(1, summary.Succeeded);

            var info = XnaContentCompiler.Core.Content.Xnb.XnbManifestRewriter.ReadInfo(output);
            Assert.Equal(XnaContentCompiler.Core.Content.Xnb.XnaTypeReaders.Texture2DReader,
                info.Readers[0].Name);
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }
}
