namespace TraceForge
{
    /// <summary>
    /// Runtime configuration snapshot for the TraceForge logger.
    /// Access and modify via <see cref="TF"/> static facade.
    /// </summary>
    public sealed class LoggerConfig
    {
        /// <summary>
        /// Global minimum verbosity. Log entries below this level are discarded.
        /// </summary>
        public Verbosity MinVerbosity { get; set; } = Verbosity.Debug;
    }
}
