using System.IO.Compression;
using System.Text.RegularExpressions;
using nz.doom.WadParser;

namespace DoomCli;

public partial class WadDataBuilder
{
    private readonly WadData data;

    public WadDataBuilder(string filePath)
    {
        data = new WadData { Name = Path.GetFileNameWithoutExtension(filePath) };
        if (KnownIwads.TryGetValue(Path.GetFileName(filePath), out string? iwadName))
        {
            data.IsIwad = true;
            data.IwadName = iwadName;
        }
    }

    private static readonly HashSet<string> KnownIwads = new(StringComparer.OrdinalIgnoreCase)
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

    public WadDataBuilder Add(ZipArchive zip) => Add(new ZipLumpArchive(zip));
    public WadDataBuilder Add(Wad wad) => Add(new WadLumpArchive(wad));
    private WadDataBuilder Add(ILumpArchive wad)
    {
        if (data.Complevel == null && wad.TryReadLump("COMPLVL", out string complvl))
            data.Complevel = complvl.Trim();
        
        if (wad.TryReadLump("GAMEINFO", out string gameinfo))
            ParseGameinfo(gameinfo);
        
        if (wad.TryReadLump("WADINFO", out string wadinfo))
            ParseWadinfo(wadinfo);

        return this;
    }

    private void ParseWadinfo(string lump)
    {
        if (data.Title == null && TryExtractValue(WadinfoTitleRegex(), lump, out string title))
            data.Title = title;
        
        if (data.Complevel == null && data.ComplevelHint == null &&
            TryExtractValue(WadinfoAdvEngRegex(), lump, out string hint))
        {
            data.ComplevelHint = hint;
        }

        if (data.IwadName == null && TryExtractValue(WadinfoGameRegex(), lump, out string game))
        {
            if (game.Contains("Ultimate Doom", StringComparison.OrdinalIgnoreCase))
                data.IwadName = "DOOM.WAD";
            else if (game.Equals("Doom II", StringComparison.OrdinalIgnoreCase))
                data.IwadName = "DOOM2.WAD";
            else
            {
                game = game.Replace(" ", "").ToUpperInvariant();
                if (!game.EndsWith(".WAD"))
                    game += ".WAD";
                data.IwadName = KnownIwads.Contains(game) ? game : null;
            }
        }
    }

    private void ParseGameinfo(string lump)
    {
        if (data.Title == null && TryExtractValue(GameinfoStartupTitleRegex(), lump, out string title))
            data.Title = title;
        
        if (data.IwadName == null && TryExtractValue(GameinfoIwadRegex(), lump, out string iwad))
        {
            iwad = iwad.ToUpperInvariant();
            if (!iwad.EndsWith(".WAD"))
                iwad += ".WAD";
            data.IwadName = iwad;
        }
    }

    private static bool TryExtractValue(Regex regex, string lump, out string value)
    {
        if (regex.Match(lump) is {Success: true} m)
        {
            value = m.Groups["text"].Value.Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }
        value = "";
        return false;
    }

    public WadData Build() => data;

    [GeneratedRegex(@"^startuptitle\s*=\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GameinfoStartupTitleRegex();

    [GeneratedRegex(@"^iwad\s*=\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GameinfoIwadRegex();

    [GeneratedRegex(@"^Title\s*:\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex WadinfoTitleRegex();
    
    [GeneratedRegex(@"^Advanced\s+engine\s+needed\s*:\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex WadinfoAdvEngRegex();
    
    [GeneratedRegex(@"^Game\s*:\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex WadinfoGameRegex();
}