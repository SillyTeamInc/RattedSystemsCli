using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Services;

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

        ServiceRunner.ManageService(action!);
        return;
    }

    // technically you could do this on windows, but it's not really intended...
    [Action("run-as-service", ArgRequirement.HasFlag)]
    public void RunAsService(CmdArgValueCollection pargs)
    {
        Emi.Info("Running as service/daemon...");
        ServiceRunner.RunAsService();
        return;
    }
}