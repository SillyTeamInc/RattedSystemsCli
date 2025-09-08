using RattedSystemsCli.Utilities;

namespace RattedSystemsCli.HostAPI;

public class UploadToken
{
    public static string GetTokenPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "ratted.token");
    }
    
    public static string? GetToken()
    {
        var envToken = Environment.GetEnvironmentVariable("RATTED_SYSTEMS_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;
    
        var tokenPath = GetTokenPath();
        if (File.Exists(tokenPath))
            return File.ReadAllText(tokenPath).Trim();
    
        return null;
    }
    
    public static void SetToken(string token)
    {
        var tokenPath = GetTokenPath();
        var configDir = Path.GetDirectoryName(tokenPath)!;
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
        
        Emi.Debug("Writing token to " + tokenPath);
        File.WriteAllText(tokenPath, token);
    }
    
    public static void ClearToken()
    {
        var tokenPath = GetTokenPath();
        if (File.Exists(tokenPath))
            File.Delete(tokenPath);
    }
}