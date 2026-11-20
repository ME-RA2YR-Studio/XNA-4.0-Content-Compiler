using System.IO;

namespace XnaContentCompiler.Core.Content;

/// <summary>
/// Maps source file extensions to content kinds and default processors.
/// </summary>
public static class ContentTypeRegistry
{
    private static readonly string[] TextureExtensions =
    {
        ".bmp", ".jpg", ".png", ".tga", ".dds",
    };

    private const string SpriteFontExtension = ".spritefont";
    private const string SoundEffectExtension = ".wav";
    private static readonly string[] SongExtensions = { ".mp3", ".wma" };

    public static IReadOnlyList<string> SupportedExtensions { get; } = BuildSupportedExtensions();

    public static ContentKind Detect(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (Array.IndexOf(TextureExtensions, extension) >= 0)
            return ContentKind.Texture;

        if (extension == SpriteFontExtension)
            return ContentKind.SpriteFont;

        if (extension == SoundEffectExtension)
            return ContentKind.SoundEffect;

        if (Array.IndexOf(SongExtensions, extension) >= 0)
            return ContentKind.Song;

        return ContentKind.Unsupported;
    }

    public static string DefaultOutputName(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    private static List<string> BuildSupportedExtensions()
    {
        var list = new List<string>(TextureExtensions);
        list.Add(SpriteFontExtension);
        list.Add(SoundEffectExtension);
        list.AddRange(SongExtensions);
        return list;
    }
}
