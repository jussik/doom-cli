using System.ComponentModel;
using Spectre.Console.Cli;

namespace DoomCli;

public class CommonSettings : CommandSettings
{
    [Description("Path to the configuration file")]
    [CommandOption("--config-file")]
    [DefaultValue(@"%USERPROFILE%\.DoomCli.json")]
    public string ConfigurationFile { get; set; }
    
    [Description("Set working directory to the directory of the executable, useful for executing from a custom protocol link")]
    [CommandOption("--relative-to-exe")]
    public bool RelativeToExe { get; set; }
}