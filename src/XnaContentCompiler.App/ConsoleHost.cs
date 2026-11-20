using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace XnaContentCompiler.App;

/// <summary>
/// Attaches the WinExe process to the launching console (or creates one) so CLI mode
/// can print progress and errors.
/// </summary>
internal static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const uint Utf8CodePage = 65001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint codePage);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCP(uint codePage);

    public static bool WasAllocated { get; private set; }

    public static void Attach()
    {
        if (AttachConsole(AttachParentProcess))
        {
            WasAllocated = false;
        }
        else if (AllocConsole())
        {
            WasAllocated = true;
        }
        else
        {
            return;
        }

        SetConsoleOutputCP(Utf8CodePage);
        SetConsoleCP(Utf8CodePage);

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = true,
        });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false))
        {
            AutoFlush = true,
        });
        Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8));
    }

    public static void PauseIfAllocated(string prompt)
    {
        if (!WasAllocated)
            return;

        Console.WriteLine();
        Console.Write(prompt);
        try
        {
            Console.ReadLine();
        }
        catch (IOException)
        {
            // No interactive input available.
        }
    }
}
