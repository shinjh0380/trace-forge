using System;

namespace TraceForge
{
    /// <summary>
    /// Immutable snapshot of a single log event. Allocated on the stack when passed to sinks.
    /// </summary>
    public readonly struct LogEntry
    {
        public Verbosity Verbosity { get; }
        public LogCategory Category { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public long TimestampTicks { get; }

        public LogEntry(Verbosity verbosity, LogCategory category, string message, Exception exception, long timestampTicks)
        {
            Verbosity = verbosity;
            Category = category;
            Message = message;
            Exception = exception;
            TimestampTicks = timestampTicks;
        }
    }
}
