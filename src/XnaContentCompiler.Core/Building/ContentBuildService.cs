using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Content.Pipeline;
using XnaContentCompiler.Core.Content;
using XnaContentCompiler.Core.Content.Audio;
using XnaContentCompiler.Core.Content.Fonts;
using XnaContentCompiler.Core.Content.Xnb;
using ContentItem = XnaContentCompiler.Core.Content.ContentItem;

namespace XnaContentCompiler.Core.Building;

/// <summary>
/// Compiles a <see cref="ContentProject"/> into XNA 4.0 XNB files.
/// Textures and sprite fonts go through the bundled MonoGame content pipeline;
/// audio is converted directly and needs no external codec tools.
/// </summary>
public sealed class ContentBuildService
{
    public event EventHandler<BuildLogEntry>? Log;

    public event EventHandler<ContentItem>? ItemStarting;

    public event EventHandler<ItemBuildResult>? ItemFinished;

    public event EventHandler<BuildProgress>? Progress;

    private ContentItem? _currentItem;

    /// <summary>
    /// Compiles every item in the project.
    /// </summary>
    public BuildSummary Build(ContentProject project, CancellationToken cancellationToken = default)
    {
        return Build(project, null, cancellationToken);
    }

    /// <summary>
    /// Compiles a subset of the project. Items that are not part of
    /// <paramref name="items"/> are neither validated nor written.
    /// </summary>
    public BuildSummary Build(
        ContentProject project,
        IReadOnlyCollection<ContentItem>? items,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();
        var configuredOutputDirectory = project.OutputDirectory?.Trim();

        if (string.IsNullOrWhiteSpace(configuredOutputDirectory))
        {
            throw new BuildConfigurationException(
                BuildErrorCode.InvalidOutputDirectory,
                "The output directory is not set.");
        }

        string outputDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(configuredOutputDirectory);
        }
        catch (Exception ex)
        {
            throw new BuildConfigurationException(
                BuildErrorCode.InvalidOutputDirectory,
                $"The output directory '{configuredOutputDirectory}' is not a valid path: {ex.Message}",
                ex);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            throw new BuildConfigurationException(
                BuildErrorCode.InvalidOutputDirectory,
                $"Could not create the output directory '{outputDirectory}': {ex.Message}",
                ex);
        }

        var probePath = Path.Combine(
            outputDirectory, $".xncc-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, string.Empty);
        }
        catch (Exception ex)
        {
            throw new BuildConfigurationException(
                BuildErrorCode.InvalidOutputDirectory,
                $"The output directory '{outputDirectory}' is not writable: {ex.Message}",
                ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                    File.Delete(probePath);
            }
            catch (Exception)
            {
                // A leftover probe file is harmless.
            }
        }

        var selected = items == null
            ? project.Items.ToList()
            : project.Items.Where(items.Contains).ToList();

        var normalizedNames = new Dictionary<ContentItem, string>();
        var invalidNames = new Dictionary<ContentItem, string>();
        foreach (var item in selected)
        {
            if (OutputNameValidator.TryNormalize(item.OutputName, out var normalized))
                normalizedNames[item] = normalized;
            else
                invalidNames[item] = item.OutputName;
        }

        var duplicates = normalizedNames
            .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sessionRoot = Path.Combine(
            Path.GetTempPath(), "XnaContentCompiler", Guid.NewGuid().ToString("N"));
        var sessionAssets = Path.Combine(sessionRoot, "assets");
        var sessionOutput = Path.Combine(sessionRoot, "output");
        var sessionCache = Path.Combine(sessionRoot, "cache");

        var results = new List<ItemBuildResult>(selected.Count);
        var canceled = false;
        var pipeline = new DirectContentPipeline(
            sessionAssets, sessionCache, new PipelineLogAdapter(EmitLog), project.Profile);

