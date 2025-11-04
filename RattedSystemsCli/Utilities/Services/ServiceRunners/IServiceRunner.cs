namespace RattedSystemsCli.Utilities.Services.ServiceRunners;

public interface IServiceRunner
{
    void RunAsService();
    void ManageService(string action);
}