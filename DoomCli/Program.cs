using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Sharprompt;
using WindowsShortcutFactory;

string shortcutsPath = Environment.ExpandEnvironmentVariables(@"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Doom");
HashSet<string> knownIwads = new(StringComparer.OrdinalIgnoreCase)
{
    "DOOM1.WAD",
    "DOOM.WAD",
    "DOOM2.WAD",
    "TNT.WAD",
    "PLUTONIA.WAD",
    "HERETIC1.WAD",
    "HERETIC.WAD",
    "HEXEN.WAD",
    "HEXDD.WAD",
    "STRIFE0.WAD",
    "STRIFE1.WAD",
    "VOICES.WAD"
};

var portOpts = FindFiles("*.exe")
    .Where(f => !Path.GetFileName(f).Equals(
        Environment.ProcessPath is { } exePath ? Path.GetFileName(exePath) : "DoomCli.exe",
        StringComparison.OrdinalIgnoreCase));
var port = GetSelectionSingle("port", portOpts);

var wadsByType = FindFiles("*.wad")
    .ToLookup(f => knownIwads.Contains(Path.GetFileName(f)));
// true = IWAD, false = PWAD
string iwad = GetSelectionSingle("IWAD", wadsByType[true]);
IReadOnlyList<string> pwads = GetSelectionMultiple("PWADs",
    wadsByType[false].Concat(FindFiles("*.pk3")).Concat(FindFiles("*.zip")));

const string customLabel = "Custom...";
List<string> potentialNames = pwads.Count > 0
    ? pwads.Select(Path.GetFileNameWithoutExtension) // PWAD filenames
        .Concat(pwads.Select(wad => Path.GetFileName(Path.GetDirectoryName(wad)))) // PWAD parent folder names
        .Select(s => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s!))
        .Distinct()
        .Order()
        .Append(customLabel)
        .ToList()
    : [Path.GetFileNameWithoutExtension(iwad), customLabel]; // IWAD filename if no PWADs
string? name = Prompt.Select("Shortcut name?", potentialNames, defaultValue: potentialNames[0]);

if (name == customLabel)
{
    name = Prompt.Input<string>("Specify shortcut name",
        validators:
        [
            o => o is string s && !string.IsNullOrWhiteSpace(s)
                ? ValidationResult.Success
                : new ValidationResult("Name cannot be empty")
        ]);
}

var argsBuilder = new StringBuilder();
argsBuilder.Append("-iwad \"");
argsBuilder.Append(Path.Combine(Environment.CurrentDirectory, iwad));
argsBuilder.Append('"');
if (pwads.Count > 0)
{
    argsBuilder.Append(" -file");
    foreach (var pwad in pwads)
    {
        argsBuilder.Append(" \"");
        argsBuilder.Append(Path.Combine(Environment.CurrentDirectory, pwad));
        argsBuilder.Append('"');
    }
}
var arguments = argsBuilder.ToString();
var shortcutPath = Path.Combine(Environment.CurrentDirectory, shortcutsPath, name + ".lnk");
var portFull = Path.Combine(Environment.CurrentDirectory, port);

Console.WriteLine("Creating shortcut:");
Console.WriteLine($"Location: {shortcutPath}");
Console.WriteLine($"Arguments: \"{portFull}\" {arguments}");
if (Prompt.Confirm("Continue?", true))
{
    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, shortcutsPath));

    new WindowsShortcut
    {
        WorkingDirectory = Path.GetDirectoryName(portFull),
        Path = portFull,
        Arguments = arguments,
        Description = name
    }.Save(shortcutPath);
}

return;

string GetSelectionSingle(string type, IEnumerable<string> opts)
{
    var options = opts.ToList();
    if (options.Count == 0)
        Console.Error.WriteLine($"No {type}s found");
    
    options.Sort();
    return Prompt.Select($"Which {type}?", options);
}

IReadOnlyList<string> GetSelectionMultiple(string type, IEnumerable<string> opts)
{
    var options = opts.ToList();
    if (options.Count == 0)
        return [];
    
    options.Sort();
    return Prompt.MultiSelect($"Which {type}?", options, minimum: 0).ToList();
}

IEnumerable<string> FindFiles(string pat) =>
    Directory.EnumerateFiles(Environment.CurrentDirectory, pat, SearchOption.AllDirectories);
