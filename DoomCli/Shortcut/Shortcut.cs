using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WindowsShortcutFactory;

namespace DoomCli.Shortcut;

public class Shortcut
{
    public required string Name { get; init; }
    public required string ShortcutPath { get; init; }
    public required string ExecutablePath { get; init; }
    public required string Arguments { get; init; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            CreateWindowsShortcut();
        else
            CreateLinuxShortcut();
    }
    
    
    [SupportedOSPlatform("windows")]
    private void CreateWindowsShortcut()
    {
        new WindowsShortcut
        {
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath)!,
            Path = ExecutablePath,
            Arguments = Arguments,
            Description = Name
        }.Save(ShortcutPath);
    }
    
    private void CreateLinuxShortcut()
    {
        File.WriteAllText(ShortcutPath, $"""
            [Desktop Entry]
            Type=Application

            Name={Name}
            Path={Path.GetDirectoryName(ExecutablePath)}
            Exec={ExecutablePath} {Arguments}
            Terminal=false
            Categories=Game;
            
            """);
    }
}