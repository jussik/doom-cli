using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Sharprompt;

namespace DoomCli;

public class ShortcutWizard
{
    private static readonly string ShortcutsBasePath =
        Environment.ExpandEnvironmentVariables(@"%APPDATA%\Microsoft\Windows\Start Menu\Programs\Doom");

    public Shortcut? BuildShortcut(string[] args)
    {
        var loader = new WadLoader();
        loader.LoadWads();

        string? argWadPath = null;
        if (args.FirstOrDefault(a => a.StartsWith("idgames://")) is { } igUri)
        {
            using var idg = new IdGamesClient();
            IdGamesEntry entry = idg.GetEntry(igUri);
            
            Console.WriteLine($"idGames entry: {entry.Title}");

            if (loader.Wads.FirstOrDefault(w =>
                    w.FilePath.EndsWith(entry.Filename, StringComparison.OrdinalIgnoreCase) &&
                    new FileInfo(w.FilePath).Length == entry.Size) is {FilePath: var fp})
            {
                argWadPath = fp;
                Console.WriteLine($"File already exists at path: {argWadPath}");
            }
            else
            {
                string dlPath = Prompt.Select("Select download destination for WAD",
                                    loader.Wads
                                        .ToLookup(w => Path.GetDirectoryName(w.FilePath)!)
                                        .OrderByDescending(g => g.Count())
                                        .Take(5)
                                        .Select(string? (g) => g.Key)
                                        .Append(null),
                                    textSelector: p => p ?? "Custom...")
                                ?? Prompt.Input<string>(
                                    "Destination path relative to current directory, or leave empty for current directory");

                argWadPath = Path.Combine(Environment.CurrentDirectory, dlPath, entry.Filename);
                Console.WriteLine($"Downloading to {argWadPath}...");
                
                Stopwatch debounce = Stopwatch.StartNew();
                idg.Download(entry, argWadPath, prog =>
                {
                    if (debounce.ElapsedMilliseconds < 100)
                        return;
                    Console.Write($"\r{100.0 * prog.BytesReceived / prog.TotalBytes:#}% ({prog.BytesReceived / 1024:N0} of {prog.TotalBytes / 1024:N0} KB)");
                    debounce.Restart();
                });
                Console.Write('\r');

                loader.AddFile(argWadPath);
            }
        }
        
        loader.UpdateCacheFile();
        
        List<string> allExes = FileUtils.ListFiles("*.exe")
            .Where(f => !Path.GetFileName(f).Equals(
                Environment.ProcessPath is { } exePath ? Path.GetFileName(exePath) : "DoomCli.exe",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (allExes.Count == 0)
        {
            Console.WriteLine("No executables found");
            return null;
        }

        if (!loader.Wads.Any(w => w.Wad.IsIwad))
        {
            Console.WriteLine("No IWADs found");
            return null;
        }
        
        List<WadFile> selectedWads = SelectWads();
        List<WadFile> pwads = selectedWads.Where(w => !w.Wad.IsIwad).ToList();
        WadFile? iwad = SelectIwad();
        if (iwad == null)
            return null; // PWAD specified IWAD that doesn't exist

        string? complevel = SelectComplevel();
        string exe = SelectExe();
        string shortcutName = GetShortcutName();

        return new Shortcut
        {
            Name = shortcutName,
            ShortcutPath = Path.Combine(ShortcutsBasePath, shortcutName + ".lnk"),
            ExecutablePath = exe,
            Arguments = BuildArguments()
        };

        List<WadFile> SelectWads()
        {
            WadFile? argWad = loader.Wads.FirstOrDefault(w => w.FilePath == argWadPath);
            return Prompt.MultiSelect(new MultiSelectOptions<WadFile>
            {
                Items = argWad != null
                    ? loader.Wads.OrderByDescending(w => w == argWad)
                        .ThenByDescending(w => w.LastModified)
                    : loader.Wads.OrderByDescending(w => w.LastModified),
                DefaultValues = argWad != null ? [argWad] : [],
                TextSelector = t => $"{t.Wad.Title ?? t.Wad.Name} ({t.FilePath})",
                Minimum = 1,
                Message = "Select WADs to include"
            }).ToList();
        }

        WadFile? SelectIwad()
        {
            if (selectedWads.FirstOrDefault(w => w.Wad.IsIwad) is { } iwadItself)
                return iwadItself;

            string? iwadName = pwads.Select(w => w.Wad.IwadName).FirstOrDefault(n => n != null);
            if (iwadName != null)
            {
                if (!iwadName.EndsWith(".WAD", StringComparison.OrdinalIgnoreCase))
                    iwadName += ".WAD";
                if (loader.Wads.FirstOrDefault(w => w.Wad.IsIwad && w.Wad.IwadName == iwadName) is { } implicitIwad)
                {
                    Console.WriteLine($"IWAD already specified: {implicitIwad.Wad.IwadName}");
                    return implicitIwad;
                }

                Console.WriteLine($"IWAD specified, but not found: {iwadName}");
                return null;
            }

            return Prompt.Select(new SelectOptions<WadFile>
            {
                Items = loader.Wads.Where(w => w.Wad.IsIwad).OrderBy(w => w.FilePath),
                TextSelector = t => $"{t.Wad.Title ?? t.Wad.Name} ({t.FilePath})",
                Message = "Select IWAD"
            });
        }

        string? SelectComplevel()
        {
            if (pwads.Select(w => w.Wad.Complevel).FirstOrDefault(cl => cl != null) is { } implComplevel)
            {
                Console.WriteLine($"Complevel already specified: {implComplevel}");
                return null;
            }

            var prompt = "Select compatibility level";
            if (pwads.Select(w => w.Wad.ComplevelHint).FirstOrDefault(h => h != null) is { } hint)
                prompt += $" (hint: {hint})";
            
            int vanillaLevel = iwad.Wad.IwadName switch
            {
                "DOOM2.WAD" => 2,
                "DOOM.WAD" or "DOOM1.WAD" => 3,
                "TNT.WAD" or "PLUTONIA.WAD" => 4,
                _ => 0
            };
            int? selectedCl = Prompt.Select(new SelectOptions<(string name, int? value)>
            {
                Message = prompt,
                Items =
                [
                    ("None", null),
                    ($"Vanilla/Limit removing ({vanillaLevel})", vanillaLevel),
                    ("Boom (9)", 9),
                    ("MBF (11)", 11),
                    ("MBF21 (21)", 21),
                    ("Custom...", -1)
                ],
                TextSelector = p => p.name
            }).value;

            return selectedCl == -1
                ? Prompt.Input<string>("Enter complevel parameter")
                : selectedCl?.ToString();
        }

        string SelectExe() => Prompt.Select("Select executable", allExes);

        string GetShortcutName()
        {
            const string customLabel = "Custom...";
            List<string> potentialNames = selectedWads
                .SelectMany(w => w.Wad.Title != null ? new[] {w.Wad.Title, w.Wad.Name} : new[] {w.Wad.Name})
                .Select(s => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s))
                .Distinct()
                .Order(StringComparer.InvariantCultureIgnoreCase)
                .Append(customLabel)
                .ToList();
            string givenName = Prompt.Select("Shortcut name?", potentialNames, defaultValue: potentialNames[0]);
            return givenName != customLabel
                ? givenName
                : Prompt.Input<string>("Enter shortcut name",
                    validators:
                    [
                        o => o is string s && !string.IsNullOrWhiteSpace(s)
                            ? ValidationResult.Success
                            : new ValidationResult("Name cannot be empty")
                    ]);
        }

        string BuildArguments()
        {
            var argsBuilder = new StringBuilder();
            argsBuilder.Append("-iwad \"");
            argsBuilder.Append(Path.Combine(Environment.CurrentDirectory, iwad.FilePath));
            argsBuilder.Append('"');

            if (pwads.Count > 0)
            {
                argsBuilder.Append(" -file");
                foreach (var pwad in pwads)
                {
                    argsBuilder.Append(" \"");
                    argsBuilder.Append(Path.Combine(Environment.CurrentDirectory, pwad.FilePath));
                    argsBuilder.Append('"');
                }
            }

            if (complevel != null)
            {
                argsBuilder.Append(" -complevel ");
                argsBuilder.Append(complevel);
            }

            return argsBuilder.ToString();
        }
    }
}