using System.Drawing;
using System.Reflection;

namespace XnaContentCompiler.App.UI;

/// <summary>
/// Provides the application icon to every window. The icon is embedded in the assembly
/// (and also set as the executable icon), so it works in both normal and single-file
/// deployments without reading files from disk.
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "app.ico";

    private static Icon? _icon;
    private static bool _loaded;

    public static Icon? Get()
    {
        if (_loaded)
            return _icon;

        _loaded = true;
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourceName);

            if (stream != null)
                _icon = new Icon(stream);
        }
        catch (Exception)
        {
            // A missing or invalid icon must never prevent the application from starting.
        }

        return _icon;
    }

    public static void Apply(Form form)
    {
        var icon = Get();
        if (icon != null)
            form.Icon = icon;
    }
}
