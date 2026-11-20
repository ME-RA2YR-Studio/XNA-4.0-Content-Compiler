namespace XnaContentCompiler.Core.Content;

/// <summary>
/// Per-texture processing options. Defaults follow the standard XNA TextureProcessor
/// values; the texture size is never changed unless requested.
/// </summary>
public sealed class TextureSettings
{
    public TextureOutputFormat OutputFormat { get; set; } = TextureOutputFormat.Color;

    public bool GenerateMipmaps { get; set; }

    public bool PremultiplyAlpha { get; set; } = true;

    public bool ResizeToPowerOfTwo { get; set; }

    public bool MakeSquare { get; set; }

    /// <summary>Matches XNA's TextureProcessor default: magenta pixels become transparent.</summary>
    public bool ColorKeyEnabled { get; set; } = true;

    /// <summary>Color key in "#RRGGBB" form.</summary>
    public string ColorKeyColor { get; set; } = "#FF00FF";

    public TextureSettings Clone()
    {
        return new TextureSettings
        {
            OutputFormat = OutputFormat,
            GenerateMipmaps = GenerateMipmaps,
            PremultiplyAlpha = PremultiplyAlpha,
            ResizeToPowerOfTwo = ResizeToPowerOfTwo,
            MakeSquare = MakeSquare,
            ColorKeyEnabled = ColorKeyEnabled,
            ColorKeyColor = ColorKeyColor,
        };
    }
}
