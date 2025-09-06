using System.Text.Json.Serialization;

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
        HttpClient.Timeout = TimeSpan.FromSeconds(30);
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RattedSystemsCli/1.0");
        HttpClient.DefaultRequestHeaders.Add("Authorization", UploadToken.GetToken() ?? "");
    }
    
    
    // just file uploading for now 
    public static async Task<ApiReply> UploadFileAsync(string filePath, string? token = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);
        
        if (string.IsNullOrWhiteSpace(token))
            token = UploadToken.GetToken();
        
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("No upload token provided or configured");
        
        using var content = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await HttpClient.PostAsync(UploadEndpoint, content);
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var apiReply = System.Text.Json.JsonSerializer.Deserialize<ApiReply>(responseBody);
        if (apiReply == null)
            throw new InvalidOperationException("Failed to parse API response");
        return apiReply;
    }
}