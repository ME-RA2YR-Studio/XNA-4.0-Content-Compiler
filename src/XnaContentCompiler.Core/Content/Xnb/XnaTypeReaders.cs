namespace XnaContentCompiler.Core.Content.Xnb;

/// <summary>
/// Canonical XNA Game Studio 4.0 content type reader names.
/// XNB files reference these by assembly identity, so writing them (instead of the
/// MonoGame identities used internally) keeps the output loadable by XNA 4.0 and
/// by MonoGame/FNA (which remap the XNA identities at runtime).
/// </summary>
public static class XnaTypeReaders
{
    public const string GraphicsAssembly =
        "Microsoft.Xna.Framework.Graphics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553";

    public const string CoreAssembly =
        "Microsoft.Xna.Framework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=842cf8be1de50553";

    public const string MscorlibAssembly =
        "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

    public const string Texture2DReader =
        "Microsoft.Xna.Framework.Content.Texture2DReader, " + GraphicsAssembly;

    public const string SpriteFontReader =
        "Microsoft.Xna.Framework.Content.SpriteFontReader, " + GraphicsAssembly;

    public const string SoundEffectReader =
        "Microsoft.Xna.Framework.Content.SoundEffectReader, " + CoreAssembly;

    public const string SongReader =
        "Microsoft.Xna.Framework.Content.SongReader, " + CoreAssembly;

    public const string Int32Reader =
        "Microsoft.Xna.Framework.Content.Int32Reader, " + CoreAssembly;

    /// <summary>
    /// Reader types this compiler can emit, mapped to the XNA assembly that declares them.
    /// Anything else is left untouched so an unsupported type is never silently pointed
    /// at the wrong assembly.
    /// </summary>
    private static readonly Dictionary<string, string> ReaderAssemblies = new(StringComparer.Ordinal)
    {
        ["Microsoft.Xna.Framework.Content.Texture2DReader"] = GraphicsAssembly,
        ["Microsoft.Xna.Framework.Content.SpriteFontReader"] = GraphicsAssembly,
        ["Microsoft.Xna.Framework.Content.SoundEffectReader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.SongReader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.RectangleReader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.CharReader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.Vector3Reader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.Vector2Reader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.StringReader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.Int32Reader"] = CoreAssembly,
        ["Microsoft.Xna.Framework.Content.SingleReader"] = CoreAssembly,
    };

    private const string ListReaderPrefix = "Microsoft.Xna.Framework.Content.ListReader`1";

    /// <summary>
    /// Rewrites a content type reader name produced by the bundled pipeline into the
    /// equivalent XNA 4.0 name. Returns false for reader types this compiler does not
    /// produce (those are left untouched).
    /// </summary>
    public static bool TryMap(string readerName, out string mappedName)
    {
        mappedName = readerName;

        var value = readerName.Trim();
        if (value.Length == 0)
            return false;

        var genericStart = value.IndexOf("[[", StringComparison.Ordinal);
        if (genericStart >= 0)
        {
            var genericEnd = value.LastIndexOf("]]", StringComparison.Ordinal);
            if (genericEnd < genericStart)
                return false;

            var outerType = value[..genericStart].Trim();
            if (!outerType.Equals(ListReaderPrefix, StringComparison.Ordinal))
                return false;

            var inner = value[(genericStart + 2)..genericEnd].Trim();

            var comma = inner.IndexOf(',');
            var elementType = (comma >= 0 ? inner[..comma] : inner).Trim();
            if (!IsKnownElementType(elementType))
                return false;

            var elementAssembly = GetElementAssembly(elementType);
            mappedName = $"{outerType}[[{elementType}, {elementAssembly}]], {CoreAssembly}";
            return true;
        }

        var typeName = value.Split(',')[0].Trim();
        if (!ReaderAssemblies.TryGetValue(typeName, out var assembly))
            return false;

        mappedName = $"{typeName}, {assembly}";
        return true;
    }

    private static bool IsKnownElementType(string typeName)
    {
        return typeName.StartsWith("Microsoft.Xna.Framework.", StringComparison.Ordinal)
            || typeName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static string GetElementAssembly(string typeName)
    {
        if (typeName.StartsWith("Microsoft.Xna.Framework.Graphics.", StringComparison.Ordinal))
            return GraphicsAssembly;

        if (typeName.StartsWith("Microsoft.Xna.Framework.", StringComparison.Ordinal))
            return CoreAssembly;

        if (typeName.StartsWith("System.", StringComparison.Ordinal))
            return MscorlibAssembly;

        return CoreAssembly;
    }
}
