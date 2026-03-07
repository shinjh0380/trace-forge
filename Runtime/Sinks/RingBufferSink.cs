using System;

namespace TraceForge
{
    /// <summary>
    /// In-memory circular buffer sink. Stores the most recent N log entries.
    /// Use <see cref="GetEntries"/> to retrieve a snapshot for display or replay.
    /// </summary>
    /// <remarks>All members are thread-safe. Use <see cref="GetEntries"/> to retrieve a snapshot.</remarks>
    public sealed class RingBufferSink : ILogSink
    {
        private readonly LogEntry[] _buffer;
        private readonly object _syncRoot = new object();
        private int _head;
        private int _count;

        /// <summary>
        /// Creates a ring buffer that stores the most recent <paramref name="capacity"/> entries.
        /// </summary>
        public RingBufferSink(int capacity = 512)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            _buffer = new LogEntry[capacity];
        }

        /// <summary>Maximum number of entries this buffer can hold.</summary>
        public int Capacity => _buffer.Length;

        /// <summary>Current number of entries stored in the buffer.</summary>
        public int Count { get { lock (_syncRoot) return _count; } }

        /// <summary>Writes a log entry to the ring buffer. Thread-safe.</summary>
        public void Write(in LogEntry entry)
        {
            lock (_syncRoot)
            {
                _buffer[_head] = entry;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length)
                    _count++;
            }
        }

        /// <summary>No-op for ring buffer. Entries are always immediately available via <see cref="GetEntries"/>.</summary>
        public void Flush()
        {
            // In-memory — nothing to flush
        }

        /// <summary>
        /// Returns a snapshot of the most recent entries, oldest first.
        /// </summary>
        public LogEntry[] GetEntries()
        {
            lock (_syncRoot)
            {
                if (_count == 0)
                    return Array.Empty<LogEntry>();

                var result = new LogEntry[_count];
                int start = _count < _buffer.Length ? 0 : _head;

                for (int i = 0; i < _count; i++)
                {
                    result[i] = _buffer[(start + i) % _buffer.Length];
                }

                return result;
            }
        }

        /// <summary>
        /// Clears all stored entries.
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _head = 0;
                _count = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }
    }
}
