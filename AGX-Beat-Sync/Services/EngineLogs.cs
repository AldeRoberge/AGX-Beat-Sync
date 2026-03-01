using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Central engine log buffer for the console. Use <see cref="Logger"/> to log;
/// the Event Console shows these when "Engine" is toggled on.
/// </summary>
public static class EngineLogs
{
    private const int MaxEntries = 500;

    private static readonly object Lock = new();
    private static readonly List<EngineLogEntry> Entries = new();
    private static readonly EngineLogSink Sink = new(Entries, Lock, MaxEntries);

    private static Microsoft.Extensions.Logging.ILogger? _logger;
    private static ILoggerFactory? _factory;

    /// <summary>
    /// Logger for engine/game/editor. Log here to see messages in the Console when Engine is enabled.
    /// </summary>
    public static Microsoft.Extensions.Logging.ILogger Logger => _logger ??= CreateLogger();

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(Sink)
            .CreateLogger();

        _factory = LoggerFactory.Create(builder => builder.AddSerilog(serilogLogger, dispose: false));
        return _factory.CreateLogger("Engine");
    }

    /// <summary>
    /// Thread-safe snapshot of current log entries (oldest first).
    /// </summary>
    public static IReadOnlyList<EngineLogEntry> GetSnapshot()
    {
        lock (Lock)
        {
            return Entries.Count == 0 ? (IReadOnlyList<EngineLogEntry>)System.Array.Empty<EngineLogEntry>() : new List<EngineLogEntry>(Entries);
        }
    }

    /// <summary>
    /// Clear all engine log entries (e.g. when starting playback).
    /// </summary>
    public static void Clear()
    {
        lock (Lock)
            Entries.Clear();
    }
}

/// <summary>
/// A single engine log line for display in the console.
/// </summary>
public sealed class EngineLogEntry
{
    public string Level { get; }
    public string Message { get; }
    public string TimeString { get; }

    public EngineLogEntry(string level, string message, string timeString)
    {
        Level = level;
        Message = message;
        TimeString = timeString;
    }
}

internal sealed class EngineLogSink : ILogEventSink
{
    private readonly List<EngineLogEntry> _entries;
    private readonly object _lock;
    private readonly int _maxEntries;

    public EngineLogSink(List<EngineLogEntry> entries, object lockObj, int maxEntries)
    {
        _entries = entries;
        _lock = lockObj;
        _maxEntries = maxEntries;
    }

    public void Emit(LogEvent logEvent)
    {
        string level = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "???"
        };
        string timeStr = logEvent.Timestamp.ToString("HH:mm:ss.fff");
        string message = logEvent.RenderMessage();

        lock (_lock)
        {
            _entries.Add(new EngineLogEntry(level, message, timeStr));
            while (_entries.Count > _maxEntries)
                _entries.RemoveAt(0);
        }
    }
}
