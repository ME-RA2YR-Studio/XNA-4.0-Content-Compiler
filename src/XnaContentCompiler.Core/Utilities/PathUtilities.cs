using System.IO;

namespace XnaContentCompiler.Core.Utilities;

public static class PathUtilities
{
    public static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public static bool IsPathInside(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            fullRoot += Path.DirectorySeparatorChar;

        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a path relative to <paramref name="root"/> when possible, otherwise the
    /// original absolute path.
    /// </summary>
    public static string MakeRelativeIfPossible(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return path;

        try
        {
            var relative = Path.GetRelativePath(root, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                return Path.GetFullPath(path);

            return relative;
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    public static string ResolveRelativeTo(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(root, path));
    }
}
