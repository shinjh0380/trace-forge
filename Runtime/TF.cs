using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TraceForge
{
    /// <summary>
    /// TraceForge static facade. Entry point for all logging operations.
    /// </summary>
    /// <example>
    /// TF.Info("Game started");
    /// TF.Warning(Categories.Network, "Packet lost");
    /// if (TF.IsEnabled(Verbosity.Trace)) TF.Trace(BuildExpensiveMessage());
    /// </example>
    public static class TF
    {
#if TRACEFORGE_STRIP_TRACE
        internal static readonly bool IsTraceStripped = true;
#else
        internal static readonly bool IsTraceStripped = false;
#endif
#if TRACEFORGE_STRIP_DEBUG
        internal static readonly bool IsDebugStripped = true;
#else
        internal static readonly bool IsDebugStripped = false;
#endif

        // ── Verbosity shortcuts (default category) ────────────────────────

        /// <summary>Logs a Trace-level message to the default category.</summary>
        [HideInCallstack]
        public static void Trace(string message)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_TRACE
            // compiled out
#else
            Logger.Write(Verbosity.Trace, Categories.Default, message, null);
#endif
        }

        /// <summary>Logs a Debug-level message to the default category.</summary>
        [HideInCallstack]
        public static void Debug(string message)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_DEBUG
            // compiled out
#else
            Logger.Write(Verbosity.Debug, Categories.Default, message, null);
#endif
        }

        /// <summary>Logs an Info-level message to the default category.</summary>
        [HideInCallstack]
        public static void Info(string message)
        {
#if TRACEFORGE_DISABLE
            // compiled out
#else
            Logger.Write(Verbosity.Info, Categories.Default, message, null);
#endif
        }

        /// <summary>Logs a Warning-level message to the default category.</summary>
        [HideInCallstack]
        public static void Warning(string message)
        {
#if TRACEFORGE_DISABLE
            // compiled out
#else
            Logger.Write(Verbosity.Warning, Categories.Default, message, null);
#endif
        }

        /// <summary>Logs an Error-level message to the default category.</summary>
        [HideInCallstack]
        public static void Error(string message)
        {
#if TRACEFORGE_DISABLE
            // compiled out
#else
            Logger.Write(Verbosity.Error, Categories.Default, message, null);
#endif
        }

        /// <summary>Logs a Fatal-level message to the default category.</summary>
        [HideInCallstack]
        public static void Fatal(string message)
        {
#if TRACEFORGE_DISABLE
            // compiled out
#else
            Logger.Write(Verbosity.Fatal, Categories.Default, message, null);
#endif
        }

        // ── Category-specified shortcuts ──────────────────────────────────

        /// <summary>Logs a Trace-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Trace(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_TRACE
#else
            Logger.Write(Verbosity.Trace, category, message, null);
#endif
        }

        /// <summary>Logs a Debug-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Debug(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_DEBUG
#else
            Logger.Write(Verbosity.Debug, category, message, null);
#endif
        }

        /// <summary>Logs an Info-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Info(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Info, category, message, null);
#endif
        }

        /// <summary>Logs a Warning-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Warning(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Warning, category, message, null);
#endif
        }

        /// <summary>Logs an Error-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Error(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Error, category, message, null);
#endif
        }

        /// <summary>Logs a Fatal-level message to the specified category.</summary>
        [HideInCallstack]
        public static void Fatal(LogCategory category, string message)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Fatal, category, message, null);
#endif
        }

        // ── Generic log ───────────────────────────────────────────────────

        /// <summary>Logs a message at the specified verbosity level and category.</summary>
        [HideInCallstack]
        public static void Log(LogCategory category, Verbosity verbosity, string message)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(verbosity, category, message, null);
#endif
        }

        /// <summary>Logs a message with a Unity context object. Call from the main thread.</summary>
        /// <param name="category">The category.</param><param name="verbosity">The verbosity.</param>
        /// <param name="message">The message.</param><param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Log(LogCategory category, Verbosity verbosity, string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(verbosity, category, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs a Warning with a Unity context object. Call from the main thread.</summary>
        /// <param name="message">The message.</param><param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Warning(string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Warning, Categories.Default, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs a Warning to a category with a Unity context object. Call from the main thread.</summary>
        /// <param name="category">The category.</param><param name="message">The message.</param>
        /// <param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Warning(LogCategory category, string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Warning, category, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs an Error with a Unity context object. Call from the main thread.</summary>
        /// <param name="message">The message.</param><param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Error(string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Error, Categories.Default, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs an Error to a category with a Unity context object. Call from the main thread.</summary>
        /// <param name="category">The category.</param><param name="message">The message.</param>
        /// <param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Error(LogCategory category, string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Error, category, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs an Error with exception and Unity context. Call from the main thread.</summary>
        /// <param name="message">The message.</param><param name="exception">The exception.</param>
        /// <param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Error(string message, Exception exception, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Error, Categories.Default, message, exception, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs a Fatal with a Unity context object. Call from the main thread.</summary>
        /// <param name="message">The message.</param><param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Fatal(string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Fatal, Categories.Default, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs a Fatal to a category with a Unity context object. Call from the main thread.</summary>
        /// <param name="category">The category.</param><param name="message">The message.</param>
        /// <param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Fatal(LogCategory category, string message, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Fatal, category, message, null, context != null ? context.GetInstanceID() : 0);
