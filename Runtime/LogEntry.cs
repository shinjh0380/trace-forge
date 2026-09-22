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
        /// <summary>Unity object instance ID associated with this entry, or zero when absent.</summary>
        public int ContextInstanceId { get; }
        /// <summary>The captured or externally supplied stack trace, or <see langword="null"/>.</summary>
        public string StackTrace { get; }

        /// <summary>Creates an entry without context or a stack trace.</summary>
        /// <param name="verbosity">The entry verbosity.</param>
        /// <param name="category">The entry category.</param>
        /// <param name="message">The entry message.</param>
        /// <param name="exception">The associated exception, if any.</param>
        /// <param name="timestampTicks">The UTC timestamp in ticks.</param>
        public LogEntry(Verbosity verbosity, LogCategory category, string message, Exception exception, long timestampTicks)
            : this(verbosity, category, message, exception, timestampTicks, 0, null)
        {
        }

        /// <summary>Creates an entry with context and stack trace metadata.</summary>
        /// <param name="verbosity">The entry verbosity.</param>
        /// <param name="category">The entry category.</param>
        /// <param name="message">The entry message.</param>
        /// <param name="exception">The associated exception, if any.</param>
        /// <param name="timestampTicks">The UTC timestamp in ticks.</param>
        /// <param name="contextInstanceId">The associated Unity object instance ID, or zero.</param>
        /// <param name="stackTrace">The captured or supplied stack trace, if any.</param>
        public LogEntry(
            Verbosity verbosity,
            LogCategory category,
            string message,
            Exception exception,
            long timestampTicks,
            int contextInstanceId,
            string stackTrace)
        {
            Verbosity = verbosity;
            Category = category;
            Message = message;
            Exception = exception;
            TimestampTicks = timestampTicks;
            ContextInstanceId = contextInstanceId;
            StackTrace = stackTrace;
        }
    }
}
