using FileWatcherEx;
using OsNotifications;
using RattedSystemsCli.HostAPI;
using RattedSystemsCli.HostAPI.SocketUploader;
using RattedSystemsCli.Utilities.Config;

namespace RattedSystemsCli.Utilities.Services.ServiceRunners;

public static class ServiceRunner
{
    private static readonly IServiceRunner Runner;
    
    static ServiceRunner()
    {
        if (OperatingSystem.IsMacOS()) Runner = new MacServiceRunner();
        else Runner = new DefaultServiceRunner();
    }
    
    public static async Task RunAsService()
    {
        Emi.Info("Using IServiceRunner " + Runner.GetType().Name);
        await Runner.RunAsService();
    }

    public static async Task ManageService(string action)
    {
        await Runner.ManageService(action);
    }
}