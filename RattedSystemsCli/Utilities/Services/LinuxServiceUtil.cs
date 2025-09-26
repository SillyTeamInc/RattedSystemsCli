using System.Diagnostics;

namespace RattedSystemsCli.Utilities.Services;

public class LinuxServiceUtil : IServiceUtil
{
    private const string ServiceName = "ratted-systems-watcher.service";
    private static readonly string ServiceFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "systemd", "user", ServiceName);

    private (string StdOut, string StdErr) RunSystemctl(string args, bool throwOnError = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = "--user " + args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        process!.WaitForExit(30000); // 30 seconds timeout

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        
        if (output.EndsWith(Environment.NewLine))
            output = output[..^Environment.NewLine.Length];
        if (error.EndsWith(Environment.NewLine))
            error = error[..^Environment.NewLine.Length];

        if (throwOnError && !string.IsNullOrWhiteSpace(error))
        {
            throw new EmiException($"systemctl {args} failed: {error}");
        }

        return (output, error);
    }

    public void ReloadDaemon()
    {
        RunSystemctl("daemon-reload", throwOnError: true);
        Emi.Info("systemd daemon reloaded successfully.");
    }

    public void CheckServiceStatus()
    {
        var (outp, err) = RunSystemctl($"status {ServiceName} --no-pager");
        if (!string.IsNullOrWhiteSpace(outp)) Emi.Info(outp);
        if (!string.IsNullOrWhiteSpace(err)) Emi.Error(err);
    }

    public bool IsServiceInstalled()
    {
        var (outp, err) = RunSystemctl($"status {ServiceName}");
        if (err.Contains("could not be found", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("not-found", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return File.Exists(ServiceFilePath);
    }

    public bool IsServiceRunning()
    {
        var (outp, _) = RunSystemctl($"is-active {ServiceName}");
        return outp.Trim() == "active";
    }

    public void InstallService()
    {
        if (IsServiceInstalled())
            throw new EmiException("Service is already installed.");

        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new EmiException("Could not determine the executable path.");

        // Collect environment variables for graphical session
        var envLines = new List<string>();

        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrWhiteSpace(display))
            envLines.Add($"Environment=DISPLAY={display}");

        string? xauth = Environment.GetEnvironmentVariable("XAUTHORITY");
        if (!string.IsNullOrWhiteSpace(xauth) && File.Exists(xauth))
            envLines.Add($"Environment=XAUTHORITY={xauth}");

        string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (!string.IsNullOrWhiteSpace(wayland))
            envLines.Add($"Environment=WAYLAND_DISPLAY={wayland}");

        string? dbus = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        if (!string.IsNullOrWhiteSpace(dbus))
            envLines.Add($"Environment=DBUS_SESSION_BUS_ADDRESS={dbus}");

        Emi.Info("Using the following environment variables for the service:");
        foreach (var line in envLines)
        {
            Emi.Info("  " + line.Replace("Environment=", ""));
        }
        
        // Build the service file dynamically
        var serviceFileContent = $@"[Unit]
Description=Ratted Systems Watcher Service
After=graphical.target

[Service]
Type=simple
ExecStart={exePath} --run-as-service
Restart=on-failure
{string.Join(Environment.NewLine, envLines)}

[Install]
WantedBy=default.target
";

        Directory.CreateDirectory(Path.GetDirectoryName(ServiceFilePath)!);
        File.WriteAllText(ServiceFilePath, serviceFileContent);

        ReloadDaemon();
        var (outp, err) = RunSystemctl($"enable {ServiceName}");
        if (!string.IsNullOrWhiteSpace(outp)) Emi.Info(outp);
        if (!string.IsNullOrWhiteSpace(err)) Emi.Error(err);

        ReloadDaemon();
        Emi.Info("Service installed successfully. Use the start action to start it.");
    }


    public void UninstallService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        StopService();

        var (outp, err) = RunSystemctl($"disable {ServiceName}");
        if (!string.IsNullOrWhiteSpace(outp)) Emi.Info(outp);
        if (!string.IsNullOrWhiteSpace(err)) Emi.Error(err);

        if (File.Exists(ServiceFilePath))
            File.Delete(ServiceFilePath);

        ReloadDaemon();
        RunSystemctl($"reset-failed {ServiceName}");
        Emi.Info("Service uninstalled successfully.");
    }

    public void StartService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        RunSystemctl($"start {ServiceName}", throwOnError: false);
    }

    public void StopService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        RunSystemctl($"stop {ServiceName}", throwOnError: false);
    }

    public void RestartService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        RunSystemctl($"restart {ServiceName}", throwOnError: true);
    }
}
