using System.IO;
using System.Text.Json.Serialization;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.Core.Content;

/// <summary>
/// An in-memory content project. Instances are created by the UI or loaded from a
/// .xnccproj file and are passed to <see cref="Building.ContentBuildService"/> for compilation.
/// </summary>
public sealed class ContentProject
{
    public const int CurrentFormatVersion = 1;

    /// <summary>Absolute path of the output directory.</summary>
    public string OutputDirectory { get; set; } = Path.Combine(Environment.CurrentDirectory, "Content");

    /// <summary>Graphics profile written into every XNB header.</summary>
    public GraphicsProfileSetting Profile { get; set; } = GraphicsProfileSetting.Reach;

    public List<ContentItem> Items { get; set; } = new();

    /// <summary>Absolute path of the project file, or null for a project that was never saved.</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }

    [JsonIgnore]
    public string ProjectDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FilePath))
                return Path.GetDirectoryName(Path.GetFullPath(FilePath))!;

            return Environment.CurrentDirectory;
        }
    }

    [JsonIgnore]
    public string Name => string.IsNullOrWhiteSpace(FilePath)
        ? string.Empty
        : Path.GetFileNameWithoutExtension(FilePath);

    public string GetOutputPath(ContentItem item)
    {
        var normalized = OutputNameValidator.Normalize(item.OutputName);
        return Path.Combine(OutputDirectory, normalized + ".xnb");
    }

    public void AddItem(ContentItem item)
    {
        Items.Add(item);
    }

    public void RemoveItem(ContentItem item)
    {
        Items.Remove(item);
    }

    public ContentItem? FindById(Guid id)
    {
        return Items.FirstOrDefault(x => x.Id == id);
    }

    public bool HasDuplicateOutputNames()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Items)
        {
            var name = OutputNameValidator.Normalize(item.OutputName);
            if (!seen.Add(name))
                return true;
        }
        return false;
    }

    public IEnumerable<string> FindDuplicateOutputNames()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        foreach (var item in Items)
        {
            var name = OutputNameValidator.Normalize(item.OutputName);
            if (!seen.Add(name) && !duplicates.Contains(name, StringComparer.OrdinalIgnoreCase))
                duplicates.Add(name);
        }
        return duplicates;
    }

    /// <summary>
    /// Creates a new item and ensures its output name does not collide with an existing item.
    /// </summary>
    public ContentItem CreateItem(string sourcePath)
    {
        var item = new ContentItem(PathUtilities.NormalizeFullPath(sourcePath));
        item.OutputName = GetUniqueOutputName(item.OutputName);
        return item;
    }

    /// <summary>
    /// Adds several source files at once, resolving output name collisions in a single
    /// pass. Adding files individually is O(n^2) and becomes noticeable for large folders.
    /// </summary>
    public List<ContentItem> CreateItems(IEnumerable<string> sourcePaths)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in Items)
        {
            if (OutputNameValidator.TryNormalize(existing.OutputName, out var normalized))
                usedNames.Add(normalized);
        }

        var created = new List<ContentItem>();
        foreach (var sourcePath in sourcePaths)
        {
            var item = new ContentItem(PathUtilities.NormalizeFullPath(sourcePath));
            var baseName = OutputNameValidator.Normalize(item.OutputName);
            var candidate = baseName;

            for (var suffix = 2; usedNames.Contains(candidate); suffix++)
                candidate = $"{baseName}_{suffix}";

            item.OutputName = candidate;
            usedNames.Add(candidate);

            Items.Add(item);
            created.Add(item);
        }

        return created;
    }

    public string GetUniqueOutputName(string desiredName)
    {
        var name = OutputNameValidator.Normalize(desiredName);
        var existing = new HashSet<string>(
            Items.Select(x => OutputNameValidator.Normalize(x.OutputName)),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(name))
            return name;

        for (var index = 2; index < 10000; index++)
        {
            var candidate = $"{name}_{index}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate a unique output name.");
    }

    /// <summary>
    /// Applies a user-entered output name after validating its format and uniqueness.
    /// </summary>
    public bool TryRenameItem(ContentItem item, string? outputName, out OutputNameError error)
    {
        error = OutputNameError.None;

        if (!Items.Contains(item))
        {
            error = OutputNameError.UnknownItem;
            return false;
        }

        if (!OutputNameValidator.TryNormalize(outputName, out var normalized))
        {
            error = OutputNameError.Invalid;
            return false;
        }

        foreach (var other in Items)
        {
            if (ReferenceEquals(other, item))
                continue;

            if (!OutputNameValidator.TryNormalize(other.OutputName, out var existing))
                continue;

            if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
            {
                error = OutputNameError.Duplicate;
                return false;
            }
        }

        item.OutputName = normalized;
        return true;
    }
}

/// <summary>
/// Validates and normalizes asset output names (relative paths without the .xnb extension).
/// </summary>
public static class OutputNameValidator
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static string Normalize(string? outputName)
    {
        if (!TryNormalize(outputName, out var normalized))
            throw new ArgumentException("Invalid output name.", nameof(outputName));

        return normalized;
    }

    public static bool TryNormalize(string? outputName, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(outputName))
            return false;

        var value = outputName.Trim().Replace('/', '\\');

        if (value.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];

        value = value.Trim().TrimEnd('\\');
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (Path.IsPathRooted(value) || value.Contains(':'))
            return false;

        var segments = value.Split('\\');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                return false;

            if (segment.IndexOfAny(InvalidChars) >= 0)
                return false;

            if (segment.EndsWith(' ') || segment.EndsWith('.'))
                return false;
        }

        normalized = string.Join("\\", segments);
        return true;
    }
}

public enum OutputNameError
{
    None,
    Invalid,
    Duplicate,
    UnknownItem,
}
