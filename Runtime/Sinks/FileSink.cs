using System;
using System.IO;

namespace TraceForge
{
    /// <summary>
    /// Writes log entries to a file. Thread-safe. Implements <see cref="IDisposable"/>.
    /// </summary>
    public sealed class FileSink : ILogSink, IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Opens or creates a log file at <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the log file.</param>
        /// <param name="append">If true, appends to existing file; otherwise overwrites.</param>
        public FileSink(string filePath, bool append = false)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _writer = new StreamWriter(filePath, append, System.Text.Encoding.UTF8) { AutoFlush = false };
        }

        /// <summary>Writes a formatted log entry line to the file. Thread-safe.</summary>
        public void Write(in LogEntry entry)
        {
            if (_disposed) return;

            var line = FormatLine(in entry);
            lock (_lock)
            {
                if (_disposed) return;
                _writer.WriteLine(line);
            }
        }

        /// <summary>Flushes all buffered entries to the underlying file stream. Thread-safe.</summary>
        public void Flush()
        {
            if (_disposed) return;
            lock (_lock)
            {
                if (_disposed) return;
                _writer.Flush();
            }
        }

        /// <summary>Flushes and closes the underlying file stream.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                try { _writer.Flush(); } catch { /* ignore */ }
                _writer.Dispose();
            }
        }

        private static string FormatLine(in LogEntry entry)
        {
            var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var verbosity = entry.Verbosity.ToString().ToUpperInvariant();
            var category = entry.Category.Name ?? "Default";
            var message = entry.Message ?? string.Empty;

            if (entry.Exception != null)
                return $"[{timestamp}] [{verbosity}] [{category}] {message}{Environment.NewLine}Exception: {entry.Exception}";

            return $"[{timestamp}] [{verbosity}] [{category}] {message}";
        }
    }
}
