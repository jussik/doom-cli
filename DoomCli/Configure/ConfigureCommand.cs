using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Spectre.Console.Cli;

namespace DoomCli.Configure;

public class ConfigureCommand : Command<CommonSettings>
{
    private enum MenuOption
    {
        DefaultDownloadDirectory,
        DefaultSourcePort,
        ShortcutsDirectory,
        RegisterIdGamesProtocol,
        RemoveIdGamesProtocol,
        SaveAndExit,
        ExitWithoutSaving
    }
    
    public override int Execute(CommandContext context, CommonSettings settings)
    {
        var config = AppConfig.Load(settings);
        
        Console.WriteLine("Notes on configuration:");
        Console.WriteLine("Relative paths are supported, they are relative to the working directory when run");
        Console.WriteLine("Environment variables such as %APPDATA% are supported");
        Console.WriteLine("The substitutions $Desktop$, $StartMenu$ and $MyDocuments$ are available for common user folders");
        Console.WriteLine($"Configurations are stored in {settings.ConfigurationFile}");

        bool dirty = false;
        while (true)
        {
            List<Selection<MenuOption>> commonItems = [
                new(MenuOption.DefaultDownloadDirectory, $"Set default download directory (current: {config.DefaultDownloadDirectory})"),
                new(MenuOption.DefaultSourcePort, $"Set default source port (current: {config.DefaultSourcePort})"),
                new(MenuOption.ShortcutsDirectory, $"Set shortcuts directory (current: {config.ShortcutsDirectory})")
            ];

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (GetCurrentIdGamesProtocolCommand() is {} cmd)
                {
                    commonItems.Add(new(MenuOption.RegisterIdGamesProtocol, $"Reregister idgames:// protocol handler (current: {cmd})"));
                    commonItems.Add(new(MenuOption.RemoveIdGamesProtocol, "Remove idgames:// protocol handler"));
                }
                else
                {
                    commonItems.Add(new(MenuOption.RegisterIdGamesProtocol, "Register idgames:// protocol handler"));
                }
            }

            List<Selection<MenuOption>> items = dirty
                ?
                [
                    ..commonItems,
                    new(MenuOption.SaveAndExit, "Save and exit"),
                    new(MenuOption.ExitWithoutSaving, "Exit without saving")
                ]
                :
                [
                    ..commonItems,
                    new(MenuOption.ExitWithoutSaving, "Exit")
                ];

            
            var selection = CliPrompt.Select("Configure options", items);
            if (selection == MenuOption.ExitWithoutSaving)
                break;
            
            if (selection == MenuOption.SaveAndExit)
            {
                config.Save(settings);
                break;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (selection == MenuOption.RegisterIdGamesProtocol)
                {
                    RegisterIdGamesProtocol();
                    Console.WriteLine("Registered idgames:// protocol handler for current user");
                    continue;
                }
                if (selection == MenuOption.RemoveIdGamesProtocol)
                {
                    RemoveIdGamesProtocol();
                    Console.WriteLine("Removed idgames:// protocol handler for current user");
                    continue;
                }
            }

            if (selection == MenuOption.DefaultSourcePort)
            {
                Console.WriteLine(
                    "Source port executables are matched as long as the given value is contained somewhere in the path");
            }

            dirty |= selection switch
            {
                MenuOption.DefaultDownloadDirectory => UpdateConfig(config.DefaultDownloadDirectory,
                    v => config.DefaultDownloadDirectory = v, null, true),
                MenuOption.DefaultSourcePort => UpdateConfig(config.DefaultSourcePort,
                    v => config.DefaultSourcePort = v, null, false),
                MenuOption.ShortcutsDirectory => UpdateConfig(config.ShortcutsDirectory,
                    v => config.ShortcutsDirectory = v!, AppConfig.Defaults.ShortcutsDirectory, true),
                _ => false
            };
        }
        
        return 0;
    }

    [SupportedOSPlatform("windows")]
    private string? GetCurrentIdGamesProtocolCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\idgames");
        return key?.OpenSubKey(@"shell\open\command")?.GetValue(null) as string;
    }

    [SupportedOSPlatform("windows")]
    private void RegisterIdGamesProtocol()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\idgames");
        key.SetValue(null, "URL:idgames Protocol");
        key.SetValue("URL Protocol", "");
        using var commandKey = key.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(null, $"\"{Environment.GetCommandLineArgs()[0]}\" --relative-to-exe \"%1\"");
    }

    [SupportedOSPlatform("windows")]
    private void RemoveIdGamesProtocol()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\idgames");
    }

    private static bool UpdateConfig(string? currentValue, Action<string?> setValue, string? defaultValue, bool isPath)
    {
        string? newValue = CliPrompt.Input(defaultValue != null
            ? $"Enter new value, or empty to reset to default ({defaultValue})"
            : "Enter new value, or empty to clear option");
        if (string.IsNullOrWhiteSpace(newValue))
            newValue = defaultValue;
        
        setValue(newValue);
        
        if (isPath && newValue != null)
            Console.WriteLine("The given path would be expanded to: " + FileUtils.EvaluatePath(newValue));
        
        return currentValue != newValue;
    }
}