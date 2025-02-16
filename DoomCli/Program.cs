using DoomCli;
using Sharprompt;

IReadOnlyList<WadFile> allWads = WadLoader.LoadWads();

var wizard = new ShortcutWizard(allWads);
if (wizard.BuildShortcut() is { } shortcut)
{
    Console.WriteLine($"Creating shortcut: {shortcut.ShortcutPath}");
    Console.WriteLine($"\"{shortcut.ExecutablePath}\" {shortcut.Arguments}");
    if (Prompt.Confirm("Continue?", true))
        shortcut.Save();
}
