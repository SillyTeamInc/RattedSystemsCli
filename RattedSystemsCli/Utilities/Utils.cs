using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using OsNotifications;
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

    public static async Task<HttpResponseState> GetResponseStateAsync(this HttpListenerResponse response)
    {
#if WINDOWS
        FieldInfo? field =
 typeof(HttpListenerResponse).GetField("_responseState", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new InvalidOperationException("Could not find the _responseState field in HttpListenerResponse.");
        }

        // Get the value of the _responseState field
        object? responseState = field.GetValue(response);
        if (responseState == null)
        {
            throw new InvalidOperationException("The _responseState field in HttpListenerResponse is null.");
        }
        
        // Cast the value to HttpResponseState
        return (HttpResponseState)responseState;
#else

        // use a different method to get the response state
        var state = HttpResponseState.Created;
        if (response.KeepAlive) state = HttpResponseState.ComputedHeaders;
        else state = HttpResponseState.Closed;

        return state;
#endif
    }

    public static StackFrame GetCallerFrame(int skipFrames = 2)
    {
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();
        var frame = frames[skipFrames];
        return frame;
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
                        Arguments = $"-c \"echo '{text.Replace("'", "'\\''")}' | wl-copy\"",
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
                        Arguments = $"-c \"echo '{text.Replace("'", "'\\''")}' | xsel -i --clipboard\"",
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
                        Arguments = $"-c \"echo '{text.Replace("'", "'\\''")}' | xclip -selection clipboard\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }

                using var process = Process.Start(psi);
                process!.WaitForExit(5000);
                
                string error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowNotification("ratted.systems", "Clipboard tool error: " + error);
                    Emi.Warn("Clipboard tool error: " + error);
                }

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
            if (OperatingSystem.IsLinux())
            { 
                Process.Start("notify-send", $"--app-name \"ratted.systems\" \"{title}\" \"{message}\"").WaitForExit();
            }
            else Notifications.ShowNotification(title, message);
            
        }
        catch (PlatformNotSupportedException)
        {
            // Ignore on unsupported platforms
        }
        catch (Exception ex)
        {
            Emi.Error("Failed to show notification: " + ex);
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
}