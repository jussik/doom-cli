namespace DoomCli;

public static class FileUtils
{
    public static IEnumerable<string> ListFiles(string pat) =>
        Directory.EnumerateFiles(Environment.CurrentDirectory, pat, SearchOption.AllDirectories);
}