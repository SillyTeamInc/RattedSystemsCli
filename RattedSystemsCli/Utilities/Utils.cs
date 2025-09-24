using System.Diagnostics;
using System.Net;
using System.Text;
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
        FieldInfo? field = typeof(HttpListenerResponse).GetField("_responseState", BindingFlags.NonPublic | BindingFlags.Instance);
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
            ClipboardService.SetTextAsync(text).Wait();
        }
        catch (Exception ex)
        {
            Emi.Error($"Failed to set clipboard text: {ex}");
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
}