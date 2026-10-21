using LiveSplit.Hosting;
using System;
using System.Diagnostics;
using System.IO;

namespace LiveSplit.Options;

public static class Log
{
    private static readonly object SyncRoot = new();
    private const long MaxLogSize = 1024 * 1024;

    static Log()
    {
        try
        {
            Trace.Listeners.Add(new LogFileTraceListener()
            {
                Filter = new EventTypeFilter(SourceLevels.Warning)
            });
        }
        catch { }
    }

    public static string LogFilePath => Path.Combine(Paths.LogDirectory, "LiveSplit.log");

    private sealed class LogFileTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            Append(message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{eventType}] {message}");
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{eventType}] {string.Format(format, args)}");
        }

        private static void Append(string text)
        {
            try
            {
                lock (SyncRoot)
                {
                    string path = LogFilePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogSize)
                    {
                        File.Move(path, path + ".old", true);
                    }

                    File.AppendAllText(path, text + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    public static void Error(Exception ex)
    {
        try
        {
            Trace.TraceError("{0}\n\n{1}", ex.Message, ex.StackTrace);
        }
        catch { }
    }

    public static void Error(string message)
    {
        try
        {
            Trace.TraceError(message);
        }
        catch { }
    }

    public static void Info(string message)
    {
        try
        {
            Trace.TraceInformation(message);
        }
        catch { }
    }

    public static void Warning(string message)
    {
        try
        {
            Trace.TraceWarning(message);
        }
        catch { }
    }
}
