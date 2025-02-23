using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DoomCli;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IdGamesEntryResponse))]
internal partial class IdGamesSourceGenerationContext : JsonSerializerContext;

public record IdGamesEntryResponse(IdGamesEntry Content);
public record IdGamesEntry(int Id, string Title, string Dir, string Filename, long Size);
public record DownloadProgress(long TotalBytes, long BytesReceived);

public class IdGamesClient : IDisposable
{
    private readonly HttpClient api = new() { BaseAddress = new("https://www.doomworld.com/idgames/api/api.php") };
    private const string DownloadsBaseUrl = "https://www.quaddicted.com/files/idgames/";
    
    public IdGamesEntry GetEntry(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || !int.TryParse(u.Host, out int id))
            throw new InvalidOperationException($"Invalid idgames URI: {uri}");

        return api.GetFromJsonAsync($"?action=get&out=json&id={id}",
            IdGamesSourceGenerationContext.Default.IdGamesEntryResponse).Result!.Content;
    }

    public static void Download(IdGamesEntry entry, string destination, Action<DownloadProgress> progressCallback)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var ctSrc = new CancellationTokenSource();
        using var dlClient = new WebClient();
        
        dlClient.DownloadFileCompleted += (_, args) =>
        {
            if (args.Error != null)
                throw new ApplicationException("Download failed: " + args.Error.Message, args.Error);
            if (args.Cancelled)
                throw new ApplicationException("Download failed");
            ctSrc.Cancel();
        };
        dlClient.DownloadProgressChanged += (_, args) =>
            progressCallback(new DownloadProgress(args.TotalBytesToReceive, args.BytesReceived));

        dlClient.DownloadFileAsync(new Uri($"{DownloadsBaseUrl}{entry.Dir}/{entry.Filename}"),
            destination);
        
        ctSrc.Token.WaitHandle.WaitOne();
    }

    public void Dispose()
    {
        api.Dispose();
    }
}