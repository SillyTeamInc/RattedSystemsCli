namespace RattedSystemsCli.Utilities.Services.ServiceRunners;

public interface IServiceRunner
{
    Task RunAsService();
    Task ManageService(string action);
}