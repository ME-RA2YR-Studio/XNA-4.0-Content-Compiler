using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Building;

public enum BuildLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Success,
}

public enum BuildErrorCode
{
    None,
    SourceNotFound,
    UnsupportedContentType,
    InvalidOutputName,
    DuplicateOutputName,
    InvalidOutputDirectory,
    TextureBuildFailed,
    FontBuildFailed,
    AudioBuildFailed,
    SongBuildFailed,
    XnaCompatibilityFailed,
    IoError,
    Canceled,
    Unknown,
}

public sealed class BuildLogEntry
{
    public BuildLogEntry(BuildLogLevel level, string message, ContentItem? item = null)
    {
        Timestamp = DateTime.Now;
        Level = level;
        Message = message;
        Item = item;
    }

    public DateTime Timestamp { get; }
    public BuildLogLevel Level { get; }
    public string Message { get; }
    public ContentItem? Item { get; }
}

public sealed class ItemBuildResult
{
    public required ContentItem Item { get; init; }
    public bool Success { get; init; }
    public BuildErrorCode ErrorCode { get; init; } = BuildErrorCode.None;
    public string? ErrorDetail { get; init; }
    public string? OutputPath { get; init; }
    public long OutputBytes { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class BuildProgress
{
    public BuildProgress(int current, int total, ContentItem item)
    {
        Current = current;
        Total = total;
        Item = item;
    }

    public int Current { get; }
    public int Total { get; }
    public ContentItem Item { get; }
}

public sealed class BuildSummary
{
    public required List<ItemBuildResult> Items { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
    public required bool Canceled { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string OutputDirectory { get; init; }
}

public class BuildConfigurationException : Exception
{
    public BuildConfigurationException(BuildErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public BuildErrorCode Code { get; }
}
