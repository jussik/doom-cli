using System.Text;
using System.Text.Json;
using Sharprompt;
using WindowsShortcutFactory;

var config = File.Exists("doomcli.json")
    ? JsonSerializer.Deserialize<Config>(File.ReadAllText("doomcli.json")) ?? new()
    : new Config();

var port = GetSelection("port", config.PortsPath, false, "*.exe");
var iwad = GetSelection("IWAD", config.IWadsPath, false, "*.wad");
List<string> pwads = new();
while(true)
{
    var selectedPwad = GetSelection("PWAD", config.PWadsPath, true, "*.wad", "*.pk3");
    if (selectedPwad == "-")
        break;
    pwads.Add(selectedPwad);
}

var name = Prompt.Input<string>("Shortcut name?", pwads.Count > 0
    ? string.Join(" ", pwads.Select(wad => Path.GetFileName(Path.GetDirectoryName(wad))).Distinct())
    : Path.GetFileName(iwad));

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
var shortcutPath = Path.Combine(Environment.CurrentDirectory, config.ShortcutsPath, name + ".lnk");
var portFull = Path.Combine(Environment.CurrentDirectory, port);

Console.WriteLine("Creating shortcut:");
Console.WriteLine($"Location: {shortcutPath}");
Console.WriteLine($"Arguments: \"{portFull}\" {arguments}");

Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, config.ShortcutsPath));

new WindowsShortcut
{
    WorkingDirectory = Path.GetDirectoryName(portFull),
    Path = portFull,
    Arguments = arguments,
    Description = name
}.Save(shortcutPath);

return;

string GetSelection(string type, string portsPath, bool allowSkip, params string[] searchPatterns)
{
    var options = searchPatterns
        .Aggregate(Enumerable.Empty<string>(),
            (acc, pat) => acc.Concat(Directory.EnumerateFiles(portsPath, pat, SearchOption.AllDirectories)))
        .Order()
        .ToList();
    
    if (options.Count == 0)
        Console.Error.WriteLine($"No {type}s found under {portsPath}");
    
    if (allowSkip)
        options.Insert(0, "-");
    
    var selected = Prompt.Select($"Which {type}?" + (allowSkip ? " ('-' to skip)" : ""), options);
    Console.WriteLine($"Selected {type}: " + selected);
    return selected;
}

internal class Config
{
    public string PortsPath { get; set; } = "Ports";
    public string IWadsPath { get; set; } = "IWADs";
    public string PWadsPath { get; set; } = "PWADs";
    public string ShortcutsPath { get; set; } = "Shortcuts";
}