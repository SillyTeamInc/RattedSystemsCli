namespace RattedSystemsCli.Utilities.Services.ServiceUtils;

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