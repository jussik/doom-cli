using System.ComponentModel;
using System.Runtime.InteropServices;
using Spectre.Console.Cli;

namespace DoomCli;

public class CommonSettings : CommandSettings
{
    [Description("Path to the configuration file")]
    [CommandOption("--config-file")]
    [DefaultConfigPath]
    public string ConfigurationFile { get; set; } = DefaultConfigPathAttribute.Path;
    
    [Description("Set working directory to the directory of the executable, useful for executing from a custom protocol link")]
    [CommandOption("--relative-to-exe")]
    public bool RelativeToExe { get; set; }

    private class DefaultConfigPathAttribute() : DefaultValueAttribute(Path)
    {
        public static readonly string Path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"%USERPROFILE%\.DoomCli.json" : "~/.config/DoomCli.json";
    }
}