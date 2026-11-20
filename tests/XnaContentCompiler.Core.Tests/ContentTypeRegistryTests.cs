using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class ContentTypeRegistryTests
{
    [Fact]
    public void SupportedTypesMatchTheOriginalContentCompiler()
    {
        var expected = new[]
        {
            ".bmp", ".jpg", ".png", ".tga", ".dds",
            ".spritefont",
            ".wav",
            ".mp3", ".wma",
        };

        Assert.Equal(
            expected.OrderBy(x => x, StringComparer.Ordinal),
            ContentTypeRegistry.SupportedExtensions.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(".bmp", ContentKind.Texture)]
    [InlineData(".jpg", ContentKind.Texture)]
    [InlineData(".png", ContentKind.Texture)]
    [InlineData(".tga", ContentKind.Texture)]
    [InlineData(".dds", ContentKind.Texture)]
    [InlineData(".PNG", ContentKind.Texture)]
    [InlineData(".spritefont", ContentKind.SpriteFont)]
    [InlineData(".wav", ContentKind.SoundEffect)]
    [InlineData(".mp3", ContentKind.Song)]
    [InlineData(".wma", ContentKind.Song)]
    [InlineData(".gif", ContentKind.Unsupported)]
    [InlineData(".jpeg", ContentKind.Unsupported)]
    [InlineData(".tif", ContentKind.Unsupported)]
    [InlineData(".webp", ContentKind.Unsupported)]
    [InlineData(".ogg", ContentKind.Unsupported)]
    public void DetectsContentKind(string extension, ContentKind expected)
    {
        Assert.Equal(expected, ContentTypeRegistry.Detect("asset" + extension));
    }
}
