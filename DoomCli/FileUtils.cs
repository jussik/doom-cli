using System.Text.RegularExpressions;

namespace DoomCli;

public static partial class FileUtils
{
    public static IEnumerable<string> ListFiles(string pat) =>
        Directory.EnumerateFiles(Environment.CurrentDirectory, pat, SearchOption.AllDirectories);

    public static string EvaluatePath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(SpecialFolderRegex().Replace(path,
            m => Environment.GetFolderPath(Enum.Parse<Environment.SpecialFolder>(m.Groups["enum"].Value, true)))));
    
    [GeneratedRegex(@"\$(?<enum>Desktop|StartMenu|MyDocuments)\$", RegexOptions.IgnoreCase)]
    private static partial Regex SpecialFolderRegex();
}