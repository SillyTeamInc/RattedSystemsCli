using System.Text.Json;
using System.Text.Json.Serialization;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Config;

namespace RattedSystemsCli.HostAPI;

public class ApiReply
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("resource")]
    public string? Resource { get; set; }
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }
}

public class Api
{
    public static string BaseUrl = "https://ratted.systems/";
    public static string UploadEndpoint = "upload/new"; // POST

    public static HttpClient HttpClient;
    static Api()
    {
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
        await using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        AddAuthorizationHeader(UploadToken.GetToken() ?? throw new InvalidOperationException("No upload token configured"));
        var response = await HttpClient.PostAsync(UploadEndpoint, content);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var apiReply = JsonSerializer.Deserialize<ApiReply>(responseBody);
        
        return apiReply ?? throw new InvalidOperationException("Failed to parse API response");
    }
}