namespace XnaContentCompiler.Core.Content;

/// <summary>
/// Per-sprite-font processing options.
/// </summary>
public sealed class SpriteFontSettings
{
    public SpriteFontTextureFormat TextureFormat { get; set; } = SpriteFontTextureFormat.Compressed;

    public bool PremultiplyAlpha { get; set; } = true;

    public SpriteFontSettings Clone()
    {
        return new SpriteFontSettings
        {
            TextureFormat = TextureFormat,
            PremultiplyAlpha = PremultiplyAlpha,
        };
    }
}
