using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace TraceForge
{
    /// <summary>
    /// Queues log entries and writes them to a file on a dedicated background thread.
    /// Thread-safe. Implements <see cref="IDisposable"/>.
    /// </summary>
    public sealed class FileSink : ILogSink, IDisposable
    {
        private const int DefaultQueueCapacity = 4096;
        private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        private readonly TextWriter _writer;
        private readonly LogEntry[] _queue;
        private readonly object _syncRoot = new object();
        private readonly object _writerLock = new object();
        private readonly object _lifecycleLock = new object();
        private readonly Thread _worker;

        private int _head;
        private int _tail;
        private int _count;
        private long _acceptedCount;
        private long _writtenCount;
        private bool _accepting = true;
        private bool _disposed;
        private Exception _workerException;

        /// <summary>
        /// Opens or creates a log file with the default queue capacity of 4,096 entries.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the log file.</param>
        /// <param name="append">If true, appends to an existing file; otherwise overwrites it.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        public FileSink(string filePath, bool append = false)
            : this(filePath, append, DefaultQueueCapacity)
        {
        }

        /// <summary>
        /// Opens or creates a log file with the specified bounded queue capacity.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the log file.</param>
        /// <param name="append">If true, appends to an existing file; otherwise overwrites it.</param>
        /// <param name="queueCapacity">Maximum number of entries waiting in the queue.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="queueCapacity"/> is not positive.</exception>
        public FileSink(string filePath, bool append, int queueCapacity)
            : this(CreateWriter(filePath, append, queueCapacity), queueCapacity)
        {
        }

        internal FileSink(TextWriter writer, int queueCapacity)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            ValidateQueueCapacity(queueCapacity);

            _writer = writer;
            _queue = new LogEntry[queueCapacity];
            _worker = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "TraceForge.FileSink"
            };

            try
            {
                _worker.Start();
            }
            catch
            {
                try { _writer.Dispose(); }
                catch { }
                throw;
            }
        }

        /// <summary>
        /// Enqueues a log entry. Blocks only while the bounded queue is full.
        /// </summary>
        /// <param name="entry">The immutable log entry to enqueue.</param>
        /// <exception cref="IOException">Thrown when the writer thread has failed.</exception>
        public void Write(in LogEntry entry)
        {
            lock (_syncRoot)
            {
                while (_count == _queue.Length && _accepting && _workerException == null)
                    Monitor.Wait(_syncRoot);

                ThrowIfFaultedLocked();

                if (!_accepting)
                    return;

                _queue[_tail] = entry;
                _tail = (_tail + 1) % _queue.Length;
                _count++;
                _acceptedCount++;
                Monitor.PulseAll(_syncRoot);
            }
        }

        /// <summary>
        /// Waits for all entries accepted before this call and flushes the underlying writer.
        /// </summary>
        /// <exception cref="IOException">Thrown when the writer thread or underlying writer has failed.</exception>
        public void Flush()
        {
            lock (_lifecycleLock)
            {
                long targetSequence;

                lock (_syncRoot)
                {
                    if (_disposed)
                        return;

                    ThrowIfFaultedLocked();
                    targetSequence = _acceptedCount;

                    while (_writtenCount < targetSequence && _workerException == null)
                        Monitor.Wait(_syncRoot);

                    ThrowIfFaultedLocked();
                }

                lock (_writerLock)
                {
                    lock (_syncRoot)
                        ThrowIfFaultedLocked();

                    try
                    {
                        _writer.Flush();
                    }
                    catch (Exception ex)
                    {
                        throw CreateWriterException(RecordWorkerFailure(ex));
                    }
                }
            }
        }

        /// <summary>
        /// Stops accepting entries, drains the queue, joins the writer thread, and closes the file.
        /// </summary>
        /// <exception cref="IOException">Thrown after cleanup when the underlying writer has failed.</exception>
        public void Dispose()
        {
            Exception failure;

            lock (_lifecycleLock)
            {
                lock (_syncRoot)
                {
                    if (_disposed)
                        return;

                    _accepting = false;
                    Monitor.PulseAll(_syncRoot);
                }

                _worker.Join();

                lock (_syncRoot)
                {
                    failure = _workerException;
                    Array.Clear(_queue, 0, _queue.Length);
                    _head = 0;
                    _tail = 0;
                    _count = 0;
                }

                lock (_writerLock)
                {
                    if (failure == null)
                    {
                        try { _writer.Flush(); }
                        catch (Exception ex) { failure = ex; }
                    }

                    try { _writer.Dispose(); }
                    catch (Exception ex)
                    {
                        if (failure == null)
                            failure = ex;
                    }
                }

                lock (_syncRoot)
                {
                    if (_workerException == null && failure != null)
                        _workerException = failure;

                    failure = _workerException;
                    _disposed = true;
                    Monitor.PulseAll(_syncRoot);
                }
            }

            if (failure != null)
                throw CreateWriterException(failure);
        }

        private void WriterLoop()
        {
            try
            {
                while (TryDequeue(out LogEntry entry))
                {
                    WriteEntry(in entry);

                    lock (_syncRoot)
                    {
                        _writtenCount++;
                        Monitor.PulseAll(_syncRoot);
                    }
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex);
            }
        }

        private bool TryDequeue(out LogEntry entry)
        {
            lock (_syncRoot)
            {
                while (_count == 0 && _accepting)
                    Monitor.Wait(_syncRoot);

                if (_count == 0)
                {
                    entry = default(LogEntry);
                    return false;
                }

                entry = _queue[_head];
                _queue[_head] = default(LogEntry);
                _head = (_head + 1) % _queue.Length;
                _count--;
                Monitor.PulseAll(_syncRoot);
                return true;
            }
        }

        private void WriteEntry(in LogEntry entry)
        {
            lock (_writerLock)
            {
                try
                {
                    Span<char> timestampBuffer = stackalloc char[24];
                    var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);

                    if (!timestamp.TryFormat(
                        timestampBuffer,
                        out int timestampLength,
                        TimestampFormat,
                        CultureInfo.InvariantCulture))
                    {
                        throw new FormatException("TraceForge could not format the log timestamp.");
                    }

                    _writer.Write('[');
                    _writer.Write(timestampBuffer.Slice(0, timestampLength));
                    _writer.Write("] [");
                    _writer.Write(GetVerbosityName(entry.Verbosity));
                    _writer.Write("] [");
                    _writer.Write(entry.Category.Name ?? "Default");
                    _writer.Write("] ");
                    _writer.WriteLine(entry.Message ?? string.Empty);

                    if (entry.Exception != null)
                    {
                        _writer.Write("Exception: ");
                        _writer.WriteLine(entry.Exception);
                    }
                }
                catch (Exception ex)
                {
                    RecordWorkerFailure(ex);
                    throw;
                }
            }
        }

        private Exception RecordWorkerFailure(Exception exception)
        {
            lock (_syncRoot)
            {
                if (_workerException == null)
                    _workerException = exception;

                _accepting = false;
                Monitor.PulseAll(_syncRoot);
                return _workerException;
            }
        }

        private void ThrowIfFaultedLocked()
        {
            if (_workerException != null)
                throw CreateWriterException(_workerException);
        }

        private static IOException CreateWriterException(Exception innerException)
        {
            return new IOException("TraceForge FileSink writer failed.", innerException);
        }

        private static TextWriter CreateWriter(string filePath, bool append, int queueCapacity)
        {
            ValidateQueueCapacity(queueCapacity);

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return new StreamWriter(filePath, append, System.Text.Encoding.UTF8)
            {
                AutoFlush = false
            };
        }

        private static void ValidateQueueCapacity(int queueCapacity)
        {
            if (queueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(queueCapacity),
                    "Queue capacity must be greater than zero.");
            }
        }

        private static string GetVerbosityName(Verbosity verbosity)
        {
            switch (verbosity)
            {
                case Verbosity.Trace: return "TRACE";
                case Verbosity.Debug: return "DEBUG";
                case Verbosity.Info: return "INFO";
                case Verbosity.Warning: return "WARNING";
                case Verbosity.Error: return "ERROR";
                case Verbosity.Fatal: return "FATAL";
                case Verbosity.Off: return "OFF";
                default: return "UNKNOWN";
            }
        }
    }
}
