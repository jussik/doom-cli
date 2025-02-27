using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DoomCli;

public partial class ShortcutSettings : CommandSettings
{
    [Description("Set working directory to the directory of the executable, useful for executing from a custom protocol link")]
    [CommandOption("--relative-to-exe")]
    public bool RelativeToExe { get; set; }
    
    [Description("idgames:// URI to download WAD from")]
    [CommandArgument(0, "[uri]")]
    public string IdGamesUri { get; set; }

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrEmpty(IdGamesUri) && !IdgamesUriRegex().IsMatch(IdGamesUri))
            return ValidationResult.Error("Argument must be in the format idgames://<id>");
        
        return ValidationResult.Success();
    }

    [GeneratedRegex(@"^idgames://\d+$")]
    private static partial Regex IdgamesUriRegex();
}