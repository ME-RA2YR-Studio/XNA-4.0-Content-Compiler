namespace XnaContentCompiler.Core.Content;

/// <summary>
/// Content types that the compiler understands.
/// </summary>
public enum ContentKind
{
    Unsupported,
    Texture,
    SpriteFont,
    SoundEffect,
    Song,
}

/// <summary>
/// Graphics profile written into the XNB header. Reach is the safe default; HiDef
/// removes the power-of-two restriction from DXT compression and allows larger textures.
/// </summary>
public enum GraphicsProfileSetting
{
    Reach,
    HiDef,
}

/// <summary>
/// Output surface formats exposed by the texture processor.
/// </summary>
public enum TextureOutputFormat
{
    /// <summary>Uncompressed 32-bit BGRA data. Works with any texture size.</summary>
    Color,

    /// <summary>DXT compression chosen automatically (DXT1 when opaque, DXT5 otherwise). Requires power-of-two dimensions in the Reach profile.</summary>
    DxtCompressed,

    /// <summary>Keeps the source surface format unchanged, matching XNA's TextureProcessorOutputFormat.NoChange.</summary>
    NoChange,
}

/// <summary>
/// Texture format used for the glyph atlas of a compiled sprite font.
/// </summary>
public enum SpriteFontTextureFormat
{
    /// <summary>DXT compressed atlas. Produces smaller files and is the XNA default.</summary>
    Compressed,

    /// <summary>Uncompressed 32-bit BGRA atlas. Useful when compression artifacts are unacceptable.</summary>
    Color,
}
