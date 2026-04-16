using RattedSystemsCli.HostAPI;

namespace RattedSystemsCli.Utilities.Services;

public class UserUtil
{
    private static Task<ApiUser?>? _cachedUserTask;
    
    public static async Task<ApiUser?> GetServiceUser()
    {
        _cachedUserTask ??= Api.GetCurrentUserAsync();
        return await _cachedUserTask;
    }
}