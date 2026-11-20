using System.IO;

namespace XnaContentCompiler.Core.Content;

/// <summary>
/// One source asset in a content project.
/// </summary>
public sealed class ContentItem
{
    public ContentItem()
    {
    }

    public ContentItem(string sourcePath)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        Kind = ContentTypeRegistry.Detect(SourcePath);
        OutputName = ContentTypeRegistry.DefaultOutputName(SourcePath);
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Absolute path of the source asset.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Output name relative to the project output directory, without the .xnb extension.</summary>
    public string OutputName { get; set; } = string.Empty;

    public ContentKind Kind { get; set; } = ContentKind.Unsupported;

    public TextureSettings Texture { get; set; } = new();

    public SpriteFontSettings SpriteFont { get; set; } = new();

    public string FileName => Path.GetFileName(SourcePath);
}
