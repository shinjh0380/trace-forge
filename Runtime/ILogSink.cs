namespace TraceForge
{
    /// <summary>
    /// Receives log entries from the TraceForge logger.
    /// Implementations must be thread-safe.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// Called for each log entry that passes verbosity and category filters.
        /// </summary>
        /// <param name="entry">The log entry. Passed by reference to avoid struct copy.</param>
        void Write(in LogEntry entry);

        /// <summary>
        /// Flushes any buffered entries. Called automatically on application quit.
        /// </summary>
        void Flush();
    }
}
