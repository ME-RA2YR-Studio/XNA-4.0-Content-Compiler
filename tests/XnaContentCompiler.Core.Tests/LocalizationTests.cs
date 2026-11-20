using XnaContentCompiler.App.Localization;

namespace XnaContentCompiler.Core.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void LanguageTablesHaveIdenticalKeys()
    {
        var localizer = LocalizationService.Instance;

        try
        {
            localizer.Initialize(AppLanguage.English);
            var english = localizer.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.NotEmpty(english);
            Assert.Equal(english, localizer.FallbackKeys.OrderBy(x => x, StringComparer.Ordinal));

            localizer.SetLanguage(AppLanguage.ChineseSimplified);
            var chinese = localizer.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.Equal(english, chinese);

            Assert.NotEqual("app.title", localizer["app.title"]);
            Assert.Contains("XNA", localizer["app.title"]);
        }
        finally
        {
            localizer.Initialize(AppLanguage.English);
        }
    }

    [Fact]
    public void FormattingUsesCurrentLanguage()
    {
        var localizer = LocalizationService.Instance;
        try
        {
            localizer.Initialize(AppLanguage.ChineseSimplified);

            Assert.Equal("已添加 3 个项目。", localizer.Format("log.added", 3));
        }
        finally
        {
            localizer.Initialize(AppLanguage.English);
        }
    }
}
