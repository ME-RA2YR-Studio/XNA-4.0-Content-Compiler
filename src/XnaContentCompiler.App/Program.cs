using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using XnaContentCompiler.App.Localization;
using XnaContentCompiler.App.Settings;
using XnaContentCompiler.App.UI;
using XnaContentCompiler.Core.Building;
using XnaContentCompiler.Core.Content;

namespace XnaContentCompiler.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog(e.ExceptionObject as Exception);

        var options = CliOptions.Parse(args, out var parseError);
        var settings = SettingsStore.Load();

        var language = options.Language
            ?? LocalizationService.ParseSetting(settings.Language);
        LocalizationService.Instance.Initialize(language);
        var loc = LocalizationService.Instance;

        if (parseError != null)
        {
            ConsoleHost.Attach();
            Console.Error.WriteLine(loc.Format("cli.error", parseError));
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 2;
        }

        if (options.Help)
        {
            ConsoleHost.Attach();
            Console.WriteLine(loc.Format("cli.help", GetVersion()));
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 0;
        }

        if (options.Version)
        {
            ConsoleHost.Attach();
            Console.WriteLine($"XNA Content Compiler {GetVersion()}");
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 0;
        }

        if (options.Build)
            return RunBuild(options, loc);

        return RunGui(options, settings);
    }

    private static int RunBuild(CliOptions options, LocalizationService loc)
    {
        ConsoleHost.Attach();

        var projectPath = options.ProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            Console.Error.WriteLine(loc.Format("cli.projectNotFound", projectPath ?? string.Empty));
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 2;
        }

        ContentProject project;
        try
        {
            project = ProjectSerializer.Load(projectPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(loc.Format("cli.error", ex.Message));
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 3;
        }

        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            project.OutputDirectory = Path.GetFullPath(options.OutputDirectory);

        if (options.Profile.HasValue)
            project.Profile = options.Profile.Value;

        var service = new ContentBuildService();
        service.Log += (_, entry) =>
        {
            var tag = entry.Level switch
            {
                BuildLogLevel.Error => "ERR",
                BuildLogLevel.Warning => "WARN",
                BuildLogLevel.Success => "OK",
                BuildLogLevel.Debug => "DBG",
                _ => "INFO",
            };
            Console.WriteLine($"[{tag}] {entry.Message}");
        };

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        BuildSummary summary;
        try
        {
            summary = service.Build(project, cancellation.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(loc.Format("cli.error", ex.Message));
            ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
            return 3;
        }

        var failures = summary.Items.Where(x => !x.Success).ToList();
        foreach (var failure in failures)
        {
            var detail = failure.ErrorDetail ?? failure.ErrorCode.ToString();
            Console.WriteLine($"  - {failure.Item.OutputName}: {detail}");
        }

        if (summary.Canceled)
            Console.WriteLine(loc["cli.canceled"]);

        Console.WriteLine(loc.Format(
            "cli.summary",
            summary.Succeeded,
            summary.Failed,
            FormatDuration(summary.Duration)));
        Console.WriteLine(project.OutputDirectory);

        ConsoleHost.PauseIfAllocated(loc["cli.pressEnter"]);
        return summary.Failed == 0 && !summary.Canceled ? 0 : 1;
    }

    private static int RunGui(CliOptions options, AppSettings settings)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            WriteCrashLog(e.Exception);
            var loc = LocalizationService.Instance;
            MessageBox.Show(
                loc.Format("error.unknown", e.Exception.Message),
                loc["dialog.error"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        try
        {
            var form = new MainForm(settings);
            if (!string.IsNullOrWhiteSpace(options.ProjectPath))
                form.OpenProjectOnStartup(options.ProjectPath);

            Application.Run(form);
            return 0;
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            var loc = LocalizationService.Instance;
            MessageBox.Show(
                loc.Format("error.unknown", ex.Message),
                loc["dialog.error"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }
    }

    /// <summary>
    /// Writes unhandled exceptions to %APPDATA%\XnaContentCompiler\logs so users can
    /// attach a report without reproducing the crash.
    /// </summary>
    private static void WriteCrashLog(Exception? exception)
    {
        if (exception == null)
            return;

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XnaContentCompiler", "logs");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(
                path,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Crash reporting must never throw.
        }
    }

    private static string GetVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{duration.TotalSeconds:0.0}s";
    }
}

internal sealed class CliOptions
{
    public bool Help { get; private set; }
    public bool Version { get; private set; }
    public bool Build { get; private set; }
    public string? ProjectPath { get; private set; }
    public string? OutputDirectory { get; private set; }
    public AppLanguage? Language { get; private set; }
    public GraphicsProfileSetting? Profile { get; private set; }

    public static CliOptions Parse(string[] args, out string? error)
    {
        var options = new CliOptions();
        error = null;
        string? positional = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            var normalized = argument.ToLowerInvariant();

            switch (normalized)
            {
                case "-h":
                case "--help":
                    options.Help = true;
                    break;

                case "--version":
                    options.Version = true;
                    break;

                case "-b":
                case "--build":
                    options.Build = true;
                    if (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
                    {
                        options.ProjectPath = args[++index];
                    }
                    break;

                case "-o":
                case "--output":
                    if (index + 1 >= args.Length)
                    {
                        error = "--output requires a directory.";
                        return options;
                    }
                    options.OutputDirectory = args[++index];
                    break;

                case "-l":
                case "--lang":
                    if (index + 1 >= args.Length)
                    {
                        error = "--lang requires a value (en, zh or auto).";
                        return options;
                    }
                    var languageText = args[++index];
                    if (languageText.Equals("auto", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (languageText.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Language = AppLanguage.ChineseSimplified;
                    }
                    else if (languageText.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Language = AppLanguage.English;
                    }
                    else
                    {
                        error = $"Invalid language '{languageText}'. Use en, zh or auto.";
                        return options;
                    }
                    break;

                case "--profile":
                    if (index + 1 >= args.Length)
                    {
                        error = "--profile requires a value (reach or hidef).";
                        return options;
                    }
                    if (!TryParseProfile(args[++index], out var profile))
                    {
                        error = $"Invalid profile '{args[index]}'. Use reach or hidef.";
                        return options;
                    }
                    options.Profile = profile;
                    break;

                default:
                    if (normalized.StartsWith("--build=", StringComparison.Ordinal))
                    {
                        options.Build = true;
                        options.ProjectPath = argument["--build=".Length..];
                    }
                    else if (normalized.StartsWith("--output=", StringComparison.Ordinal))
                    {
                        options.OutputDirectory = argument["--output=".Length..];
                    }
                    else if (normalized.StartsWith("--lang=", StringComparison.Ordinal))
                    {
                        var value = argument["--lang=".Length..];
                        if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                            options.Language = AppLanguage.ChineseSimplified;
                        else if (value.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                            options.Language = AppLanguage.English;
                        else if (!value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                        {
                            error = $"Invalid language '{value}'. Use en, zh or auto.";
                            return options;
                        }
                    }
                    else if (normalized.StartsWith("--profile=", StringComparison.Ordinal))
                    {
                        var profileValue = argument["--profile=".Length..];
                        if (!TryParseProfile(profileValue, out var parsedProfile))
                        {
                            error = $"Invalid profile '{profileValue}'. Use reach or hidef.";
                            return options;
                        }
                        options.Profile = parsedProfile;
                    }
                    else if (normalized.StartsWith('-'))
                    {
                        error = $"Unknown option '{argument}'.";
                        return options;
                    }
                    else
                    {
                        if (positional != null)
                        {
                            error = "Only one project file can be specified.";
                            return options;
                        }
                        positional = argument;
                    }
                    break;
            }
        }

        if (options.ProjectPath == null)
            options.ProjectPath = positional;
        else if (positional != null)
        {
            error = "Only one project file can be specified.";
            return options;
        }

        if (options.Build && string.IsNullOrWhiteSpace(options.ProjectPath))
            error = "--build requires a project file.";

        return options;
    }

    private static bool TryParseProfile(string value, out GraphicsProfileSetting profile)
    {
        if (value.Equals("hidef", StringComparison.OrdinalIgnoreCase))
        {
            profile = GraphicsProfileSetting.HiDef;
            return true;
        }

        if (value.Equals("reach", StringComparison.OrdinalIgnoreCase))
        {
            profile = GraphicsProfileSetting.Reach;
            return true;
        }

        profile = GraphicsProfileSetting.Reach;
        return false;
    }
}
