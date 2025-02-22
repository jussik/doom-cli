using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using nz.doom.WadParser;

namespace DoomCli;

public record WadFile(string FilePath, string Key, DateTime LastModified, IWadData Wad);

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(WadCache))]
internal partial class WadLoaderSourceGenerationContext : JsonSerializerContext;
    
public class WadCache
{
    public required int Version { get; init; }
    public required Dictionary<string, WadData> Wads { get; init; } = new();
}

public class WadLoader
{
    private readonly Dictionary<string, WadData> wads = new();
    private readonly Dictionary<string, WadFile> wadFiles = new();
    private bool isDirty;
    
    private const int CacheVersion = 1;
    private static readonly string CachePath = Path.Combine(Path.GetTempPath(), "DoomCli_cache.json");

    public IEnumerable<WadFile> Wads => wadFiles.Values;
    
    public void LoadWads()
    {
        Console.Write("Loading WADs...\r");
        
        LoadWadsFromCache();
        if (wads.Count > 0)
            Console.Write($"Loading WADS (found {wads.Count} in cache)...\r");

        LoadWadsFromDisk();
        Console.WriteLine();
    }

    public WadFile AddFile(string path)
    {
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pk3", StringComparison.OrdinalIgnoreCase))
            return LoadZip(path);
        
        if (path.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
            return LoadWad(path);

        throw new ArgumentException("Unsupported file type");
    }

    private void LoadWadsFromDisk()
    {
        foreach (string zipPath in FileUtils.ListFiles("*.pk3").Concat(FileUtils.ListFiles("*.zip")))
        {
            LoadZip(zipPath);
        }

        foreach (string wadPath in FileUtils.ListFiles("*.wad"))
        {
            LoadWad(wadPath);
        }
    }

    private WadFile LoadWad(string wadPath)
    {
        DateTime lastModified = File.GetLastWriteTime(wadPath);
        string key = GetFileKey(wadPath, lastModified);
        if (!wads.TryGetValue(key, out var wad))
        {
            using var fs = File.OpenRead(wadPath);
            wads[key] = wad = new WadDataBuilder(wadPath)
                .Add(WadParser.Parse(fs))
                .Build();
            isDirty = true;
        }
            
        return wadFiles[key] = new WadFile(wadPath, key, lastModified, wad);
    }

    private WadFile LoadZip(string zipPath)
    {
        using var ms = new MemoryStream();
        DateTime lastModified = File.GetLastWriteTime(zipPath);
        string key = GetFileKey(zipPath, lastModified);
        if (!wads.TryGetValue(key, out var wad))
        {
            var builder = new WadDataBuilder(zipPath);

            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                builder.Add(zip);

                foreach (ZipArchiveEntry entry in zip.Entries.Where(e =>
                             e.Name.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)))
                {
                    ms.SetLength(0);
                    using (var fs = entry.Open())
                    {
                        fs.CopyTo(ms);
                    }

                    ms.Position = 0;
                    builder.Add(WadParser.Parse(ms));
                }
            }

            wads[key] = wad = builder.Build();
            isDirty = true;
        }

        return wadFiles[key] = new WadFile(zipPath, key, lastModified, wad);
    }

    private static string GetFileKey(string filePath, DateTime lastModified) =>
        $"{Path.GetFileName(filePath)}:{lastModified.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";

    private void LoadWadsFromCache()
    {
        try
        {
            if (!File.Exists(CachePath))
                return;

            using var cacheFile = File.OpenRead(CachePath);

            if (cacheFile.Length <= 0)
                return;

            var cache = JsonSerializer.Deserialize(cacheFile, WadLoaderSourceGenerationContext.Default.WadCache);
            if (cache?.Version != CacheVersion)
            {
                Console.Write("Loading WADs (incompatible cache version, refreshing)...\r");
                return;
            }

            foreach ((string? key, WadData value) in cache.Wads)
            {
                wads[key] = value;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to load cache!{Environment.NewLine}{e}");
        }
    }

    public void UpdateCacheFile()
    {
        if (!isDirty)
            return;
        try
        {
            using var fs = File.OpenWrite(CachePath);
            fs.SetLength(0);
            JsonSerializer.Serialize(fs, new WadCache {Version = CacheVersion, Wads = wads}, WadLoaderSourceGenerationContext.Default.WadCache);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to update cache!{Environment.NewLine}{e}");
        }
    }
}