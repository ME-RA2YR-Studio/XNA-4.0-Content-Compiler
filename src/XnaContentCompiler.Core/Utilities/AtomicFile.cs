using System.IO;
using System.Text;

namespace XnaContentCompiler.Core.Utilities;

/// <summary>
/// Writes files through a temporary sibling file so a failed write never destroys the
/// previous contents.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            Replace(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents, new UTF8Encoding(false));
            Replace(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void Replace(string temporary, string destination)
    {
        if (File.Exists(destination))
            File.Replace(temporary, destination, null, ignoreMetadataErrors: true);
        else
            File.Move(temporary, destination);
    }
}
