// CubeGame stable launcher.
//
// Permanent, frozen Windows launcher. It never downloads anything, never writes into the
// game folder and knows nothing about build ids: the active game build is selected only by
// current.txt next to this executable.
//
//   Desktop shortcut
//     -> D:\Games\CubeGame\CubeGameLauncher.exe
//     -> D:\Games\CubeGame\current.txt                      (relative path, one line)
//     -> D:\Games\CubeGame\releases\<build-id>\CubeGame.exe  (Unity player, own folder)
//
// Behaviour:
//   1. Resolve the directory of *this* executable (not the current working directory).
//   2. Read current.txt from that directory.
//   3. Resolve its one-line relative path against that directory.
//   4. Require the target executable to exist.
//   5. Start it with the game folder as working directory and forward our own arguments.
//   6. Exit with 0 right after a successful start (do not wait for the game).
//   7. On any failure show a normal Windows error box and exit with a non-zero code.
//
// Frozen contract: D91_STABLE_LAUNCHER_CONTRACT.md. Do not rebuild or patch after freeze.
//
// Build (no extra environment needed; .NET Framework 4.x ships with Windows):
//   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /noconfig /target:winexe
//     /platform:anycpu /optimize+ /out:CubeGameLauncher.exe
//     /r:System.dll /r:System.Windows.Forms.dll CubeGameLauncher.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("CubeGame Launcher")]
[assembly: AssemblyDescription("Stable CubeGame launcher: starts the build selected by current.txt")]
[assembly: AssemblyProduct("CubeGame")]
[assembly: AssemblyCompany("CubeGame")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class CubeGameLauncher
{
    private const string CurrentFile = "current.txt";
    private const string Caption = "CubeGame";

    private const int ExitOk = 0;
    private const int ExitCurrentMissing = 1;
    private const int ExitCurrentInvalid = 2;
    private const int ExitTargetMissing = 3;
    private const int ExitStartFailed = 4;

    [STAThread]
    private static int Main(string[] args)
    {
        string launcherDirectory = LauncherDirectory();

        string currentPath = Path.Combine(launcherDirectory, CurrentFile);
        if (!File.Exists(currentPath))
        {
            return Fail(ExitCurrentMissing,
                "Cannot find " + CurrentFile + " next to the launcher.\n\n" +
                "Expected: " + currentPath + "\n\n" +
                "Install the game so that " + CurrentFile + " sits in the same folder as the launcher.");
        }

        string relativeTarget;
        try
        {
            relativeTarget = File.ReadAllText(currentPath, Encoding.UTF8).Trim();
            if (relativeTarget.Length > 0 && relativeTarget[0] == '\uFEFF')
            {
                relativeTarget = relativeTarget.Substring(1).Trim();
            }
        }
        catch (Exception error)
        {
            return Fail(ExitCurrentInvalid,
                "Cannot read " + currentPath + ".\n\n" + error.Message);
        }

        if (relativeTarget.Length == 0)
        {
            return Fail(ExitCurrentInvalid,
                currentPath + " is empty.\n\n" +
                "It must contain a relative path such as:\nreleases\\<build-id>\\CubeGame.exe");
        }

        if (Path.IsPathRooted(relativeTarget))
        {
            return Fail(ExitCurrentInvalid,
                currentPath + " must contain a path relative to the launcher folder, not an absolute path.\n\n" +
                "Found: " + relativeTarget);
        }

        string target;
        try
        {
            target = Path.GetFullPath(Path.Combine(launcherDirectory, relativeTarget));
        }
        catch (Exception error)
        {
            return Fail(ExitCurrentInvalid,
                currentPath + " contains an invalid path:\n" + relativeTarget + "\n\n" + error.Message);
        }

        if (!File.Exists(target))
        {
            return Fail(ExitTargetMissing,
                "The game build selected by " + CurrentFile + " does not exist.\n\n" +
                "Selected: " + relativeTarget + "\n" +
                "Missing:  " + target + "\n\n" +
                "Restore that build or point " + currentPath + " to an installed release.");
        }

        string workingDirectory = Path.GetDirectoryName(target);

        var startInfo = new ProcessStartInfo();
        startInfo.FileName = target;
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.Arguments = JoinArguments(args);
        startInfo.UseShellExecute = true;

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception error)
        {
            return Fail(ExitStartFailed,
                "Cannot start the game build:\n" + target + "\n\n" + error.Message);
        }

        return ExitOk;
    }

    private static string LauncherDirectory()
    {
        string location = null;
        try
        {
            location = Assembly.GetExecutingAssembly().Location;
        }
        catch (Exception)
        {
            // Fall through to AppDomain.BaseDirectory below.
        }

        if (!string.IsNullOrEmpty(location))
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(location));
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }

        return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
    }

    private static string JoinArguments(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int index = 0; index < args.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(args[index]));
        }

        return builder.ToString();
    }

    // Windows CRT argument quoting: a quoted argument keeps literal spaces and quotes.
    private static string QuoteArgument(string argument)
    {
        if (argument == null || argument.Length == 0)
        {
            return "\"\"";
        }

        bool needsQuotes = false;
        for (int index = 0; index < argument.Length; index++)
        {
            char symbol = argument[index];
            if (symbol == ' ' || symbol == '\t' || symbol == '\n' || symbol == '\v' || symbol == '"')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
        {
            return argument;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        int backslashes = 0;
        for (int index = 0; index < argument.Length; index++)
        {
            char symbol = argument[index];
            if (symbol == '\\')
            {
                backslashes++;
                continue;
            }

            if (symbol == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            if (backslashes > 0)
            {
                builder.Append('\\', backslashes);
                backslashes = 0;
            }

            builder.Append(symbol);
        }

        if (backslashes > 0)
        {
            builder.Append('\\', backslashes * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static int Fail(int exitCode, string message)
    {
        try
        {
            MessageBox.Show(message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception)
        {
            // A silent exit is forbidden by the contract, but if the message box itself fails
            // the exit code still reports the problem to the caller.
        }

        return exitCode;
    }
}
