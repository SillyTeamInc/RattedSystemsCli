namespace RattedSystemsCli.Utilities.Services.ServiceUtils;

public class UnsupportedServiceUtil : IServiceUtil
{
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
    
    private void Throw() => throw new PlatformNotSupportedException("The ratted.systems watcher service is only supported on Linux and MacOS.\n" +
                                                                    "Want this to change? Open an issue on the GitHub or join the discord and let me know!");
}