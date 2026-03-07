namespace TraceForge
{
    /// <summary>
    /// Log verbosity levels in ascending severity order.
    /// Use <see cref="Off"/> to completely disable a category.
    /// </summary>
    public enum Verbosity
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5,
        Off = 6
    }
}