        try
        {
            Directory.CreateDirectory(sessionAssets);
            Directory.CreateDirectory(sessionOutput);
            Directory.CreateDirectory(sessionCache);

            var total = selected.Count;
            for (var index = 0; index < total; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                    break;
                }

                var item = selected[index];
                _currentItem = item;
                Progress?.Invoke(this, new BuildProgress(index + 1, total, item));
                ItemStarting?.Invoke(this, item);

                var itemStarted = Stopwatch.StartNew();
                ItemBuildResult result;

                try
                {
                    result = BuildItem(
                        item,
                        normalizedNames,
                        invalidNames,
                        duplicates,
                        pipeline,
                        sessionAssets,
                        sessionOutput,
                        outputDirectory,
                        project.Profile,
                        () => canceled);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                    break;
                }
                catch (Exception ex)
                {
                    result = Failure(item, BuildErrorCode.Unknown, ex.Message, itemStarted.Elapsed);
                    EmitLog(BuildLogLevel.Error, $"{item.FileName}: {ex.Message}");
                }

                results.Add(result);
                ItemFinished?.Invoke(this, result);
            }
        }
        finally
        {
            _currentItem = null;
            TryDeleteDirectory(sessionRoot);
        }

        var succeeded = results.Count(x => x.Success);
        var failed = results.Count(x => !x.Success);

        return new BuildSummary
        {
            Items = results,
            Succeeded = succeeded,
            Failed = failed,
            Canceled = canceled,
            Duration = started.Elapsed,
            OutputDirectory = outputDirectory,
        };
    }

    private ItemBuildResult BuildItem(
        ContentItem item,
        IReadOnlyDictionary<ContentItem, string> normalizedNames,
        IReadOnlyDictionary<ContentItem, string> invalidNames,
        IReadOnlySet<string> duplicates,
        DirectContentPipeline pipeline,
        string sessionAssets,
        string sessionOutput,
        string outputDirectory,
        GraphicsProfileSetting profile,
        Func<bool> isCanceled)
    {
        var started = Stopwatch.StartNew();

        if (invalidNames.TryGetValue(item, out var invalidName))
        {
            return Fail(BuildErrorCode.InvalidOutputName,
                $"Output name '{invalidName}' is not a valid relative path.");
        }

        var outputName = normalizedNames[item];
        if (duplicates.Contains(outputName))
        {
            return Fail(BuildErrorCode.DuplicateOutputName,
                $"Output name '{outputName}' is used by more than one item.");
        }

        if (!File.Exists(item.SourcePath))
            return Fail(BuildErrorCode.SourceNotFound, $"Source file '{item.SourcePath}' was not found.");

        if (item.Kind == ContentKind.Unsupported)
        {
            return Fail(BuildErrorCode.UnsupportedContentType,
                $"The file type '{Path.GetExtension(item.SourcePath)}' is not supported.");
        }

        if (isCanceled())
            throw new OperationCanceledException();

        var relativeOutput = outputName + ".xnb";
        var stagedOutput = Path.Combine(sessionOutput, relativeOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedOutput)!);

        string? stagedMedia = null;

        try
        {
            switch (item.Kind)
            {
                case ContentKind.Texture:
                    pipeline.BuildTexture(item.SourcePath, stagedOutput, item.Texture);
                    break;

                case ContentKind.SpriteFont:
                    BuildSpriteFont(item, sessionAssets, pipeline, stagedOutput);
                    break;

                case ContentKind.SoundEffect:
                    BuildSoundEffect(item, stagedOutput, profile);
                    break;

                case ContentKind.Song:
                    stagedMedia = BuildSong(item, stagedOutput, outputName, profile);
                    break;

                default:
                    return Fail(BuildErrorCode.UnsupportedContentType,
                        $"The content kind '{item.Kind}' is not supported.");
            }

            if (!File.Exists(stagedOutput))
            {
                return Fail(BuildErrorCode.Unknown,
                    "The content pipeline finished without producing an output file.");
            }

            if (item.Kind is ContentKind.Texture or ContentKind.SpriteFont)
            {
                var patched = stagedOutput + ".xna.xnb";
                var rewrite = XnbManifestRewriter.RewriteToXna(stagedOutput, patched);
                foreach (var warning in rewrite.Warnings)
                    EmitLog(BuildLogLevel.Warning, warning);

                stagedOutput = patched;
            }

            var finalOutput = Path.Combine(outputDirectory, relativeOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(finalOutput)!);

            try
            {
                WriteOutputAtomically(stagedOutput, finalOutput);

                if (stagedMedia != null)
                {
                    var mediaName = Path.GetFileName(stagedMedia);
                    var finalMedia = Path.Combine(Path.GetDirectoryName(finalOutput)!, mediaName);
                    WriteOutputAtomically(stagedMedia, finalMedia);

                    EmitLog(BuildLogLevel.Info,
                        $"Copied media {mediaName} ({FormatSize(new FileInfo(finalMedia).Length)})");
                }
            }
            catch (Exception ex)
            {
                return Fail(BuildErrorCode.IoError,
                    $"Could not write '{finalOutput}': {ex.Message}");
            }

            if (item.Kind == ContentKind.Texture
                && XnbTextureInspector.TryReadSize(finalOutput, out var width, out var height))
            {
                var maxDimension = profile == GraphicsProfileSetting.HiDef
                    ? XnbTextureInspector.HiDefMaxDimension
                    : XnbTextureInspector.ReachMaxDimension;

                if (width > maxDimension || height > maxDimension)
                {
                    EmitLog(BuildLogLevel.Warning,
                        $"{relativeOutput.Replace('\\', '/')}: {width}x{height} exceeds the XNA {profile} " +
                        $"limit of {maxDimension} pixels; use a smaller texture.");
                }
            }

            var length = new FileInfo(finalOutput).Length;
            EmitLog(BuildLogLevel.Success,
                $"Built {relativeOutput.Replace('\\', '/')} ({FormatSize(length)})");

            return new ItemBuildResult
            {
                Item = item,
                Success = true,
                OutputPath = finalOutput,
                OutputBytes = length,
                Duration = started.Elapsed,
            };
        }
        catch (PipelineException ex)
        {
            return FailWithDetail(ErrorCodeFor(item.Kind), ex.Message, ex, started.Elapsed);
        }
        catch (InvalidContentException ex)
        {
            return FailWithDetail(ErrorCodeFor(item.Kind), ex.Message, ex, started.Elapsed);
        }
        catch (InvalidDataException ex)
        {
            return FailWithDetail(ErrorCodeFor(item.Kind), ex.Message, ex, started.Elapsed);
        }
        catch (XnbCompatibilityException ex)
        {
            return FailWithDetail(BuildErrorCode.XnaCompatibilityFailed, ex.Message, ex, started.Elapsed);
        }
        catch (NotSupportedException ex)
        {
            return FailWithDetail(ErrorCodeFor(item.Kind), ex.Message, ex, started.Elapsed);
        }
        catch (System.Xml.XmlException ex)
        {
            return FailWithDetail(ErrorCodeFor(item.Kind), ex.Message, ex, started.Elapsed);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailWithDetail(BuildErrorCode.IoError, ex.Message, ex, started.Elapsed);
        }
        catch (IOException ex)
        {
            return FailWithDetail(BuildErrorCode.IoError, ex.Message, ex, started.Elapsed);
        }

        ItemBuildResult Fail(BuildErrorCode code, string detail)
            => FailWithDetail(code, detail, null, started.Elapsed);

        ItemBuildResult FailWithDetail(BuildErrorCode code, string detail, Exception? exception, TimeSpan elapsed)
        {
            var message = exception is null ? detail : $"{detail} ({exception.GetType().Name})";
            EmitLog(BuildLogLevel.Error, $"{item.FileName}: {message}");
            return Failure(item, code, detail, elapsed);
        }
    }

    private void BuildSpriteFont(
        ContentItem item,
        string sessionAssets,
        DirectContentPipeline pipeline,
        string stagedOutput)
    {
        var prepared = SpriteFontPreprocessor.Prepare(item.SourcePath, sessionAssets);
        if (prepared.Warning != null)
            EmitLog(BuildLogLevel.Warning, prepared.Warning);
        else if (prepared.ResolvedFontPath != null)
            EmitLog(BuildLogLevel.Info, $"Using font file: {prepared.ResolvedFontPath}");

        pipeline.BuildSpriteFont(prepared.SourcePath, stagedOutput, item.SpriteFont);
    }

    private void BuildSoundEffect(
        ContentItem item,
        string outputPath,
        GraphicsProfileSetting profile)
    {
        var sound = WavSoundReader.Read(item.SourcePath);
        SoundEffectXnbWriter.Write(sound, outputPath, profile);

        EmitLog(BuildLogLevel.Info,
            $"{item.FileName}: {sound.Channels} channel(s), {sound.SampleRate} Hz, " +
            $"{sound.DurationMilliseconds} ms" +
            (sound.LoopLength > 0 ? ", loop points preserved" : string.Empty));
    }

    /// <summary>
    /// Builds a Song XNB and stages a copy of the media file next to it. XNA Song content
    /// references the media by relative name instead of embedding it.
    /// </summary>
    private string BuildSong(
        ContentItem item,
        string stagedOutput,
        string outputName,
        GraphicsProfileSetting profile)
    {
        var duration = SongInfoReader.GetDurationMilliseconds(item.SourcePath);
        if (duration <= 0)
        {
            throw new InvalidDataException(
                $"{item.FileName}: could not read the audio duration; the file may be corrupt or unsupported.");
        }

        var extension = Path.GetExtension(item.SourcePath);
        var mediaName = Path.GetFileName(outputName) + extension;
        var mediaPath = Path.Combine(Path.GetDirectoryName(stagedOutput)!, mediaName);

        MusicXnbWriter.Write(stagedOutput, mediaName, duration, profile);
        File.Copy(item.SourcePath, mediaPath, overwrite: true);

        EmitLog(BuildLogLevel.Info,
            $"{item.FileName}: {duration} ms, media referenced as {mediaName}");

        return mediaPath;
    }

    private static BuildErrorCode ErrorCodeFor(ContentKind kind)
    {
        return kind switch
        {
            ContentKind.Texture => BuildErrorCode.TextureBuildFailed,
            ContentKind.SpriteFont => BuildErrorCode.FontBuildFailed,
            ContentKind.SoundEffect => BuildErrorCode.AudioBuildFailed,
            ContentKind.Song => BuildErrorCode.SongBuildFailed,
            _ => BuildErrorCode.Unknown,
        };
    }

    private static ItemBuildResult Failure(ContentItem item, BuildErrorCode code, string detail, TimeSpan duration)
    {
        return new ItemBuildResult
        {
            Item = item,
            Success = false,
            ErrorCode = code,
            ErrorDetail = detail,
            Duration = duration,
        };
    }

    private void EmitLog(BuildLogLevel level, string message)
    {
        Log?.Invoke(this, new BuildLogEntry(level, message, _currentItem));
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }

    /// <summary>
    /// Copies a staged file next to its destination and swaps it in, so an interrupted
    /// build can never leave a half-written asset behind.
    /// </summary>
    private static void WriteOutputAtomically(string sourcePath, string destinationPath)
    {
        var temporary = destinationPath + ".tmp";
        try
        {
            File.Copy(sourcePath, temporary, overwrite: true);

            if (File.Exists(destinationPath))
                File.Replace(temporary, destinationPath, null, ignoreMetadataErrors: true);
            else
                File.Move(temporary, destinationPath);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (IOException)
            {
                // A leftover temporary file is removed on the next successful build.
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // Temporary directories are also cleaned by the operating system.
        }
    }

    private sealed class PipelineLogAdapter : ContentBuildLogger
    {
        private readonly Action<BuildLogLevel, string> _sink;

        public PipelineLogAdapter(Action<BuildLogLevel, string> sink)
        {
            _sink = sink;
        }

        public override void Log(LogLevel level, string message)
        {
            var mapped = level switch
            {
                LogLevel.Warning => BuildLogLevel.Warning,
                LogLevel.Error => BuildLogLevel.Error,
                LogLevel.Debug => BuildLogLevel.Debug,
                _ => BuildLogLevel.Info,
            };

            // The font processor reports the staged copy of the font file, which is not
            // meaningful to users because the resolved font path is already logged.
            if (message.StartsWith("Building Font ", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrWhiteSpace(message))
                _sink(mapped, message);
        }
    }
}
