namespace RattedSystemsCli.Utilities.Services;

public class ServiceRunner
{
    public static void RunAsService()
    {
        if (OperatingSystem.IsLinux())
        {
            Emi.Debug("Running as a Linux daemon.");
        }
        else
        {
            Emi.Debug("Service mode is only supported on Linux.");
        }
        
        // todo: implement service logic
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    public static void ManageService(string action)
    {
        try
        {
            switch (action.ToLower())
            {
                case "start":
                    Emi.Info("Starting service...");
                    ServiceUtil.StartService();
                    break;
                case "stop":
                    Emi.Info("Stopping service...");
                    ServiceUtil.StopService();
                    break;
                case "restart":
                    Emi.Info("Restarting service...");
                    ServiceUtil.RestartService();
                    break;
                case "status":
                    Emi.Info("Checking service status...");
                    ServiceUtil.CheckServiceStatus();
                    break;
                case "install":
                    Emi.Info("Installing service...");
                    ServiceUtil.InstallService();
                    break;
                case "uninstall":
                    Emi.Info("Uninstalling service...");
                    ServiceUtil.UninstallService();
                    break;
                default:
                    Emi.Error(
                        $"Unknown action: {action}. Valid actions are start, stop, restart, status, install, uninstall.");
                    break;
            }
        } 
        catch (Exception ex)
        {
            if (ex is EmiException)
            {
                Emi.Error(ex.Message);
                Environment.ExitCode = 1;
                return;
            }
            Emi.Error("An error occurred while managing the service: " + ex);
            Environment.ExitCode = 1;
        }
    }
}