using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmniProgress.Backends;
using EmniProgress.Backends.KDE;
using EmniProgress.Core;
using EmniProgress.Factory;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Config;

namespace RattedSystemsCli.HostAPI;

public class ApiReply
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("resource")] public string? Resource { get; set; }
    [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
}

public class ApiGetUserReply
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("user")] public ApiUser? User { get; set; }
}

public class ApiUser
{
    [JsonPropertyName("IsBot")] public bool IsBot { get; set; }
    [JsonPropertyName("Username")] public string? Username { get; set; }
    [JsonPropertyName("DiscriminatorValue")] public int DiscriminatorValue { get; set; }
    [JsonPropertyName("AvatarId")] public string? AvatarId { get; set; }
    [JsonPropertyName("AvatarUrl")] public string? AvatarUrl { get; set; }
    [JsonPropertyName("BannerId")] public string? BannerId { get; set; }
    [JsonPropertyName("BannerColor")] public ApiColor? BannerColor { get; set; }
    [JsonPropertyName("AccentColor")] public ApiColor? AccentColor { get; set; }
    [JsonPropertyName("PublicFlags")] public string? PublicFlags { get; set; }
    [JsonPropertyName("GlobalName")] public string? GlobalName { get; set; }
    [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("Discriminator")] public string? Discriminator { get; set; }
    [JsonPropertyName("AvatarDecorationHash")] public string? AvatarDecorationHash { get; set; }
    [JsonPropertyName("AvatarDecorationSkuId")] public ulong? AvatarDecorationSkuId { get; set; }
    [JsonPropertyName("Id")] public ulong Id { get; set; }
    [JsonPropertyName("FormattedUsername")] public string? FormattedUsername { get; set; }
    [JsonPropertyName("UniqueId")] public string? UniqueId { get; set; }
    [JsonPropertyName("StorageUsed")] public string? StorageUsed { get; set; }
}

public class ApiColor
{
    [JsonPropertyName("RawValue")] public uint RawValue { get; set; }
    [JsonPropertyName("R")] public byte R { get; set; }
    [JsonPropertyName("G")] public byte G { get; set; }
    [JsonPropertyName("B")] public byte B { get; set; }
}

public sealed class ProgressStreamContent : HttpContent
{
    private readonly Stream _source;
    private readonly IProgressBackend _progress;
    private readonly long _length;
    private readonly string _fileName;
    private readonly string _destinationUrl;

    public ProgressStreamContent(Stream source, IProgressBackend progress, long length, string fileName, string destinationUrl)
    {
        _source = source;
        _progress = progress;
        _length = length;
        _fileName = fileName;
        _destinationUrl = destinationUrl;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
    {
        var buffer = new byte[81920];
        long sent = 0;
        int read;
        int lastPercent = 0;
        var stopwatch = Stopwatch.StartNew();

        var kde =  (_progress as CompositeProgressBackend)?.GetBackend<KdeProgressBackend>();
        if (kde != null)
        {
            await kde.SetDestUrlAsync("https://ratted.systems");
            await kde.UpdateAmountAsync((ulong)_length, 0, KdeJobUnit.Bytes);
            await kde.UpdateDescriptionFieldAsync(1, "Filename", _fileName);
        }
    
        while ((read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read));
            sent += read;
    
            var percent = _length <= 0 ? 100f : sent * 100f / _length;
            if (percent - lastPercent < 1 && sent != _length)
                continue;
    
            if (kde != null)
            {
                //var uploadSpeedBps = sent / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                ulong uploadSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? (ulong)(sent / stopwatch.Elapsed.TotalSeconds) : 0;
                await kde.UpdateSpeedAsync(uploadSpeedBytesPerSecond);
                await kde.UpdateAmountAsync((ulong)_length, (ulong)sent, KdeJobUnit.Bytes);
            }
    
            if ((int)percent < 100)
                await _progress.UpdateAsync(percent, "Uploading");
    
            lastPercent = (int)percent;
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _length;
        return true;
    }
}

public class Api
{
    public static string BaseUrl = "https://ratted.systems/";
    public static string UploadEndpoint = "upload/new"; // POST

    public static HttpClient HttpClient;

    static Api()
    {
        if (Debugger.IsAttached)
        {
            BaseUrl = "http://localhost:8080/";
        }
        HttpClient = new HttpClient();
        HttpClient.BaseAddress = new Uri(BaseUrl);
        HttpClient.Timeout = Timeout.InfiniteTimeSpan;
        string os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();
        HttpClient.AddUserAgentHeader();
    }

    private static void AddAuthorizationHeader(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Please provide a valid token", nameof(token));

        if (HttpClient.DefaultRequestHeaders.Contains("Authorization"))
            HttpClient.DefaultRequestHeaders.Remove("Authorization");

        HttpClient.DefaultRequestHeaders.Add("Authorization", token);
    }

    public static async Task<ApiReply> UploadFileAsync(string filePath, string? token = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        if (string.IsNullOrWhiteSpace(token))
            token = UploadToken.GetToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No upload token provided or configured");

        using var content = new MultipartFormDataContent();
        await using var progress = (CompositeProgressBackend)EmniFactory.Create();
        
        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new ProgressStreamContent(
            fileStream,
            progress,
            fileStream.Length,
            Path.GetFileName(filePath),
            new Uri(HttpClient.BaseAddress!, UploadEndpoint).ToString());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        AddAuthorizationHeader(token);

        await progress.StartAsync("Uploading", Path.GetFileName(filePath), "RattedSystemsCli", "document-send");
        var response = await HttpClient.PostAsync(UploadEndpoint, content);
        var kde = progress.GetBackend<KdeProgressBackend>();
        if (kde != null)
        {
            string qualifiedFilePath = Path.GetFullPath(filePath).Replace("\\", "/");
            await kde.SetDestUrlAsync(qualifiedFilePath);
            await kde.UpdateSpeedAsync(0);
            await kde.UpdateAmountAsync((ulong)fileStream.Length, (ulong)fileStream.Length, KdeJobUnit.Bytes);
        }

        await progress.UpdateAsync(100, "Uploaded!");
        await progress.FinishAsync(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Upload successful!" : "Upload failed: " + response.ReasonPhrase);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var apiReply = JsonSerializer.Deserialize<ApiReply>(responseBody);

        return apiReply ?? throw new InvalidOperationException("Failed to parse API response");
    }

    public static async Task<ApiUser?> GetCurrentUserAsync(string? token = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            token = UploadToken.GetToken();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No upload token provided or configured");

        AddAuthorizationHeader(token);

        var response = await HttpClient.GetAsync("api/v1/discord/get-user");
        var responseBody = await response.Content.ReadAsStringAsync();
        var apiReply = JsonSerializer.Deserialize<ApiGetUserReply>(responseBody);

        if (apiReply == null)
            throw new InvalidOperationException("Failed to parse API response");

        if (!apiReply.Success)
            throw new InvalidOperationException("API error: " + apiReply.Message);

        return apiReply.User;
    }
}