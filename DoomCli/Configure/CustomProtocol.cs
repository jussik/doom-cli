using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DoomCli.Configure;

public abstract class CustomProtocol
{
    public abstract string? GetCurrentIdGamesProtocolCommand();
    public abstract void RegisterIdGamesProtocol(string cmd);
    public abstract void RemoveIdGamesProtocol();
}

[SupportedOSPlatform("windows")]
public class CustomProtocolWindows : CustomProtocol
{
    public override string? GetCurrentIdGamesProtocolCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\idgames");
        return key?.OpenSubKey(@"shell\open\command")?.GetValue(null) as string;
    }
    public override void RegisterIdGamesProtocol(string cmd)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\idgames");
        key.SetValue(null, "URL:idgames Protocol");
        key.SetValue("URL Protocol", "");
        using var commandKey = key.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(null, $"{cmd} --relative-to-exe \"%1\"");
    }
    public override void RemoveIdGamesProtocol()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\idgames");
    }
}

public class CustomProtocolLinux : CustomProtocol
{
    private static readonly string ApplicationsPath = FileUtils.EvaluatePath("~/.local/share/applications");
    private static readonly string MimeInfoCachePath = Path.Combine(ApplicationsPath, "mimeinfo.cache");
    private static readonly string DesktopFilePath = Path.Combine(ApplicationsPath, "doomcli-idgames-protocol.desktop");
    
    public override string? GetCurrentIdGamesProtocolCommand()
        => File.Exists(MimeInfoCachePath)
            ? File.ReadAllLines(MimeInfoCachePath)
                .Where(l => l.StartsWith("x-scheme-handler/idgames="))
                .Select(l => l.Split('=')[1])
                .FirstOrDefault()
            : null;

    public override void RegisterIdGamesProtocol(string cmd)
    {
        // Write .desktop file to handle idgames:// protocol
        Directory.CreateDirectory(ApplicationsPath);
        File.WriteAllText(DesktopFilePath, $"""
            [Desktop Entry]
            Name=DoomCli idgames Protocol Handler
            Exec={cmd} %u
            Type=Application
            Terminal=true
            MimeType=x-scheme-handler/idgames;
            
            """);

        UpdateMimeCache();
    }

    public override void RemoveIdGamesProtocol()
    {
        if (File.Exists(DesktopFilePath))
        {
            File.Delete(DesktopFilePath);
            UpdateMimeCache();
        }
    }
    
    private static void UpdateMimeCache()
    {
        try
        {
            using Process proc = Process.Start("update-desktop-database", ApplicationsPath);
            proc.WaitForExit(5000);
            if (proc.ExitCode == 0)
                Console.WriteLine("MIME cache updated.");
            else
                Console.Error.WriteLine("Failed to update MIME cache, exit code: " + proc.ExitCode);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Failed to update MIME cache: " + e.Message);
        }
    }
}
