using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace XnaContentCompiler.Core.Content.Fonts;

public sealed class PreparedSpriteFont
{
    public required string SourcePath { get; init; }
    public string? ResolvedFontPath { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Creates a temporary copy of a .spritefont file whose FontName points at a concrete
/// font file resolved by <see cref="WindowsFontResolver"/>. The original file is never
/// modified.
/// </summary>
public static class SpriteFontPreprocessor
{
    public static PreparedSpriteFont Prepare(string spriteFontPath, string workingDirectory)
    {
        Directory.CreateDirectory(workingDirectory);

        var document = XDocument.Load(spriteFontPath, LoadOptions.PreserveWhitespace);
        NormalizeCharacterCodes(document);

        var fontNameElement = document.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "FontName");
        var styleElement = document.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "Style");

        var fontName = fontNameElement?.Value.Trim() ?? string.Empty;
        var style = styleElement?.Value.Trim() ?? "Regular";

        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(spriteFontPath))!;
        var resolved = WindowsFontResolver.Resolve(fontName, style, baseDirectory);

        var tempPath = Path.Combine(
            workingDirectory,
            $"font_{Guid.NewGuid():N}.spritefont");

        string? warning = null;

        if (resolved != null && File.Exists(resolved))
        {
            var extension = Path.GetExtension(resolved);
            var fontCopyName = $"font_{Guid.NewGuid():N}{extension}";
            var fontCopyPath = Path.Combine(workingDirectory, fontCopyName);
            File.Copy(resolved, fontCopyPath, overwrite: true);

            if (fontNameElement != null)
                fontNameElement.Value = fontCopyName;

            document.Save(tempPath);
            return new PreparedSpriteFont
            {
                SourcePath = tempPath,
                ResolvedFontPath = resolved,
            };
        }

        warning = $"Could not resolve font '{fontName}' ({style}); letting the pipeline search by family name.";
        document.Save(tempPath);

        return new PreparedSpriteFont
        {
            SourcePath = tempPath,
            Warning = warning,
        };
    }

    /// <summary>
    /// XNA sprite font files encode character ranges as literal characters (a space
    /// character for the first code point). The bundled intermediate serializer splits
    /// element text on whitespace, so literal whitespace characters break the import.
    /// Rewriting every code point as a decimal UTF-16 value avoids that.
    /// </summary>
    private static void NormalizeCharacterCodes(XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            var localName = element.Name.LocalName;
            var isRegionCode = (localName is "Start" or "End")
                && element.Parent?.Name.LocalName is "CharacterRegion";
            var isDefaultCharacter = localName == "DefaultCharacter";

            if (!isRegionCode && !isDefaultCharacter)
                continue;

            var raw = element.Value;
            if (string.IsNullOrEmpty(raw))
                continue;

            if (TryParseCharacter(raw, out var character))
                element.Value = ((int)character).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool TryParseCharacter(string raw, out char character)
    {
        character = '\0';

        if (raw.Length == 1)
        {
            character = raw[0];
            return true;
        }

        var text = raw.Trim();
        if (text.Length == 1)
        {
            character = text[0];
            return true;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var hexValue)
            && hexValue is >= 0 and <= 0xFFFF)
        {
            character = (char)hexValue;
            return true;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value is >= 0 and <= 0xFFFF)
        {
            character = (char)value;
            return true;
        }

        return false;
    }
}
