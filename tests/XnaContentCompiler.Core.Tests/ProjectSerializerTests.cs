using System.Text.Json;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.Core.Tests;

public sealed class ProjectSerializerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsRelativePathsAndSettings()
    {
        var root = TestDirectory.Create();

        try
        {
            var assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(assets);
            File.WriteAllBytes(Path.Combine(assets, "pixel.png"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(assets, "beep.wav"), Array.Empty<byte>());

            var project = new ContentProject
            {
                OutputDirectory = Path.Combine(root, "out"),
            };

            var texture = project.CreateItem(Path.Combine(assets, "pixel.png"));
            texture.Texture.OutputFormat = TextureOutputFormat.DxtCompressed;
            texture.Texture.GenerateMipmaps = true;
            texture.Texture.ColorKeyEnabled = true;
            texture.Texture.ColorKeyColor = "#00FF00";
            project.AddItem(texture);

            var sound = project.CreateItem(Path.Combine(assets, "beep.wav"));
            project.AddItem(sound);

            var projectPath = Path.Combine(root, "demo.xnccproj");
            ProjectSerializer.Save(project, projectPath);

            var json = File.ReadAllText(projectPath);
            Assert.DoesNotContain(root, json);

            using (var document = JsonDocument.Parse(json))
            {
                Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());
                Assert.Equal("out", document.RootElement.GetProperty("outputDirectory").GetString());
            }

            var loaded = ProjectSerializer.Load(projectPath);

            Assert.Equal(projectPath, loaded.FilePath);
            Assert.Equal(Path.Combine(root, "out"), loaded.OutputDirectory);
            Assert.Equal(2, loaded.Items.Count);

            var loadedTexture = loaded.Items[0];
            Assert.Equal(Path.Combine(assets, "pixel.png"), loadedTexture.SourcePath);
            Assert.Equal(ContentKind.Texture, loadedTexture.Kind);
            Assert.Equal("pixel", loadedTexture.OutputName);
            Assert.Equal(TextureOutputFormat.DxtCompressed, loadedTexture.Texture.OutputFormat);
            Assert.True(loadedTexture.Texture.GenerateMipmaps);
            Assert.True(loadedTexture.Texture.ColorKeyEnabled);
            Assert.Equal("#00FF00", loadedTexture.Texture.ColorKeyColor);

            Assert.Equal(ContentKind.SoundEffect, loaded.Items[1].Kind);
            Assert.Equal(GraphicsProfileSetting.Reach, loaded.Profile);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheGraphicsProfile()
    {
        var root = TestDirectory.Create();
        try
        {
            var project = new ContentProject
            {
                OutputDirectory = Path.Combine(root, "out"),
                Profile = GraphicsProfileSetting.HiDef,
            };

            var projectPath = Path.Combine(root, "demo.xnccproj");
            ProjectSerializer.Save(project, projectPath);

            var json = File.ReadAllText(projectPath);
            Assert.Contains("HiDef", json);

            Assert.Equal(GraphicsProfileSetting.HiDef, ProjectSerializer.Load(projectPath).Profile);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }

    [Fact]
    public void Load_RepairsInvalidOutputNames()
    {
        var root = TestDirectory.Create();
        try
        {
            var projectPath = Path.Combine(root, "broken.xnccproj");
            File.WriteAllText(projectPath, """
{
  "formatVersion": 1,
  "outputDirectory": "out",
  "items": [
    { "sourcePath": "bad.png", "outputName": "..\\evil", "kind": "Texture" }
  ]
}
""");

            var project = ProjectSerializer.Load(projectPath);

            Assert.Single(project.Items);
            Assert.Equal("bad", project.Items[0].OutputName);
        }
        finally
        {
            TestDirectory.Delete(root);
        }
    }
}
