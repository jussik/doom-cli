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
        Console.Write($"Loading WADs... from {Environment.CurrentDirectory}\r");
        
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
            using FileStream fs = File.OpenRead(wadPath);
            var builder = new WadDataBuilder(wadPath).Add(WadParser.Parse(fs));

            if (!builder.IsWadinfoComplete)
            {
                string textFile = Path.ChangeExtension(wadPath, "txt");
                if (File.Exists(textFile))
                    builder.AddWadinfoText(File.ReadAllText(textFile));
            }
            
            wads[key] = wad = builder.Build();
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

                if (!builder.IsWadinfoComplete)
                {
                    char[] testBuffer = new char[4];
                    foreach (ZipArchiveEntry entry in zip.Entries.Where(e =>
                                 e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                    {
                        // check if it's a potential WADINFO lump equivalent in a separate text file
                        using var reader = new StreamReader(entry.Open());
                        if (reader.ReadBlock(testBuffer, 0, 4) == 4 && testBuffer.SequenceEqual("===="))
                        {
                            builder.AddWadinfoText(reader.ReadToEnd());
                            if (builder.IsWadinfoComplete)
                                break;
                        }
                    }
                }
            }

            if (!builder.IsWadinfoComplete)
            {
                string textFile = Path.ChangeExtension(zipPath, "txt");
                if (File.Exists(textFile))
                    builder.AddWadinfoText(File.ReadAllText(textFile));
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
            JsonSerializer.Serialize(fs,
                // only cache wads we have found files for
                new WadCache {Version = CacheVersion, Wads = wadFiles.ToDictionary(w => w.Key, w => wads[w.Key])},
                WadLoaderSourceGenerationContext.Default.WadCache);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to update cache!{Environment.NewLine}{e}");
        }
    }
}