using System.Drawing;
using System.Drawing.Text;
using XnaContentCompiler.App.Localization;

namespace XnaContentCompiler.App.UI;

/// <summary>
/// Provides the UI fonts. Fonts are cached for the lifetime of the process so switching
/// languages or opening dialogs repeatedly does not accumulate GDI font handles.
/// </summary>
public static class UiFonts
{
    private const string EnglishFamily = "Segoe UI";
    private const string ChineseFamily = "Microsoft YaHei UI";
    private const string ChineseFallbackFamily = "Microsoft YaHei";

    private static readonly object Gate = new();
    private static Font? _englishFont;
    private static Font? _chineseFont;
    private static Font? _englishBoldFont;
    private static Font? _chineseBoldFont;
    private static Font? _monoFont;
    private static bool? _hasSegoeUi;
    private static bool? _hasYaHeiUi;
    private static bool? _hasYaHei;
    private static bool? _hasConsolas;

    public static Font GetInterfaceFont(AppLanguage language)
    {
        lock (Gate)
        {
            if (language == AppLanguage.ChineseSimplified)
            {
                return _chineseFont ??= new Font(
                    HasFamily(ChineseFamily, ref _hasYaHeiUi) ? ChineseFamily
                        : HasFamily(ChineseFallbackFamily, ref _hasYaHei) ? ChineseFallbackFamily
                        : EnglishFamily,
                    9F, FontStyle.Regular, GraphicsUnit.Point);
            }

            return _englishFont ??= new Font(
                HasFamily(EnglishFamily, ref _hasSegoeUi) ? EnglishFamily : "Tahoma",
                9F, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    public static Font GetBoldInterfaceFont(AppLanguage language)
    {
        lock (Gate)
        {
            if (language == AppLanguage.ChineseSimplified)
                return _chineseBoldFont ??= new Font(GetInterfaceFont(language), FontStyle.Bold);

            return _englishBoldFont ??= new Font(GetInterfaceFont(language), FontStyle.Bold);
        }
    }

    public static Font GetMonoFont()
    {
        lock (Gate)
        {
            return _monoFont ??= new Font(
                HasFamily("Consolas", ref _hasConsolas) ? "Consolas" : "Courier New",
                9.75F, FontStyle.Regular, GraphicsUnit.Point);
        }
    }

    private static bool HasFamily(string familyName, ref bool? cache)
    {
        if (cache.HasValue)
            return cache.Value;

        using var collection = new InstalledFontCollection();
        cache = collection.Families.Any(
            f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));
        return cache.Value;
    }
}
