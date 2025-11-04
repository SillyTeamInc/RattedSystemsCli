using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RattedSystemsCli.Utilities.Services;

#pragma warning disable CS0618 // Type or member is obsolete

namespace RattedSystemsCli.Utilities.Github;

public class UpdateChecker
{
    public static string GetRepositoryUrl()
    {
        return ThisAssembly.Git.RepositoryUrl.Replace(".git", "");
    }

    public static string GetRepository()
    {
        string repoUrl = GetRepositoryUrl();
        if (repoUrl.EndsWith("/")) repoUrl = repoUrl[..^1];
        int lastSlash = repoUrl.LastIndexOf('/');
        int secondLastSlash = repoUrl.LastIndexOf('/', lastSlash - 1);
        if (secondLastSlash == -1 || lastSlash == -1 || lastSlash <= secondLastSlash)
            throw new Exception("Invalid repository URL");
        return repoUrl[(secondLastSlash + 1)..];
    }

    public static string GetCurrentTag()
    {
        return string.IsNullOrEmpty(ThisAssembly.Git.BaseTag) ? ThisAssembly.Git.Branch : ThisAssembly.Git.BaseTag;
    }
    
    public static async Task<GhReleaseInfo?> FetchLatestReleaseInfoAsync()
    {
        string url = $"https://api.github.com/repos/{GetRepository()}/releases/latest";

        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RattedSystemsCli");

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch latest release info from GitHub. " +
                                $"Status code: {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GhReleaseInfo>(responseBody);
    }

    public static async Task<UpdateInfo> IsUpdateAvailableAsync(string currentTag, GhReleaseInfo releaseInfo)
    {
        // Remove 'v' prefix if present
        if (currentTag.StartsWith('v')) currentTag = currentTag[1..];
        string latestTag = releaseInfo.TagName;
        if (latestTag.StartsWith('v')) latestTag = latestTag[1..];

        // Parse versions
        if (!Version.TryParse(currentTag, out var currentVersion))
            throw new Exception("Invalid current version format: " + currentTag);
        if (!Version.TryParse(latestTag, out var latestVersion))
            throw new Exception("Invalid latest version format: " + latestTag);

        bool isUpdateAvailable = latestVersion > currentVersion;

        string releaseUrl = releaseInfo.HtmlUrl;
        string releaseNotes = releaseInfo.Body;
        return new UpdateInfo
        {
            IsUpdateAvailable = isUpdateAvailable,
            CurrentVersion = currentVersion.ToString(),
            LatestVersion = latestVersion.ToString(),
            ReleaseUrl = releaseUrl,
            ReleaseNotes = releaseNotes
        };
    }
    
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    public static async Task DownloadAndApplyUpdateAsync(GhReleaseInfo updateInfo)
    {
        string executablePath = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName ?? "RattedSystemsCli");
        string currentFullPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
        
        try
        {
            bool running = ServiceUtil.IsServiceInstalled() && ServiceUtil.IsServiceRunning();
            if (running)
            {
                ServiceUtil.StopService();
                Emi.Info("Stopped running service to apply update.");
            } 
        } catch (PlatformNotSupportedException)
        {
            // Ignore on unsupported platforms
        } catch (Exception ex)
        {
            Emi.Error("Failed to stop service: " + ex);
            Environment.Exit(1);
        }

        string assetName = "";
        if (OperatingSystem.IsLinux()) assetName = "RattedSystemsCli";
        else if (OperatingSystem.IsMacOS()) assetName = "RattedSystemsCli.macos";
        else if (OperatingSystem.IsWindows()) assetName = "RattedSystemsCli.exe"; 
        
        var asset = updateInfo.Assets.FirstOrDefault(a => a.Name.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            throw new Exception("No suitable asset found for the current platform.");
        }
        string downloadUrl = asset.BrowserDownloadUrl;
        
        string tempFilePath = Path.Combine(Path.GetTempPath(), executablePath + ".tmp");

        Downloader downloader = new Downloader(downloadUrl, tempFilePath, true);
        int coreCount = Environment.ProcessorCount;
        Emi.Info($"Downloading update using {coreCount} threads...");
        await downloader.DownloadFileMultithreaded(coreCount); 
        

        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                await Process.Start("chmod", $"+x \"{tempFilePath}\"").WaitForExitAsync();
            }
            Emi.Info("Restarting to apply update...");
            Process.Start(tempFilePath, "--apply-update:\"" + currentFullPath + "\"");
            Environment.Exit(0);
        } catch (Exception ex)
        {
            Emi.Error("Failed to apply update: " + ex);
            Environment.Exit(1);
        }
    }
}

public class GhReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";
    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
    [JsonPropertyName("assets")]
    public List<GhReleaseAsset> Assets { get; set; } = new();
}

public class GhReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}