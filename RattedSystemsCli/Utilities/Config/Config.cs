using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileWatcherEx;
using RattedSystemsCli.Utilities.Github;

namespace RattedSystemsCli.Utilities.Config;

public class ConfigManager
{
    public static ServiceConfig? CurrentServiceConfig = new ServiceConfig();
    
    public static string GetConfigDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".config", "ratted-systems");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;  
    }
    public static string OldTokenPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "ratted.token");
    public static string ServiceConfigPath => Path.Combine(GetConfigDirectory(), "service-config.json");
    public static string TokenPath => Path.Combine(GetConfigDirectory(), "ratted.token");
    public static string LogPath => Path.Combine(GetConfigDirectory(), "ratted.log");
    
    public static event Action<ServiceConfig?>? OnConfigChanged;
    
    public static void LoadServiceConfig()
    {
        var path = ServiceConfigPath;
        if (!File.Exists(path))
        {
            CurrentServiceConfig = new ServiceConfig();
            SaveServiceConfig();
            return;
        }

        var json = File.ReadAllText(path);
        try
        {
            CurrentServiceConfig = JsonSerializer.Deserialize<ServiceConfig>(json);
            CurrentServiceConfig = ConvertConfigPaths(CurrentServiceConfig ?? new ServiceConfig());
            UpdateConfig(CurrentServiceConfig);
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to parse service config: " + ex.Message);
            CurrentServiceConfig = new ServiceConfig();
        }
    }
    
    public static void SaveServiceConfig()
    {
        CurrentServiceConfig = ConvertConfigPaths(CurrentServiceConfig);
        var path = ServiceConfigPath;
        var json = JsonSerializer.Serialize(CurrentServiceConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
    
    public static void UpdateConfig(ServiceConfig outdatedConfig)
    {
        string? oldVersion = outdatedConfig.CliVersionDoNotModify;
        string currentVersion = UpdateChecker.GetCurrentTag();
        if (oldVersion == currentVersion)
        {
            Emi.Info("Service config is up to date. No update needed.");
            return;
        }
        
        Emi.Info($"Updating service config from version {oldVersion} to {currentVersion}...");
        
        var defaultConfig = new ServiceConfig();
        bool updated = false;
        var outdatedConfigType = typeof(ServiceConfig);
        var defaultConfigType = typeof(ServiceConfig);
        
        foreach (var prop in defaultConfigType.GetProperties())
        {
            var outdatedProp = outdatedConfigType.GetProperty(prop.Name);
            if (outdatedProp == null) continue;
            var outdatedValue = outdatedProp.GetValue(outdatedConfig);
            var defaultValue = prop.GetValue(defaultConfig);
            if (outdatedValue == null || (outdatedValue is string str && string.IsNullOrWhiteSpace(str)))
            {
                outdatedProp.SetValue(outdatedConfig, defaultValue);
                updated = true;
                Emi.Info($"Added missing config field '{prop.Name}' with default value.");
            }
        }
        
        outdatedConfig.CliVersionDoNotModify = currentVersion;
        if (updated)   
        {
            CurrentServiceConfig = outdatedConfig;
            SaveServiceConfig();
            Emi.Info("Service config updated successfully.");
        }
        else
        {
            Emi.Info("No updates were necessary for the service config.");
        }
    }

    private static string ConvertPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~")) 
            path = Path.Combine(home, path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (path.Contains("%userprofile%")) 
            path = path.Replace("%userprofile%", home, StringComparison.OrdinalIgnoreCase);
        return path;
    }

    private static ServiceConfig ConvertConfigPaths(ServiceConfig config)
    {
        config.WatchDirectory = ConvertPath(config.WatchDirectory);
        config.TokenFilePath = ConvertPath(config.TokenFilePath);
        return config;
    }

    public static void EditServiceConfig()
    {
        var path = ServiceConfigPath;
        if (!File.Exists(path))
        {
            SaveServiceConfig();
        }
        
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
        
        Emi.Info("Opened config file in default editor: " + path);
    }
    
    private static FileSystemWatcherEx? _configWatcher;

    public static void WatchForConfigChanges()
    {
        
        _configWatcher = new FileSystemWatcherEx(Path.GetDirectoryName(ServiceConfigPath))
        {
            Filter = Path.GetFileName(ServiceConfigPath),
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite
        };
        
        _configWatcher.OnChanged += (sender, @event) =>
        {
            try
            {
                if (!string.Equals(@event.FullPath, ServiceConfigPath, StringComparison.OrdinalIgnoreCase)) return;
                Emi.Info("Service config file changed, reloading...");
                LoadServiceConfig();
                Emi.Info("Service config reloaded successfully.");
                Emi.Debug("New service config: " + JsonSerializer.Serialize(CurrentServiceConfig));
                OnConfigChanged?.Invoke(CurrentServiceConfig);
            } catch (Exception ex)
            {
                Emi.Error("Error reloading service config: " + ex.Message);
            }
        }; 
        
        _configWatcher.Start();
    }
}

public class ServiceConfig
{
    [JsonPropertyName("cli_version_do_not_modify")]
    public string CliVersionDoNotModify { get; set; } = "";
    [JsonPropertyName("token_file_path")]
    public string TokenFilePath { get; set; } = "default";
    [JsonPropertyName("watch_directory")]
    public string WatchDirectory { get; set; } = "";
    [JsonPropertyName("include_subdirectories")]
    public bool IncludeSubdirectories { get; set; } = true;
    [JsonPropertyName("file_filter")]
    public string FileFilter { get; set; } = "*.*";
    [JsonPropertyName("upload_success_sound")]
    public string UploadSuccessSound { get; set; } = "";
    [JsonPropertyName("upload_failure_sound")]
    public string UploadFailureSound { get; set; } = "";
    [JsonPropertyName("clipboard_copy_template")]
    public string ClipboardCopyTemplate { get; set; } = "{url}";
}