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
    
    public static void RunAsService()
    {
        Emi.Info("Using IServiceRunner " + Runner.GetType().Name);
        Runner.RunAsService();
    }

    public static void ManageService(string action)
    {
        Runner.ManageService(action);
    }
}