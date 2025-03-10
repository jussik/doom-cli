﻿using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console.Cli;

namespace DoomCli.Shortcut;

public partial class ShortcutCommand : Command<ShortcutSettings>
{
    public override int Execute(CommandContext context, ShortcutSettings settings)
    {
        var config = AppConfig.Load(settings);

        // Set working directory to the directory of the executable to ensure relative paths are resolved correctly
        if (settings.RelativeToExe && Environment.ProcessPath is { } exePath)
            Directory.SetCurrentDirectory(Path.GetDirectoryName(exePath)!);
        
        var loader = new WadLoader();
        loader.LoadWads();

        WadFile? argWad = GetWadFromArgs();
        
        loader.UpdateCacheFile();
        
        List<string> allExes = FileUtils.ListFiles("*.exe")
            .Where(f => !Path.GetFileName(f).Equals(
                Environment.ProcessPath is { } path ? Path.GetFileName(path) : "DoomCli.exe",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (allExes.Count == 0)
        {
            Console.WriteLine("No executables found");
            return 1;
        }

        if (!loader.Wads.Any(w => w.Wad.IsIwad))
        {
            Console.WriteLine("No IWADs found");
            return 1;
        }
        
        List<WadFile> selectedWads = SelectWads();
        List<WadFile> pwads = selectedWads.Where(w => !w.Wad.IsIwad).ToList();
        WadFile? iwad = SelectIwad();
        if (iwad == null)
            return 1; // PWAD specified IWAD that doesn't exist

        string? complevel = SelectComplevel();
        string exe = SelectExe();
        string shortcutName = GetShortcutName();

        var shortcut = new Shortcut
        {
            Name = shortcutName,
            ShortcutPath = Path.Combine(FileUtils.EvaluatePath(config.ShortcutsDirectory), shortcutName + ".lnk"),
            ExecutablePath = exe,
            Arguments = BuildArguments()
        };
        
        Console.WriteLine($"Creating shortcut: {shortcut.ShortcutPath}");
        Console.WriteLine($"\"{shortcut.ExecutablePath}\" {shortcut.Arguments}");
        if (CliPrompt.Confirm("Continue?", true))
            shortcut.Save();

        return 0;

        WadFile? GetWadFromArgs()
        {
            if (string.IsNullOrEmpty(settings.IdGamesUri))
                return null;

            IdGamesEntry entry;
            using (var idg = new IdGamesClient())
            {
                entry = idg.GetEntry(settings.IdGamesUri);
            }
            
            Console.WriteLine($"Found idGames entry '{entry.Title}' at {settings.IdGamesUri}");

            if (loader.Wads.FirstOrDefault(w =>
                    w.FilePath.EndsWith(entry.Filename, StringComparison.OrdinalIgnoreCase) &&
                    new FileInfo(w.FilePath).Length == entry.Size) is {} wad)
            {
                Console.WriteLine($"File already exists at path: {wad.FilePath}");
                return wad;
            }

            List<Selection<string?>> dlPaths = loader.Wads
                .ToLookup(w => Path.GetDirectoryName(w.FilePath)!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new Selection<string?>(g.Key, g.Key))
                .Append(new Selection<string?>(null, "Custom..."))
                .ToList();

            string? defaultPath = null;
            if (!string.IsNullOrWhiteSpace(config.DefaultDownloadDirectory))
            {
                defaultPath = FileUtils.EvaluatePath(config.DefaultDownloadDirectory);
                dlPaths.RemoveAll(p => p.Value == defaultPath);
                dlPaths.Insert(0, new Selection<string?>(defaultPath, defaultPath));
            }

            string dlPath = CliPrompt.Select("Select download destination for WAD", dlPaths, defaultPath)
                            ?? CliPrompt.Input(
                                "Destination path relative to current directory, or leave empty for current directory");

            string wadPath = Path.Combine(Environment.CurrentDirectory, dlPath, entry.Filename);
            Console.WriteLine($"Downloading to {wadPath}...");
                
            Stopwatch debounce = Stopwatch.StartNew();
            IdGamesClient.Download(entry, wadPath, prog =>
            {
                if (debounce.ElapsedMilliseconds < 50)
                    return;
                Console.Write($"\r{100.0 * prog.BytesReceived / prog.TotalBytes:#}% ({prog.BytesReceived / 1024:N0} of {prog.TotalBytes / 1024:N0} KB)");
                debounce.Restart();
            });
            Console.Write('\r');

            return loader.AddFile(wadPath);
        }

        List<WadFile> SelectWads()
            => CliPrompt.MultiSelect("Select WADs to include",
                (argWad != null
                    ? loader.Wads.OrderByDescending(w => w == argWad)
                        .ThenByDescending(w => w.LastModified)
                    : loader.Wads.OrderByDescending(w => w.LastModified))
                .Select(w => new Selection<WadFile>(w, $"{w.Wad.Title ?? w.Wad.Name} ({w.FilePath})")),
                argWad != null ? [argWad] : []
            ).ToList();

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

            return CliPrompt.Select("Select IWAD", loader.Wads
                .Where(w => w.Wad.IsIwad)
                .OrderBy(w => w.FilePath)
                .Select(w => new Selection<WadFile>(w, $"{w.Wad.Title ?? w.Wad.Name} ({w.FilePath})")));
        }

        string? SelectComplevel()
        {
            if (pwads.Select(w => w.Wad.Complevel).FirstOrDefault(cl => cl != null) is { } implComplevel)
            {
                Console.WriteLine($"Complevel already specified: {implComplevel}");
                return null;
            }
            
            int vanillaLevel = iwad.Wad.IwadName switch
            {
                "DOOM2.WAD" => 2,
                "DOOM.WAD" or "DOOM1.WAD" => 3,
                "TNT.WAD" or "PLUTONIA.WAD" => 4,
                _ => 0
            };
            Selection<int?>[] items = [
                new(null, "Default or ZDoom"),
                new(vanillaLevel, $"Vanilla or Limit removing ({vanillaLevel})"),
                new(9, "Boom (9)"),
                new(11, "MBF (11)"),
                new(21, "MBF21 (21)"),
                new(-1, "Custom...")
            ];

            var prompt = "Select compatibility level";
            int? defaultValue = null;
            if (pwads.Select(w => w.Wad.ComplevelHint).FirstOrDefault(h => h != null) is { } hint)
            {
                prompt += $" (hint: {hint})";
                if (hint.Contains("MBF21", StringComparison.OrdinalIgnoreCase) ||
                    hint.Contains("MBF 21", StringComparison.OrdinalIgnoreCase))
                    defaultValue = 21;
                else if (hint.Contains("MBF", StringComparison.OrdinalIgnoreCase))
                    defaultValue = 11;
                else if (hint.Contains("Boom", StringComparison.OrdinalIgnoreCase))
                    defaultValue = 9;
                else if (hint.Contains("Vanilla", StringComparison.OrdinalIgnoreCase) ||
                         hint.Contains("Limit removing", StringComparison.OrdinalIgnoreCase))
                    defaultValue = vanillaLevel;
            }

            int? selectedCl = CliPrompt.Select(prompt, items, defaultValue);

            return selectedCl == -1
                ? CliPrompt.Input("Enter complevel parameter")
                : selectedCl?.ToString();
        }

        string SelectExe()
        {
            string? defaultExe = null;
            if (!string.IsNullOrWhiteSpace(config.DefaultSourcePort))
            {
                string defaultExePattern = config.DefaultSourcePort;
                if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                {
                    // ensure paths are normalized to the current OS
                    defaultExePattern = defaultExePattern
                        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                }
                defaultExe = allExes.FirstOrDefault(e => e.Contains(defaultExePattern, StringComparison.OrdinalIgnoreCase));
            }
            return CliPrompt.Select("Select executable", allExes.Select(s => new Selection<string>(s, s)), defaultExe);
        }

        string GetShortcutName()
        {
            char[] illegalChars = Path.GetInvalidFileNameChars();
            var potentialNames = selectedWads
                .SelectMany(w => w.Wad.Title != null ? new[] {w.Wad.Title, w.Wad.Name} : new[] {w.Wad.Name})
                .Select(s =>
                {
                    s = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
                    s = string.Concat(s.Split(illegalChars, StringSplitOptions.RemoveEmptyEntries));
                    s = WhitespaceRegex().Replace(s, " ");
                    return s;
                })
                .Distinct()
                .Order(StringComparer.InvariantCultureIgnoreCase)
                .Select(s => new Selection<string?>(s, s))
                .Append(new Selection<string?>(null, "Custom..."))
                .ToList();
            return CliPrompt.Select("Shortcut name?", potentialNames, potentialNames[0].Value)
                   ?? CliPrompt.Input("Enter shortcut name",
                       s => string.IsNullOrWhiteSpace(s)
                           ? new ValidationResult("Name cannot be empty")
                           : s.IndexOfAny(illegalChars) >= 0
                               ? new ValidationResult("Name contains illegal characters")
                               : ValidationResult.Success);
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

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespaceRegex();
}