using System.Diagnostics;
using System.Xml.Linq;

namespace RattedSystemsCli.Utilities.Services.ServiceUtils;

public class MacServiceUtil : IServiceUtil
{
    private const string ServiceName = "com.rattedsystems.watcher";
    private static readonly string PlistFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{ServiceName}.plist");

    private (string StdOut, string StdErr) RunLaunchCtl(string args, bool throwOnError = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "launchctl",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        process!.WaitForExit(30000);

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (throwOnError && process.ExitCode != 0)
            throw new EmiException($"launchctl {args} failed: {error}");

        return (output.Trim(), error.Trim());
    }

    public void ReloadDaemon()
    {
        Emi.Info("No daemon reload required");
    }

    public void CheckServiceStatus()
    {
        var (outp, err) = RunLaunchCtl($"list | grep {ServiceName}");
        if (!string.IsNullOrWhiteSpace(outp)) Emi.Info(outp);
        if (!string.IsNullOrWhiteSpace(err)) Emi.Error(err);
    }

    public bool IsServiceInstalled()
    {
        return File.Exists(PlistFilePath);
    }

    public bool IsServiceRunning()
    {
        var (outp, _) = RunLaunchCtl($"list | grep {ServiceName}");
        return outp.Contains(ServiceName);
    }

    public void InstallService()
    {
        if (IsServiceInstalled())
            throw new EmiException("Service is already installed.");

        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? throw new EmiException("Could not determine the executable path.");

        var envDict = new Dictionary<string, string>();
        string[] keys = { "DISPLAY", "XAUTHORITY", "WAYLAND_DISPLAY", "DBUS_SESSION_BUS_ADDRESS" };
        foreach (var key in keys)
        {
            var val = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(val))
                envDict[key] = val;
        }

        Emi.Info("Using the following environment variables for the service:");
        foreach (var kv in envDict)
            Emi.Info($"  {kv.Key}={kv.Value}");

        var plist = new XDocument(
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "Label"), new XElement("string", ServiceName),
                    new XElement("key", "ProgramArguments"),
                    new XElement("array",
                        new XElement("string", exePath),
                        new XElement("string", "--run-as-service")
                    ),
                    new XElement("key", "RunAtLoad"), new XElement("true"),
                    new XElement("key", "KeepAlive"), new XElement("true"),
                    new XElement("key", "StandardOutPath"), new XElement("string", $"{Path.GetTempPath()}{ServiceName}.out.log"),
                    new XElement("key", "StandardErrorPath"), new XElement("string", $"{Path.GetTempPath()}{ServiceName}.err.log"),
                    new XElement("key", "EnvironmentVariables"),
                    new XElement("dict",
                        envDict.Select(kv => new object[]
                        {
                            new XElement("key", kv.Key),
                            new XElement("string", kv.Value)
                        }).SelectMany(x => x).ToArray()
                    )
                )
            )
        );

        Directory.CreateDirectory(Path.GetDirectoryName(PlistFilePath)!);
        plist.Save(PlistFilePath);

        RunLaunchCtl($"load {PlistFilePath}", throwOnError: true);
        Emi.Info("Service installed and loaded successfully.");
        Emi.Info("Keep in mind that this service will always run while installed.");
        Emi.Info("To uninstall it, use the 'uninstall' service-action.");
    }

    public void UninstallService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        StopService();

        RunLaunchCtl($"unload {PlistFilePath}", throwOnError: false);
        if (File.Exists(PlistFilePath))
            File.Delete(PlistFilePath);

        Emi.Info("Service uninstalled successfully.");
    }

    public void StartService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        if (IsServiceRunning())
            throw new EmiException("Service is already running.");
        
        
        SetAutoRestart(true);
        RunLaunchCtl($"start {ServiceName}", throwOnError: false);
    }

    public void StopService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        
        SetAutoRestart(false);
        RunLaunchCtl($"stop {ServiceName}", throwOnError: false);
    }

    public void RestartService()
    {
        if (!IsServiceInstalled())
            throw new EmiException("Service is not installed.");

        StopService();
        StartService();
    }
    
    private void SetAutoRestart(bool enable)
    {
        var plist = XDocument.Load(PlistFilePath);
        var keepAliveElement = plist.Root?.Element("dict")?
            .Elements("key")
            .FirstOrDefault(e => e.Value == "KeepAlive")?
            .NextNode as XElement;

        if (keepAliveElement != null)
        {
            keepAliveElement.ReplaceWith(new XElement(enable ? "true" : "false"));
            plist.Save(PlistFilePath);
            RunLaunchCtl($"unload {PlistFilePath}", throwOnError: false);
            RunLaunchCtl($"load {PlistFilePath}", throwOnError: false);
        }
    }
}
