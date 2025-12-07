using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomCli;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(AppConfig))]
internal partial class AppConfigSourceGenerationContext : JsonSerializerContext;

public class AppConfig
{
    public string? WadsDirectory { get; set; }
    public string? DefaultDownloadDirectory { get; set; }
    public string? DefaultSourcePort { get; set; }
    public string ShortcutsDirectory { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"$StartMenu$\Doom" : "~/.local/share/applications";
    
    public static AppConfig Load(CommonSettings settings)
    {
        string configPath = FileUtils.EvaluatePath(settings.ConfigurationFile);
        if (!File.Exists(configPath))
            return new AppConfig();
        
        using var fs = File.OpenRead(configPath);
        return JsonSerializer.Deserialize(fs, AppConfigSourceGenerationContext.Default.AppConfig)!;
    }

    public void Save(CommonSettings settings)
    {
        string configPath = FileUtils.EvaluatePath(settings.ConfigurationFile);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            
        using var fs = File.OpenWrite(configPath);
        fs.SetLength(0);
        JsonSerializer.Serialize(fs, this, AppConfigSourceGenerationContext.Default.AppConfig);
    }
    
    public static readonly AppConfig Defaults = new();
}