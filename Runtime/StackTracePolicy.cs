namespace TraceForge
{
    /// <summary>
    /// Controls which log entries capture a stack trace.
    /// </summary>
    public enum StackTracePolicy
    {
        /// <summary>Never capture stack traces.</summary>
        None,

        /// <summary>Capture stack traces for errors and fatal entries.</summary>
        ErrorAndAbove,

        /// <summary>Capture stack traces for every entry.</summary>
        All
    }
}
