using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class RenameItemTests
{
    [Fact]
    public void RenamesAndNormalizesOutputName()
    {
        var project = new ContentProject();
        var item = new ContentItem { SourcePath = "a.png", OutputName = "a", Kind = ContentKind.Texture };
        project.AddItem(item);

        Assert.True(project.TryRenameItem(item, "textures/hero.xnb", out var error));
        Assert.Equal(OutputNameError.None, error);
        Assert.Equal("textures\\hero", item.OutputName);
    }

    [Fact]
    public void RejectsInvalidNames()
    {
        var project = new ContentProject();
        var item = new ContentItem { SourcePath = "a.png", OutputName = "a", Kind = ContentKind.Texture };
        project.AddItem(item);

        Assert.False(project.TryRenameItem(item, "..\\escape", out var error));
        Assert.Equal(OutputNameError.Invalid, error);
        Assert.Equal("a", item.OutputName);
    }

    [Fact]
    public void RejectsDuplicateNamesCaseInsensitively()
    {
        var project = new ContentProject();
        var first = new ContentItem { SourcePath = "a.png", OutputName = "hero", Kind = ContentKind.Texture };
        var second = new ContentItem { SourcePath = "b.png", OutputName = "villain", Kind = ContentKind.Texture };
        project.AddItem(first);
        project.AddItem(second);

        Assert.False(project.TryRenameItem(second, "HERO", out var error));
        Assert.Equal(OutputNameError.Duplicate, error);
        Assert.Equal("villain", second.OutputName);
    }

    [Fact]
    public void RejectsItemsNotInTheProject()
    {
        var project = new ContentProject();
        var item = new ContentItem { SourcePath = "a.png", OutputName = "a", Kind = ContentKind.Texture };

        Assert.False(project.TryRenameItem(item, "renamed", out var error));
        Assert.Equal(OutputNameError.UnknownItem, error);
    }

    [Fact]
    public void AllowsRenamingToTheSameName()
    {
        var project = new ContentProject();
        var item = new ContentItem { SourcePath = "a.png", OutputName = "hero", Kind = ContentKind.Texture };
        project.AddItem(item);

        Assert.True(project.TryRenameItem(item, "hero", out _));
    }
}
