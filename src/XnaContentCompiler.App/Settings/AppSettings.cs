using System.IO;
using System.Text.Json;
using XnaContentCompiler.Core.Utilities;

namespace XnaContentCompiler.App.Settings;

public sealed class AppSettings
{
    public string Language { get; set; } = "auto";

    public List<string> RecentProjects { get; set; } = new();

    /// <summary>Output directory used the last time a project was created or built.</summary>
    public string? LastOutputDirectory { get; set; }

    public WindowSettings Window { get; set; } = new();

    public void AddRecentProject(string path)
    {
        var fullPath = Path.GetFullPath(path);
        RecentProjects.RemoveAll(x => string.Equals(x, fullPath, StringComparison.OrdinalIgnoreCase));
        RecentProjects.Insert(0, fullPath);

        while (RecentProjects.Count > 8)
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
    }
}

public sealed class WindowSettings
{
    public int X { get; set; } = 80;
    public int Y { get; set; } = 60;
    public int Width { get; set; } = 1180;
    public int Height { get; set; } = 780;
    public bool Maximized { get; set; }
}

/// <summary>
/// Persists application preferences under %APPDATA%\XnaContentCompiler.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XnaContentCompiler", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Invalid or unreadable settings should never prevent the application from starting.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        AtomicFile.WriteAllText(SettingsPath, json);
    }
}
