namespace TraceForge
{
    /// <summary>
    /// Predefined log categories for common Unity subsystems.
    /// Create custom categories with <c>new LogCategory("MySystem")</c>.
    /// </summary>
    public static class Categories
    {
        public static readonly LogCategory Default = new LogCategory("Default");
        public static readonly LogCategory Gameplay = new LogCategory("Gameplay");
        public static readonly LogCategory Network = new LogCategory("Network");
        public static readonly LogCategory UI = new LogCategory("UI");
        public static readonly LogCategory Audio = new LogCategory("Audio");
        public static readonly LogCategory Physics = new LogCategory("Physics");
        public static readonly LogCategory AI = new LogCategory("AI");
        public static readonly LogCategory Performance = new LogCategory("Performance");
        /// <summary>Unity engine and third-party log category.</summary>
        public static readonly LogCategory Unity = new LogCategory("Unity");
    }
}
