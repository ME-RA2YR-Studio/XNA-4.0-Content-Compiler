using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class OutputNameValidatorTests
{
    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("foo.xnb", "foo")]
    [InlineData("dir/foo", "dir\\foo")]
    [InlineData("dir\\foo", "dir\\foo")]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("dir/sub/name.xnb", "dir\\sub\\name")]
    public void Normalize_AcceptsValidRelativeNames(string input, string expected)
    {
        Assert.True(OutputNameValidator.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..\\evil")]
    [InlineData("dir\\..\\evil")]
    [InlineData("C:\\absolute")]
    [InlineData("/absolute")]
    [InlineData("bad?.name")]
    [InlineData("trailing.")]
    public void Normalize_RejectsInvalidNames(string input)
    {
        Assert.False(OutputNameValidator.TryNormalize(input, out _));
    }

    [Fact]
    public void Project_CreatesUniqueOutputNames()
    {
        var project = new ContentProject();
        var first = project.CreateItem(Path.Combine(Path.GetTempPath(), "a.png"));
        project.AddItem(first);
        var second = project.CreateItem(Path.Combine(Path.GetTempPath(), "b.png"));

        Assert.Equal("a", first.OutputName);
        Assert.Equal("b", second.OutputName);

        second.OutputName = "a";
        project.AddItem(second);

        Assert.True(project.HasDuplicateOutputNames());
        Assert.Equal("a_2", project.GetUniqueOutputName("a"));
    }

    [Fact]
    public void CreateItems_ResolvesCollisionsInOnePass()
    {
        var root = Path.Combine(Path.GetTempPath(), "xncc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "one"));
        Directory.CreateDirectory(Path.Combine(root, "two"));

        try
        {
            var project = new ContentProject();
            var existing = project.CreateItem(Path.Combine(root, "one", "hero.png"));
            project.AddItem(existing);

            var created = project.CreateItems(new[]
            {
                Path.Combine(root, "one", "hero.png"),
                Path.Combine(root, "two", "hero.png"),
                Path.Combine(root, "two", "hero.png"),
            });

            Assert.Equal(3, created.Count);
            Assert.Equal(new[] { "hero_2", "hero_3", "hero_4" },
                created.Select(x => x.OutputName));
            Assert.False(project.HasDuplicateOutputNames());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
