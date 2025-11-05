using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Config;
using RattedSystemsCli.Utilities.Services;
using RattedSystemsCli.Utilities.Services.ServiceRunners;

namespace RattedSystemsCli.Actions;

[Actioner]
public class ServiceActions
{
    
    [Action("service-action", ArgRequirement.HasValue)]
    public void ServiceAction(CmdArgValueCollection pargs)
    {
        string? action = pargs.GetValue("service-action");
        if (string.IsNullOrWhiteSpace(action))
        {
            Emi.Error("No action specified for service-action.");
            return;
        }

        string? token = UploadToken.GetToken();
        
        if (string.IsNullOrWhiteSpace(token))
        {
            Emi.Error("No upload token found. Please set your upload token before managing the service.");
            Emi.Info("Please set an upload token using \"--set-token:UPLOAD_TOKEN\"");
            Emi.Info($"The token is stored at {ConfigManager.TokenPath}.");
            return;
        }

        ServiceRunner.ManageService(action!).ConfigureAwait(false).GetAwaiter().GetResult();
        return;
    }

    // technically you could do this on windows, but it's not really intended.
    // it would completely work, you'd just have to figure out how to run the exe as a service yourself.
    [Action("run-as-service", ArgRequirement.HasFlag)]
    public void RunAsService(CmdArgValueCollection pargs)
    {
        Emi.Info("Running as service/daemon...");
        ServiceRunner.RunAsService().ConfigureAwait(false).GetAwaiter().GetResult();
        return;
    }
}