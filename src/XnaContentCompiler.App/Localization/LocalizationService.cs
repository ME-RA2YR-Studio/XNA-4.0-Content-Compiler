using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace XnaContentCompiler.App.Localization;

public enum AppLanguage
{
    English,
    ChineseSimplified,
}

/// <summary>
/// Loads the embedded JSON language tables and provides runtime language switching.
/// </summary>
public sealed class LocalizationService
{
    private static readonly Lazy<LocalizationService> LazyInstance =
        new(() => new LocalizationService());

    private readonly Dictionary<string, string> _fallback = new(StringComparer.Ordinal);
    private Dictionary<string, string> _current = new(StringComparer.Ordinal);

    private LocalizationService()
    {
    }

    public static LocalizationService Instance => LazyInstance.Value;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    public event EventHandler? LanguageChanged;

    /// <summary>Keys in the active language table. Used by diagnostics and tests.</summary>
    public IReadOnlyCollection<string> Keys => _current.Keys;

    /// <summary>Keys in the English fallback table. Used by diagnostics and tests.</summary>
    public IReadOnlyCollection<string> FallbackKeys => _fallback.Keys;

    public void Initialize(AppLanguage language)
    {
        Merge(_fallback, LoadTable(AppLanguage.English));
        Load(language);
    }

    public void SetLanguage(AppLanguage language)
    {
        if (language == Language && _current.Count > 0)
            return;

        Load(language);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_current.TryGetValue(key, out var value))
            return value;

        if (_fallback.TryGetValue(key, out value))
            return value;

        return key;
    }

    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentCulture, template, args);
    }

    public static AppLanguage DetectSystemLanguage()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.ChineseSimplified
            : AppLanguage.English;
    }

    public static AppLanguage ParseSetting(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DetectSystemLanguage();

        return value.Trim().ToLowerInvariant() switch
        {
            "zh" or "zh-cn" or "zh-hans" or "chinese" => AppLanguage.ChineseSimplified,
            "en" or "en-us" or "english" => AppLanguage.English,
            _ => DetectSystemLanguage(),
        };
    }

    public static string ToSettingValue(AppLanguage language)
    {
        return language == AppLanguage.ChineseSimplified ? "zh-Hans" : "en";
    }

    private void Load(AppLanguage language)
    {
        Language = language;
        _current = LoadTable(language);
    }

    private static Dictionary<string, string> LoadTable(AppLanguage language)
    {
        var fileName = language == AppLanguage.ChineseSimplified ? "zh-Hans.json" : "en.json";
        var resourceName = "lang/" + fileName;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Missing embedded language resource '{resourceName}'.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var table = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
            ?? throw new InvalidOperationException($"The language resource '{resourceName}' is invalid.");

        return new Dictionary<string, string>(table, StringComparer.Ordinal);
    }

    private static void Merge(Dictionary<string, string> destination, Dictionary<string, string> source)
    {
        destination.Clear();
        foreach (var pair in source)
            destination[pair.Key] = pair.Value;
    }
}
