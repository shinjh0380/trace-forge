using System;
using UnityEngine;

namespace TraceForge
{
    /// <summary>
    /// Routes TraceForge log entries to the Unity Console via <c>Debug.Log</c>, <c>Debug.LogWarning</c>, and <c>Debug.LogError</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Thread safety:</b> Must be called from the Unity Main Thread. Unity's <c>Debug.Log</c> APIs are not thread-safe.</para>
    /// <para><b>Re-entry protection:</b> Uses a <c>[ThreadStatic]</c> guard to prevent recursive calls (e.g., from <c>Application.logMessageReceived</c>).</para>
    /// </remarks>
    public sealed class UnityConsoleSink : ILogSink
    {
        [ThreadStatic]
        private static bool _isWriting;

        /// <summary>Writes a log entry to the Unity Console. Must be called from the Main Thread.</summary>
        [HideInCallstack]
        public void Write(in LogEntry entry)
        {
            // Guard against re-entrant calls (e.g., Unity itself logging during Write)
            if (_isWriting)
                return;

            _isWriting = true;
            try
            {
                string formatted = Format(in entry);

                switch (entry.Verbosity)
                {
                    case Verbosity.Warning:
                        Debug.LogWarning(formatted);
                        break;
                    case Verbosity.Error:
                    case Verbosity.Fatal:
                        if (entry.Exception != null)
                            Debug.LogException(entry.Exception);
                        else
                            Debug.LogError(formatted);
                        break;
                    default:
                        Debug.Log(formatted);
                        break;
                }
            }
            finally
            {
                _isWriting = false;
            }
        }

        /// <summary>No-op. Unity Console has no flush concept.</summary>
        public void Flush()
        {
            // Unity Console has no flush concept
        }

        private static string Format(in LogEntry entry)
        {
            var category = entry.Category.Name;
            var message = entry.Message ?? string.Empty;

            if (string.IsNullOrEmpty(category) || category == "Default")
                return message;

            return $"[{category}] {message}";
        }
    }
}
