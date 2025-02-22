using DoomCli;
using Sharprompt;

// Set working directory to the directory of the executable to ensure relative paths are resolved correctly
if (args.Contains("--relative-to-exe") && Environment.ProcessPath is { } exePath)
    Directory.SetCurrentDirectory(Path.GetDirectoryName(exePath)!);

if (new ShortcutWizard().BuildShortcut(args) is { } shortcut)
{
    Console.WriteLine($"Creating shortcut: {shortcut.ShortcutPath}");
    Console.WriteLine($"\"{shortcut.ExecutablePath}\" {shortcut.Arguments}");
    if (Prompt.Confirm("Continue?", true))
        shortcut.Save();
}