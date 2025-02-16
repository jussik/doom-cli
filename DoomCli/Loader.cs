﻿using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using nz.doom.WadParser;

namespace DoomCli;

public record WadFile(string FilePath, string Key, DateTimeOffset LastModified, IWadData Wad);

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(WadCache))]
internal partial class SourceGenerationContext : JsonSerializerContext;
    
public class WadCache
{
    public required int Version { get; init; }
    public required Dictionary<string, WadData> Wads { get; init; } = new();
}

public static class Loader
{
    private const int CacheVersion = 1;
    private static readonly string CachePath = Path.Combine(Path.GetTempPath(), "DoomCli_cache.json");
    
    public static IReadOnlyList<WadFile> LoadWads()
    {
        Console.Write("Loading WADs...\r");
        
        Dictionary<string, WadData> wads = GetWadsFromCache() ?? new();
        if (wads.Count > 0)
            Console.Write($"Loading WADS (found {wads.Count} in cache)...\r");

        HashSet<string> cachedKeys = new(wads.Keys);
        List<WadFile> wadFiles = LoadWadsImpl(wads);

        if (!cachedKeys.SetEquals(wadFiles.Select(w => w.Key)))
        {
            HashSet<string> wadKeys = wadFiles.Select(w => w.Key).ToHashSet();
            foreach (string key in cachedKeys.Except(wadKeys))
                wads.Remove(key);
            UpdateWadsToCache(wads);
        }

        wadFiles.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
        return wadFiles;
    }

    private static List<WadFile> LoadWadsImpl(Dictionary<string, WadData> wadsByKey)
    {
        using SHA256 sha = SHA256.Create();
        using var ms = new MemoryStream();

        List<WadFile> wadFiles = new();
        
        var zips = FileUtils.ListFiles("*.pk3").Concat(FileUtils.ListFiles("*.zip"));
        foreach (string zipPath in zips)
        {
            string key;
            using (var fs = File.OpenRead(zipPath))
            {
                key = GetFileKey(Path.GetFileName(zipPath), fs);
            }

            if (!wadsByKey.TryGetValue(key, out var wad))
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

                wadsByKey[key] = wad = builder.Build();
            }

            wadFiles.Add(new WadFile(zipPath, key, File.GetLastWriteTime(zipPath), wad));
        }

        foreach (string wadPath in FileUtils.ListFiles("*.wad"))
        {
            using var fs = File.OpenRead(wadPath);
            string key = GetFileKey(Path.GetFileName(wadPath), fs);

            if (!wadsByKey.TryGetValue(key, out var wad))
            {
                fs.Position = 0;
                wadsByKey[key] = wad = new WadDataBuilder(wadPath)
                    .Add(WadParser.Parse(fs))
                    .Build();
            }
            
            wadFiles.Add(new WadFile(wadPath, key, File.GetLastWriteTime(wadPath), wad));
        }

        return wadFiles;
        
        string GetFileKey(string filename, FileStream fs) =>
            $"{filename}:{Convert.ToBase64String(sha.ComputeHash(fs))}";
    }

    private static Dictionary<string, WadData>? GetWadsFromCache()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            using var cacheFile = File.OpenRead(CachePath);

            if (cacheFile.Length <= 0)
                return null;

            var cache = JsonSerializer.Deserialize(cacheFile, SourceGenerationContext.Default.WadCache);
            if (cache?.Version != CacheVersion)
            {
                Console.Write("Loading WADs (incompatible cache version, refreshing)...\r");
                return null;
            }

            return cache.Wads;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to load cache!{Environment.NewLine}{e}");
            return null;
        }
    }

    private static void UpdateWadsToCache(Dictionary<string, WadData> wads)
    {
        try
        {
            using var fs = File.OpenWrite(CachePath);
            fs.SetLength(0);
            JsonSerializer.Serialize(fs, new WadCache {Version = CacheVersion, Wads = wads}, SourceGenerationContext.Default.WadCache);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to update cache!{Environment.NewLine}{e}");
        }
    }
}