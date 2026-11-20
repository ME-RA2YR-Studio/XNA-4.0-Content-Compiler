using System.IO;
using Microsoft.Win32;

namespace XnaContentCompiler.Core.Content.Fonts;

public sealed class ResolvedFont
{
    public ResolvedFont(string path, string family, string styleText)
    {
        Path = path;
        Family = family;
        StyleText = styleText;
    }

    public string Path { get; }
    public string Family { get; }
    public string StyleText { get; }
}

/// <summary>
/// Resolves sprite font family names such as "Arial" plus a style ("Bold", "Italic")
/// to a concrete font file. Pre-resolving the file lets the bundled processor honor
/// font styles, which it otherwise ignores when looking fonts up by family name.
/// </summary>
public static class WindowsFontResolver
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] FontDirectories =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts"),
    };

    private static readonly string[] FontExtensions = { ".ttf", ".otf", ".ttc" };

    public static string? Resolve(string fontName, string style, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(fontName))
            return null;

        var key = fontName + "|" + style + "|" + baseDirectory;
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;
        }

        var resolved = ResolveCore(fontName.Trim(), style, baseDirectory);

        lock (CacheLock)
            Cache[key] = resolved;

        return resolved;
    }

    private static string? ResolveCore(string fontName, string style, string baseDirectory)
    {
        // Explicit font file, absolute or relative to the .spritefont file.
        var explicitFile = Path.IsPathRooted(fontName)
            ? fontName
            : Path.Combine(baseDirectory, fontName);

        if (LooksLikeFontFile(fontName) && File.Exists(explicitFile))
            return Path.GetFullPath(explicitFile);

        var fromRegistry = ResolveFromRegistry(fontName, style);
        if (fromRegistry != null)
            return fromRegistry;

        var normalizedStyle = style.Replace(" ", string.Empty).ToLowerInvariant();
        foreach (var directory in FontDirectories)
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (var candidate in BuildFileCandidates(fontName, normalizedStyle))
            {
                foreach (var extension in FontExtensions)
                {
                    var path = Path.Combine(directory, candidate + extension);
                    if (File.Exists(path))
                        return path;
                }
            }
        }

        return null;
    }

    private static string? ResolveFromRegistry(string fontName, string style)
    {
        var requestedStyle = ParseStyle(style);

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", writable: false);

                if (key is null)
                    continue;

                var bestPath = (string?)null;
                var bestScore = int.MinValue;

                foreach (var valueName in key.GetValueNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var displayName = valueName
                        .Replace(" (TrueType)", string.Empty, StringComparison.OrdinalIgnoreCase)
                        .Replace(" (OpenType)", string.Empty, StringComparison.OrdinalIgnoreCase)
                        .Trim();

                    var score = ScoreMatch(displayName, fontName, requestedStyle);
                    if (score < 0 || score < bestScore)
                        continue;

                    var rawValue = key.GetValue(valueName);
                    if (rawValue is not string registryPath || string.IsNullOrWhiteSpace(registryPath))
                        continue;

                    var nullIndex = registryPath.IndexOf('\0');
                    if (nullIndex >= 0)
                        registryPath = registryPath[..nullIndex];

                    var candidate = Path.IsPathRooted(registryPath)
                        ? registryPath
                        : Path.Combine(FontDirectories[0], registryPath);

                    if (!File.Exists(candidate))
                        continue;

                    bestScore = score;
                    bestPath = candidate;
                }

                if (bestPath != null)
                    return Path.GetFullPath(bestPath);
            }
            catch (Exception)
            {
                // Registry access can fail in constrained environments; file-name fallbacks still apply.
            }
        }

        return null;
    }

    private static int ScoreMatch(string displayName, string family, (bool Bold, bool Italic) requested)
    {
        var hasBold = displayName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("Black", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("Heavy", StringComparison.OrdinalIgnoreCase);
        var hasItalic = displayName.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

        if (hasBold != requested.Bold || hasItalic != requested.Italic)
            return -1;

        if (string.Equals(displayName, family, StringComparison.OrdinalIgnoreCase))
            return 100;

        if (displayName.Equals(family + " Regular", StringComparison.OrdinalIgnoreCase))
            return 90;

        if (displayName.StartsWith(family + " ", StringComparison.OrdinalIgnoreCase))
            return 50;

        return -1;
    }

    private static IEnumerable<string> BuildFileCandidates(string family, string style)
    {
        var compact = new string(family.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
        var spaced = family.ToLowerInvariant();

        if (style is "" or "regular")
        {
            yield return spaced;
            yield return compact;
            yield return spaced.Replace(' ', '-');
            yield break;
        }

        if (style.Contains("bolditalic") || (style.Contains("bold") && style.Contains("italic")))
        {
            yield return compact + "bi";
            yield return spaced.Replace(' ', '-') + "-bold-italic";
            yield return compact + "-bold-italic";
            yield break;
        }

        if (style.Contains("bold"))
        {
            yield return compact + "bd";
            yield return compact + "b";
            yield return spaced.Replace(' ', '-') + "-bold";
            yield return compact + "-bold";
            yield break;
        }

        if (style.Contains("italic") || style.Contains("oblique"))
        {
            yield return compact + "i";
            yield return spaced.Replace(' ', '-') + "-italic";
            yield return compact + "-italic";
        }
    }

    private static (bool Bold, bool Italic) ParseStyle(string style)
    {
        var text = style.Replace(" ", string.Empty);
        return (
            text.Contains("Bold", StringComparison.OrdinalIgnoreCase),
            text.Contains("Italic", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Oblique", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeFontFile(string name)
    {
        var extension = System.IO.Path.GetExtension(name);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase);
    }
}
