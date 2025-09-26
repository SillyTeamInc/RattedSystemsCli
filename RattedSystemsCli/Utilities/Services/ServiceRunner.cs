using FileWatcherEx;
using OsNotifications;
using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Utilities.Config;

namespace RattedSystemsCli.Utilities.Services;

public class ServiceRunner
{
    
    public static void RunAsService()
    {
        if (OperatingSystem.IsLinux())
        {
            Emi.Debug("Running as a Linux daemon.");
        }
        
        Notifications.BundleIdentifier = "RattedSystemsCli";
        Notifications.SetGuiApplication(false); 
        Utils.ShowNotification("ratted.systems", "service started and running in the background.");
        
        ConfigManager.LoadServiceConfig();
        ConfigManager.WatchForConfigChanges();
        
        var config = ConfigManager.CurrentServiceConfig;
        string watchDir = config?.WatchDirectory ?? "";
        string fileFilter = config?.FileFilter ?? "";

        Emi.Debug("Watch directory from config: " + watchDir);
        if (string.IsNullOrWhiteSpace(watchDir) || !Directory.Exists(watchDir))
        {
            Emi.Error("Invalid or missing watch directory in service config. Please configure it using '--service-action:configure.'");
            Environment.ExitCode = 1;
        }

        if (string.IsNullOrWhiteSpace(fileFilter))
        {
            fileFilter = "*.*";
            Emi.Warn("No file type filters specified in service config. Defaulting to '*.*' (all files).");
        }

        var watcher = new FileSystemWatcherEx
        {
            FolderPath = watchDir,
            Filter = fileFilter,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = config?.IncludeSubdirectories ?? false,
        };

        Emi.Info("Watching directory: " + watchDir);
        
        watcher.OnChanged += async (sender, @event) =>
        {
            Task.Run(async () =>
            {
                Emi.Info($"File {@event.ChangeType}: {@event.FullPath}");
                try
                {
                    var fileInfo = new FileInfo(@event.FullPath);
                    if (fileInfo.Length > 50 * 1024 * 1024)
                    {
                        Utils.ShowNotification("ratted.systems", "uploading large file, this may take a bit!");
                    }
                    
                    var reply = await Api.UploadFileAsync(@event.FullPath);

                    if (reply.Success)
                    {
                        
                        Emi.Info("File uploaded successfully!");
                        Utils.SetClipboardText(reply.Resource ?? "");
                        Utils.ShowNotification("ratted.systems", "copied upload url to clipboard!");
                        Emi.Info("File URL: " + reply.Resource + " (copied to clipboard)");
                        // TODO: Delay each upload by a second
                    }
                    else
                    {
                        Emi.Error("File upload failed: " + reply.Message);
                        Utils.ShowNotification("ratted.systems", "file upload failed: " + reply.Message);
                    }
                }
                catch (Exception ex)
                {
                    Utils.ShowNotification("ratted.systems", "file upload failed: " + ex.Message);
                    Emi.Error("An error occurred during file upload: " + ex);
                }
            });
        };

        watcher.Start();
        
        ConfigManager.OnConfigChanged += (newConfig) =>
        {
            Emi.Info("Service config changed, updating watcher...");
            if (newConfig == null) return;
            
            if (string.IsNullOrWhiteSpace(newConfig.WatchDirectory) || !Directory.Exists(newConfig.WatchDirectory))
            {
                Emi.Error("Invalid or missing watch directory in updated service config. Watcher not updated.");
                return;
            }

            watcher.Stop();
            watcher.FolderPath = newConfig?.WatchDirectory ?? "";
            watcher.Filter = string.IsNullOrWhiteSpace(newConfig?.FileFilter) ? "*.*" : newConfig.FileFilter;
            watcher.IncludeSubdirectories = newConfig?.IncludeSubdirectories ?? false;
            watcher.Start();
        };

        // Keep the service running
        Task.Delay(-1).Wait();
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
                    Console.WriteLine();
                    Emi.Warn("Keep in mind that this uses the current executable path as the service target.");
                    Emi.Warn(
                        "If you move or delete this executable, the service will break, and you will need to reinstall it.");
                    string currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    Emi.Info("(Current executable path: " + currentExePath + ")");
                    break;
                case "uninstall":
                    Emi.Info("Uninstalling service...");
                    ServiceUtil.UninstallService();
                    break;
                case "configure":
                case "config":
                    ConfigManager.EditServiceConfig();
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