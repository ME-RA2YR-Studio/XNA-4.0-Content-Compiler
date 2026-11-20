using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Microsoft.Xna.Framework.Graphics;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Building;

/// <summary>
/// Runs the bundled MonoGame importers, processors and XNB compiler directly instead
/// of going through <c>PipelineManager</c>. The manager resolves importers by loading
/// assemblies from disk, which is unavailable in single-file deployments, so building
/// content in-process keeps the tool fully self-contained.
/// </summary>
internal sealed class DirectContentPipeline
{
    private readonly string _projectDirectory;
    private readonly string _intermediateDirectory;
    private readonly ContentBuildLogger _logger;
    private readonly GraphicsProfile _profile;
    private readonly ContentCompiler _compiler = new();

    public DirectContentPipeline(
        string projectDirectory,
        string intermediateDirectory,
        ContentBuildLogger logger,
        GraphicsProfileSetting profile = GraphicsProfileSetting.Reach)
    {
        _projectDirectory = projectDirectory;
        _intermediateDirectory = intermediateDirectory;
        _logger = logger;
        _profile = profile == GraphicsProfileSetting.HiDef
            ? GraphicsProfile.HiDef
            : GraphicsProfile.Reach;
    }

    public void BuildTexture(string sourcePath, string outputPath, TextureSettings settings)
    {
        var importer = new TextureImporter();
        var imported = importer.Import(sourcePath, new ImportContext(_logger, _intermediateDirectory, Path.GetDirectoryName(outputPath)!));

        var processor = new TextureProcessor
        {
            TextureFormat = settings.OutputFormat switch
            {
                TextureOutputFormat.DxtCompressed => TextureProcessorOutputFormat.DxtCompressed,
                TextureOutputFormat.NoChange => TextureProcessorOutputFormat.NoChange,
                _ => TextureProcessorOutputFormat.Color,
            },
            GenerateMipmaps = settings.GenerateMipmaps,
            PremultiplyAlpha = settings.PremultiplyAlpha,
            ResizeToPowerOfTwo = settings.ResizeToPowerOfTwo,
            MakeSquare = settings.MakeSquare,
            ColorKeyEnabled = settings.ColorKeyEnabled,
        };

        if (settings.ColorKeyEnabled)
            processor.ColorKeyColor = ParseColorKey(settings.ColorKeyColor);

        var context = CreateProcessorContext(outputPath);
        var processed = processor.Process(imported, context);
        Compile(outputPath, processed);
    }

    public void BuildSpriteFont(string sourcePath, string outputPath, SpriteFontSettings settings)
    {
        var importer = new FontDescriptionImporter();
        var imported = importer.Import(sourcePath, new ImportContext(_logger, _intermediateDirectory, Path.GetDirectoryName(outputPath)!));

        var processor = new FontDescriptionProcessor
        {
            TextureFormat = settings.TextureFormat == SpriteFontTextureFormat.Compressed
                ? TextureProcessorOutputFormat.Compressed
                : TextureProcessorOutputFormat.Color,
            PremultiplyAlpha = settings.PremultiplyAlpha,
        };

        var context = CreateProcessorContext(outputPath);
        var processed = processor.Process(imported, context);
        Compile(outputPath, processed);
    }

    private void Compile(string outputPath, object content)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath)!;
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        _compiler.Compile(
            stream,
            content,
            TargetPlatform.Windows,
            _profile,
            compressContent: false,
            _projectDirectory,
            outputDirectory);
    }

    private ProcessorContext CreateProcessorContext(string outputPath)
    {
        return new ProcessorContext(
            _logger,
            _projectDirectory,
            _intermediateDirectory,
            Path.GetDirectoryName(outputPath)!,
            Path.GetFileName(outputPath),
            _profile);
    }

    private static Color ParseColorKey(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.StartsWith('#'))
            text = text[1..];

        if (text.Length == 8
            && int.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var argb))
        {
            return new Color((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF, (argb >> 24) & 0xFF);
        }

        if (text.Length == 6
            && int.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            return new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 255);
        }

        return new Color(255, 0, 255, 255);
    }

    private sealed class ImportContext : ContentImporterContext
    {
        public ImportContext(ContentBuildLogger logger, string intermediateDirectory, string outputDirectory)
        {
            Logger = logger;
            IntermediateDirectory = intermediateDirectory;
            OutputDirectory = outputDirectory;
        }

        public override string IntermediateDirectory { get; }

        public override ContentBuildLogger Logger { get; }

        public override string OutputDirectory { get; }

        public override void AddDependency(string filename)
        {
            // The compiler always imports from source, so incremental dependencies are unused.
        }
    }

    private sealed class ProcessorContext : ContentProcessorContext
    {
        public ProcessorContext(
            ContentBuildLogger logger,
            string projectDirectory,
            string intermediateDirectory,
            string outputDirectory,
            string outputFilename,
            GraphicsProfile profile)
        {
            Logger = logger;
            ProjectDirectory = projectDirectory;
            IntermediateDirectory = intermediateDirectory;
            OutputDirectory = outputDirectory;
            OutputFilename = outputFilename;
            TargetProfile = profile;
        }

        public override string BuildConfiguration => "Release";

        public override string IntermediateDirectory { get; }

        public override ContentBuildLogger Logger { get; }

        public override ContentIdentity SourceIdentity => new();

        public override string OutputDirectory { get; }

        public override string OutputFilename { get; }

        public override OpaqueDataDictionary Parameters { get; } = new();

        public override string ProjectDirectory { get; }

        public override TargetPlatform TargetPlatform => TargetPlatform.Windows;

        public override GraphicsProfile TargetProfile { get; }

        public override void AddDependency(string filename)
        {
        }

        public override void AddOutputFile(string filename)
        {
        }

        [Obsolete("Use the importer/processor overload.")]
        public override TOutput BuildAndLoadAsset<TInput, TOutput>(
            ExternalReference<TInput> sourceAsset,
            string processorName,
            OpaqueDataDictionary? processorParameters,
            string? importerName)
        {
            throw new NotSupportedException("Nested content builds are not supported by this tool.");
        }

        public override TOutput BuildAndLoadAsset<TInput, TOutput>(
            ExternalReference<TInput> sourceAsset,
            IContentImporter importer,
            IContentProcessor processor)
        {
            throw new NotSupportedException("Nested content builds are not supported by this tool.");
        }

        [Obsolete("Use the importer/processor overload.")]
        public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(
            ExternalReference<TInput> sourceAsset,
            string processorName,
            OpaqueDataDictionary? processorParameters,
            string? importerName,
            string? assetName)
        {
            throw new NotSupportedException("Nested content builds are not supported by this tool.");
        }

        public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(
            ExternalReference<TInput> sourceAsset,
            IContentImporter importer,
            IContentProcessor processor,
            string? assetName = null)
        {
            throw new NotSupportedException("Nested content builds are not supported by this tool.");
        }

        [Obsolete("Use the IContentProcessor overload.")]
        public override TOutput Convert<TInput, TOutput>(
            TInput input,
            string processorName,
            OpaqueDataDictionary processorParameters)
        {
            throw new NotSupportedException("Nested content conversions are not supported by this tool.");
        }

        public override TOutput Convert<TInput, TOutput>(TInput input, IContentProcessor processor)
        {
            return (TOutput)processor.Process(input!, this);
        }
    }
}
