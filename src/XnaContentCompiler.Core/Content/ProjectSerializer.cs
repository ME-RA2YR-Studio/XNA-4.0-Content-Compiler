using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.Core.Content;

/// <summary>
/// Reads and writes .xnccproj project files. Paths are stored relative to the project
/// file whenever possible so a project folder can be moved or copied as a whole.
/// </summary>
public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static void Save(ContentProject project, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var projectDirectory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(projectDirectory);

        var document = new ProjectDocument
        {
            FormatVersion = ContentProject.CurrentFormatVersion,
            OutputDirectory = PathUtilities.MakeRelativeIfPossible(
                Path.GetFullPath(project.OutputDirectory), projectDirectory),
            GraphicsProfile = project.Profile,
            Items = project.Items.Select(item => new ItemDocument
            {
                SourcePath = PathUtilities.MakeRelativeIfPossible(
                    Path.GetFullPath(item.SourcePath), projectDirectory),
                OutputName = OutputNameValidator.Normalize(item.OutputName),
                Kind = item.Kind,
                Texture = item.Texture,
                SpriteFont = item.SpriteFont,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(document, Options);
        AtomicFile.WriteAllText(fullPath, json);
        project.FilePath = fullPath;
    }

    public static ContentProject Load(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var projectDirectory = Path.GetDirectoryName(fullPath)!;
        var json = File.ReadAllText(fullPath);
        var document = JsonSerializer.Deserialize<ProjectDocument>(json, Options)
            ?? throw new InvalidDataException("The project file is empty or invalid.");

        if (document.FormatVersion > ContentProject.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported project format version {document.FormatVersion}.");
        }

        var project = new ContentProject
        {
            OutputDirectory = PathUtilities.ResolveRelativeTo(
                string.IsNullOrWhiteSpace(document.OutputDirectory)
                    ? "Content"
                    : document.OutputDirectory,
                projectDirectory),
            FilePath = fullPath,
            Profile = document.GraphicsProfile,
        };

        foreach (var itemDocument in document.Items ?? new List<ItemDocument>())
        {
            var sourcePath = PathUtilities.ResolveRelativeTo(itemDocument.SourcePath, projectDirectory);
            var detectedKind = ContentTypeRegistry.Detect(sourcePath);

            string outputName;
            if (!OutputNameValidator.TryNormalize(itemDocument.OutputName, out outputName))
                outputName = ContentTypeRegistry.DefaultOutputName(sourcePath);

            project.Items.Add(new ContentItem
            {
                SourcePath = sourcePath,
                OutputName = outputName,
                Kind = detectedKind == ContentKind.Unsupported ? itemDocument.Kind : detectedKind,
                Texture = itemDocument.Texture ?? new TextureSettings(),
                SpriteFont = itemDocument.SpriteFont ?? new SpriteFontSettings(),
            });
        }

        return project;
    }

    private sealed class ProjectDocument
    {
        public int FormatVersion { get; set; } = ContentProject.CurrentFormatVersion;
        public string? OutputDirectory { get; set; }
        public GraphicsProfileSetting GraphicsProfile { get; set; } = GraphicsProfileSetting.Reach;
        public List<ItemDocument>? Items { get; set; }
    }

    private sealed class ItemDocument
    {
        public string SourcePath { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;
        public ContentKind Kind { get; set; } = ContentKind.Unsupported;
        public TextureSettings? Texture { get; set; }
        public SpriteFontSettings? SpriteFont { get; set; }
    }
}
