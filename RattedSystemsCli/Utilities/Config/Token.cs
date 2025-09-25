namespace RattedSystemsCli.Utilities.Config;

public class UploadToken
{
    public static string GetTokenPath() => ConfigManager.TokenPath;
    
    public static string? GetToken()
    {
        if (File.Exists(ConfigManager.OldTokenPath) && !File.Exists(GetTokenPath()))
        {
            Emi.Warn("Old token file found at " + ConfigManager.OldTokenPath + ". Migrating to new location.");
            try
            {
                var oldToken = File.ReadAllText(ConfigManager.OldTokenPath).Trim();
                SetToken(oldToken);
                File.Delete(ConfigManager.OldTokenPath);
                Emi.Info("Token migrated to " + GetTokenPath());
            }
            catch (Exception ex)
            {
                Emi.Error("Failed to migrate old token: " + ex.Message);
            }
        }
        
        var envToken = Environment.GetEnvironmentVariable("RATTED_SYSTEMS_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;
    
        var tokenPath = GetTokenPath();
        return File.Exists(tokenPath) ? File.ReadAllText(tokenPath).Trim() : null;
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