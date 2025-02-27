using System.ComponentModel;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DoomCli.Shortcut;

public partial class ShortcutSettings : CommonSettings
{
    [Description("idgames:// URI to download from")]
    [CommandArgument(0, "[uri]")]
    public string? IdGamesUri { get; set; }

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrEmpty(IdGamesUri) && !IdgamesUriRegex().IsMatch(IdGamesUri))
            return ValidationResult.Error("Argument must be in the format idgames://<id>");
        
        return ValidationResult.Success();
    }

    [GeneratedRegex(@"^idgames://\d+/?$")]
    private static partial Regex IdgamesUriRegex();
}