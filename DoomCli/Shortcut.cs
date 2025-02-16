using WindowsShortcutFactory;

namespace DoomCli;

public class Shortcut
{
    public required string Name { get; init; }
    public required string ShortcutPath { get; init; }
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);

        new WindowsShortcut
        {
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!,
            Path = ExecutablePath,
            Arguments = Arguments,
            Description = Name
        }.Save(ShortcutPath);
    }
}