#endif
        }

        /// <summary>Logs an Error with exception, category, and Unity context. Call from the main thread.</summary>
        /// <param name="category">The category.</param><param name="message">The message.</param>
        /// <param name="exception">The exception.</param><param name="context">The Unity context object.</param>
        [HideInCallstack]
        public static void Error(LogCategory category, string message, Exception exception, UnityEngine.Object context)
        {
#if !TRACEFORGE_DISABLE
            Logger.Write(Verbosity.Error, category, message, exception, context != null ? context.GetInstanceID() : 0);
#endif
        }

        // ── Exception overloads ───────────────────────────────────────────

        /// <summary>Logs an Error-level message with an associated exception.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception to attach to the log entry.</param>
        [HideInCallstack]
        public static void Error(string message, Exception exception)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Error, Categories.Default, message, exception);
#endif
        }

        /// <summary>Logs an Error-level message with an associated exception to the specified category.</summary>
        /// <param name="category">The category to log to.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception to attach to the log entry.</param>
        [HideInCallstack]
        public static void Error(LogCategory category, string message, Exception exception)
        {
#if TRACEFORGE_DISABLE
#else
            Logger.Write(Verbosity.Error, category, message, exception);
#endif
        }

        // ── Filtering checks ──────────────────────────────────────────────

        /// <summary>Returns true if a message at <paramref name="verbosity"/> would be dispatched to at least one sink.</summary>
        /// <param name="verbosity">The verbosity level to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEnabled(Verbosity verbosity)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_TRACE
            if (verbosity == Verbosity.Trace)
                return false;
#endif
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_DEBUG
            if (verbosity == Verbosity.Debug)
                return false;
#endif
#if TRACEFORGE_DISABLE
            return false;
#else
            return Logger.IsEnabled(verbosity);
#endif
        }

        /// <summary>Returns true if a message at <paramref name="verbosity"/> for <paramref name="category"/> would be dispatched.</summary>
        /// <param name="verbosity">The verbosity level to check.</param>
        /// <param name="category">The category to check for per-category verbosity overrides.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEnabled(Verbosity verbosity, in LogCategory category)
        {
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_TRACE
            if (verbosity == Verbosity.Trace)
                return false;
#endif
#if TRACEFORGE_DISABLE || TRACEFORGE_STRIP_DEBUG
            if (verbosity == Verbosity.Debug)
                return false;
#endif
#if TRACEFORGE_DISABLE
            return false;
#else
            return Logger.IsEnabled(verbosity, category);
#endif
        }

        // ── Configuration ─────────────────────────────────────────────────

        /// <summary>Sets the global minimum verbosity. Messages below this level are discarded.</summary>
        public static void SetMinVerbosity(Verbosity verbosity) => Logger.SetMinVerbosity(verbosity);

        /// <summary>Sets a per-category verbosity override. Takes precedence over the global minimum.</summary>
        public static void SetCategoryVerbosity(in LogCategory category, Verbosity verbosity)
            => Logger.SetCategoryVerbosity(category, verbosity);

        /// <summary>Removes a per-category verbosity override, restoring global minimum behavior.</summary>
        public static void ClearCategoryVerbosity(in LogCategory category)
            => Logger.ClearCategoryVerbosity(category);

        /// <summary>Registers a sink to receive log entries.</summary>
        public static void AddSink(ILogSink sink) => Logger.AddSink(sink);

        /// <summary>Unregisters a sink. Returns true if the sink was found and removed.</summary>
        public static bool RemoveSink(ILogSink sink) => Logger.RemoveSink(sink);

        /// <summary>Removes all registered sinks.</summary>
        public static void ClearSinks() => Logger.ClearSinks();

        /// <summary>
        /// Resets the logger to default state. Primarily for use in tests.
        /// </summary>
        public static void Reset()
        {
            Logger.ClearSinks();
            Logger.SetMinVerbosity(Verbosity.Debug);
            Logger.SetStackTracePolicy(Logger.GetDefaultStackTracePolicy());
            Logger.ClearAllCategoryVerbosities();
        }
    }
}
