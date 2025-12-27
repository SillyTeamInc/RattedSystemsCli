using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using OsNotifications;
using RattedSystemsCli.Utilities.Config;
using RattedSystemsCli.Utilities.Github;
using TextCopy;


namespace RattedSystemsCli.Utilities;

public enum HttpResponseState
{
    Created,
    ComputedHeaders,
    SentHeaders,
    Closed,
}

public static class Utils
{
    public static bool IsNullOrEmpty(this string? str)
    {
        return string.IsNullOrEmpty(str);
    }

    public static string GatherStacktrace(bool includeColor = false)
    {
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();

        var sb = new StringBuilder();
        bool first = true;
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method == null) continue;
            // Skip our current method
            if (first)
            {
                first = false;
                continue;
            }

            var args = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            sb.AppendLine($"  at {method.DeclaringType?.FullName}.{method.Name}({args}) ");
        }

        return sb.ToString();
    }

    public static StackFrame GetCallerFrame(int skipFrames = 2)
    {
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();
        var frame = frames[skipFrames];
        return frame;
    }

    public static void SetClipboardTemplated(string template, string url)
    {
        // currently only doing this because nothing else is templated
        string text = template.Replace("{url}", url);
        SetClipboardText(text);
    }

    public static void SetClipboardText(string text)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                string? display = Environment.GetEnvironmentVariable("DISPLAY");
                string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

                ProcessStartInfo psi;

                if (!string.IsNullOrEmpty(wayland))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "sh",
                        Arguments = $"-c \"printf '%s' '{text.Replace("'", "'\\''")}' | wl-copy\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }
                else if (!string.IsNullOrEmpty(display))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "sh",
                        Arguments = $"-c \"printf '%s' '{text.Replace("'", "'\\''")}' | xsel -i --clipboard\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Environment =
                        {
                            ["DISPLAY"] = display,
                            ["XAUTHORITY"] = Environment.GetEnvironmentVariable("XAUTHORITY") ??
                                             $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.Xauthority"
                        }
                    };
                }
                else
                {
                    Emi.Warn("No DISPLAY or WAYLAND_DISPLAY found!");
                    psi = new ProcessStartInfo
                    {
                        FileName = "sh",
                        Arguments = $"-c \"printf '%s' '{text.Replace("'", "'\\''")}' | xclip -selection clipboard\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }
                using var process = Process.Start(psi);
                process!.WaitForExit(5000);


                return;
            }

            // fallback just in case
            ClipboardService.SetTextAsync(text).Wait();
        }
        catch (Exception ex)
        {
            Emi.Error($"Failed to set clipboard text: {ex}");
            ShowNotification("ratted.systems", "Failed to set clipboard text: " + ex.Message);
        }
    }

    public static void ShowNotification(string title, string message)
    {
        try
        {
            // TODO: Probably make my own notification library;
            //       OsNotifications isn't really good...
            //       On MacOS, only showing the first notification works
            //       then after the first notification, it hangs and uses
            //       100% of the thread it's running on.
            if (OperatingSystem.IsLinux())
            { 
                Emi.Debug("Showing notification: " + title + " - " + message);
                Process.Start("notify-send", $"--app-name \"ratted.systems\" \"{title}\" \"{message}\"").WaitForExit();
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("osascript", $"-e 'display notification \"{message.Replace("\"", "\\\"")}\" with title \"{title.Replace("\"", "\\\"")}\"").WaitForExit();
            } 
            else
            { 
                // the windows implementation seems to work fine, but who knows
                // maybe it breaks in the future knowing my luck and the state of Windows.
                Notifications.ShowNotification(title, message);
            }
            
        }
        catch (PlatformNotSupportedException)
        {
            
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to show notification: " + ex);
        }
    }


    public static void PlaySound(string file)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = $"\"{file}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                })!.WaitForExit();
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ffplay",
                    Arguments = $"-nodisp -autoexit \"{file}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                })!.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to play sound: " + ex);
        }
    }

    public static string RelativeTimeSpan(TimeSpan relative)
    {
        double value;
        string unit;

        if ((value = Math.Round(relative.TotalSeconds, 2)) < 60)
            unit = "second";
        else if ((value = Math.Round(relative.TotalMinutes, 2)) < 60)
            unit = "minute";
        else if ((value = Math.Round(relative.TotalHours, 2)) < 24)
            unit = "hour";
        else if ((value = Math.Round(relative.TotalDays, 2)) < 30)
            unit = "day";
        else
        {
            value = Math.Round(relative.TotalDays / 30, 2);
            unit = "month";
        }

        return $"{value} {unit}{(Math.Abs(value - 1) < 0.001 ? "" : "s")} ago";
    }

    public static string GetUserAgent()
    {
        if (OperatingSystem.IsMacOS())
        {
            string osMac = "MacOSX/" + Environment.OSVersion.Version;
            string versionMac = UpdateChecker.GetCurrentTag()[1..];
            return $"RattedSystemsCli/{versionMac} (+https://ratted.systems/) {osMac}";
        }
        
        string os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();
        string version = UpdateChecker.GetCurrentTag()[1..];
        return $"RattedSystemsCli/{version} (+https://ratted.systems/) {os}";
    }

    public static void AddUserAgentHeader(this HttpWebRequest request)
    {
        request.UserAgent = GetUserAgent();
    }

    public static void AddUserAgentHeader(this HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.ToString() != GetUserAgent())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(GetUserAgent());
        }
    }

    public static void PlayUploadSuccess()
    {
        string successFile = ConfigManager.CurrentServiceConfig?.UploadSuccessSound ?? "";
        if (!string.IsNullOrWhiteSpace(successFile) && File.Exists(successFile))
        {
            PlaySound(successFile);
        }
    }

    public static void PlayUploadFailure()
    {
        string failureFile = ConfigManager.CurrentServiceConfig?.UploadFailureSound ?? "";
        if (!string.IsNullOrWhiteSpace(failureFile) && File.Exists(failureFile))
        {
            PlaySound(failureFile);
        }
    }
}