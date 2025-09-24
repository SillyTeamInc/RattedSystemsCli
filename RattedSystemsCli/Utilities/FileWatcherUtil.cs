using System.Text.Json.Serialization;

namespace RattedSystemsCli.Utilities;

public struct FileWatcherConfig
{
    public FileWatcherConfig() { }

    [JsonPropertyName("path")]
    public string Path { get; set; } = ".";
    [JsonPropertyName("filter")]
    public string Filter { get; set; } = "*.*";
    [JsonPropertyName("includeSubdirectories")]
    public bool IncludeSubdirectories { get; set; } = false;
}

// TODO: implement this
public class FileWatcherUtil
{
    
}