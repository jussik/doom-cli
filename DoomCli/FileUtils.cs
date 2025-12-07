using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DoomCli;

public static partial class FileUtils
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        MatchCasing = MatchCasing.CaseInsensitive
    };
    public static IEnumerable<string> ListFiles(string pat) =>
        Directory.EnumerateFiles(Environment.CurrentDirectory, pat, EnumerationOptions);

    public static string EvaluatePath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(SpecialFolderRegex().Replace(path,
                m => Environment.GetFolderPath(Enum.Parse<Environment.SpecialFolder>(m.Groups["enum"].Value, true)))));
        }
        return path == "~"
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : path.StartsWith("~/")
                ? Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]))
                : path;
    }

    [GeneratedRegex(@"\$(?<enum>Desktop|StartMenu|MyDocuments)\$", RegexOptions.IgnoreCase)]
    private static partial Regex SpecialFolderRegex();
}