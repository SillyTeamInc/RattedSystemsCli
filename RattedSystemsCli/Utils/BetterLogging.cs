


using RattedSystemsCli.Utils;

namespace BetterLogging;

public static class Emi
{
    public static Dictionary<LogLevel, string> LogLevelColors = new()
    {
        { LogLevel.Debug, Console.Log.Color3 },
        { LogLevel.Info, Console.Log.Color7 },
        { LogLevel.Warning, Console.Log.Color6 },
        { LogLevel.Error, Console.Log.ColorC },
        //https://ratted.systems/u/BXO4HgAw.png{ LogLevel.Critical, Console.Log.ColorNegative + Console.Log.Color4 }
    };


    private static void LogInternal(LogLevel level, string caption, string message)
    {
        Console.WriteLine(
            $"{Console.Log.Color7}[{Console.Log.ColorA}{DateTimeOffset.Now:HH:mm:ss}{Console.Log.Color7}] " +
            $"{Console.Log.Color7}[{LogLevelColors[level]}{level.ToString().ToLower()}{Console.Log.ColorPositive}{Console.Log.Color7}] " +
            $"{Console.Log.ValueColor}{caption}:{Console.Log.R} {message}");
    }

    public static void Log(string caption, string message, LogLevel level = LogLevel.Info)
    {
        LogInternal(level, caption, message);
    }

    public static void Log(string caption, string message, Exception ex, LogLevel level = LogLevel.Error)
    {
        LogInternal(level, caption, message);
        Console.WriteLine(
            $"{Console.Log.Color7}[{Console.Log.ColorA}{DateTimeOffset.Now:HH:mm:ss}{Console.Log.Color7}] " +
            $"{Console.Log.Color7}[{LogLevelColors[level]}{level.ToString().ToLower()}{Console.Log.ColorPositive}{Console.Log.Color7}] " +
            $"{Console.Log.ValueColor}{caption}:{Console.Log.R} {ex}");
    }

    // versions that use Utils.GetCallerMethod()?.Name ?? "Unknown"
    public static void Log(string message, LogLevel level = LogLevel.Info)
    {
        LogInternal(level, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }

    // debug, info, warn, error, critical
    public static void Debug(string message)
    {
        LogInternal(LogLevel.Debug, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }

    public static void Info(string message)
    {
        LogInternal(LogLevel.Info, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }

    public static void Warn(string message)
    {
        LogInternal(LogLevel.Warning, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }

    public static void Error(string message)
    {
        LogInternal(LogLevel.Error, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }

    /*public static void Critical(string message)
    {
        LogInternal(LogLevel.Critical, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", message);
    }*/

    // same as above but with string.Format
    public static void Log(string caption, string message, params object[] args)
    {
        LogInternal(LogLevel.Info, caption, string.Format(message, args));
    }

    public static void Log(string caption, string message, Exception ex, params object[] args)
    {
        LogInternal(LogLevel.Error, caption, string.Format(message, args));
        Console.WriteLine(
            $"{Console.Log.Color7}[{Console.Log.ColorA}{DateTimeOffset.Now:HH:mm:ss}{Console.Log.Color7}] " +
            $"{Console.Log.Color7}[{LogLevelColors[LogLevel.Error]}error{Console.Log.ColorPositive}{Console.Log.Color7}] " +
            $"{Console.Log.ValueColor}{caption}:{Console.Log.R} {ex}");
    }

    public static void Log(string message, params object[] args)
    {
        LogInternal(LogLevel.Info, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", string.Format(message, args));
    }

    public static void Debug(string message, params object[] args)
    {
        LogInternal(LogLevel.Debug, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown",
            string.Format(message, args));
    }

    public static void Info(string message, params object[] args)
    {
        LogInternal(LogLevel.Info, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown", string.Format(message, args));
    }

    public static void Warn(string message, params object[] args)
    {
        LogInternal(LogLevel.Warning, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown",
            string.Format(message, args));
    }

    public static void Error(string message, params object[] args)
    {
        LogInternal(LogLevel.Error, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown",
            string.Format(message, args));
    }

    /*public static void Critical(string message, params object[] args)
    {
        LogInternal(LogLevel.Critical, Utils.GetCallerFrame().GetMethod()?.Name ?? "Unknown",
            string.Format(message, args));
    }*/
}
