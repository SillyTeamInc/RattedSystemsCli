using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Github;
using RattedSystemsCli.Utilities.Services;

#pragma warning disable CS0618 // Type or member is obsolete

namespace RattedSystemsCli.Actions;

[Actioner]
public class UpdateActions
{
    // Theoretically should be cross-platform, but only tested on Windows and Linux
    [Action("check-for-updates", ArgRequirement.HasFlag)]
    public void CheckForUpdatesAction(CmdArgValueCollection pargs)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            Emi.Error("Update checking is only supported on Windows and Linux.");
            Emi.Error("For other platforms, you need to compile from source.");
            return;
        }
        string action = pargs.GetValue("check-for-updates") ?? string.Empty;
        bool applyUpdate = action is "apply" or "true";
        Emi.Info("Checking for updates...");
        try
        {
            string currentTag = UpdateChecker.GetCurrentTag();
            GhReleaseInfo? latestRelease = UpdateChecker.FetchLatestReleaseInfoAsync().Result;
            if (latestRelease == null) {
                Emi.Error("Failed to fetch the latest release info.");
                Environment.ExitCode = 1;
                return;
            }
            
            
            var updateInfo = UpdateChecker.IsUpdateAvailableAsync(currentTag, latestRelease).Result;
            if (updateInfo.IsUpdateAvailable)
            {
                Emi.Info("A new version is available: v" + updateInfo.LatestVersion);
                Emi.Info("Release notes: " + updateInfo.ReleaseUrl);
                if (applyUpdate)
                {
                    Emi.Info("Applying update...");
                    UpdateChecker.DownloadAndApplyUpdateAsync(latestRelease).Wait();
                }
                else
                {
                    Emi.Info("Run with --check-for-updates:apply to download and apply the update automatically.");
                    Environment.ExitCode = 0;
                }
            }
            else
            {
                Emi.Info("You are using the latest version.");
                Environment.ExitCode = 0;
            }
        }
        catch (Exception ex)
        {
            Emi.Error("An error occurred while checking for updates: " + ex.Message);
            Environment.ExitCode = 1;
        }
        
        return;
    }
    
    [Action("apply-update", ArgRequirement.HasValue)]
    public void ApplyUpdateAction(CmdArgValueCollection pargs)
    {
        Thread.Sleep(500);
        Console.WriteLine();
        
        string updatePath = pargs.GetValue("apply-update") ?? "";
        string currentPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrWhiteSpace(updatePath) || !File.Exists(updatePath))
        {
            Emi.Error("Invalid update path specified: " + updatePath);
            Environment.ExitCode = 1;
            return;
        }

        Emi.Info("Applying update to: " + updatePath);
        try
        {
            File.Copy(currentPath, updatePath, true);
            Emi.Info("Restarting to finalize update...");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = updatePath,
                Arguments = "--finish-update:" + currentPath,
             });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to apply update: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
    
    [Action("finish-update", ArgRequirement.HasValue)]
    public void FinishUpdateAction(CmdArgValueCollection pargs)
    {
        Thread.Sleep(200);
        
        string updatedPath = pargs.GetValue("finish-update") ?? "";
        if (string.IsNullOrWhiteSpace(updatedPath) || !File.Exists(updatedPath))
        {
            Emi.Error("Invalid updated path specified: " + updatedPath);
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            try
            {
                bool wasRunning = ServiceUtil.IsServiceInstalled() && !ServiceUtil.IsServiceRunning();
                if (wasRunning)
                {
                    ServiceUtil.StartService();
                    Emi.Info("Restarted service!");
                } 
            } catch (PlatformNotSupportedException)
            {
                // Ignore on unsupported platforms
            } catch (Exception ex)
            {
                Emi.Error("Failed to restart service: " + ex);
            }
            
            
            File.Delete(updatedPath);
            Emi.Info($"Successfully updated to version {UpdateChecker.GetCurrentTag()} ({ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit})");
            
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to finalize update: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
    
    
}