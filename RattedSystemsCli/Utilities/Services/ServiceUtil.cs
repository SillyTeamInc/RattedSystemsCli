using System.Runtime.InteropServices;

namespace RattedSystemsCli.Utilities.Services;

public static class ServiceUtil
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    
    private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static IServiceUtil _impl;

    static ServiceUtil()
    {
        if (IsLinux)
        {
            _impl = new LinuxServiceUtil();
        }
        else if (IsMacOs)
        {
            _impl = new MacServiceUtil();
        }
        else 
        {
            // honestly considering not implementing Windows support
            // most Windows users would be using ShareX or similar anyway
            // if you see this, please let me know what you think!!
            _impl = new UnsupportedServiceUtil();
        }
    }

    public static bool IsServiceInstalled()
    {
        return _impl.IsServiceInstalled();
    }
    
    public static bool IsServiceRunning()
    {
        return _impl.IsServiceRunning();
    }
    
    public static void InstallService()
    {
        _impl.InstallService();
    }
    
    public static void UninstallService()
    {
        _impl.UninstallService();
    }
    
    public static void StartService()
    {
        _impl.StartService();
    }
    
    public static void StopService()
    {
        _impl.StopService();
    }
    
    public static void RestartService()
    {
        _impl.RestartService();
    }

    public static void CheckServiceStatus()
    {
        if (!IsServiceInstalled())
        {
            Emi.Error("Service is not installed.");
            return;
        }

        _impl.CheckServiceStatus();
    }
}

public interface IServiceUtil
{
    bool IsServiceInstalled();
    bool IsServiceRunning();
    void InstallService();
    void UninstallService();
    void StartService();
    void StopService();
    void RestartService();
    void CheckServiceStatus();
}

public class UnsupportedServiceUtil : IServiceUtil
{
    private void Throw() => throw new PlatformNotSupportedException("The ratted.systems watcher service is only supported on Linux.");

    public bool IsServiceInstalled() => ThrowBool();
    public bool IsServiceRunning() => ThrowBool();
    public void InstallService() => Throw();
    public void UninstallService() => Throw();
    public void StartService() => Throw();
    public void StopService() => Throw();
    public void RestartService() => Throw();
    public void CheckServiceStatus() => Throw();

    private bool ThrowBool()
    {
        Throw();
        return false;
    }
}