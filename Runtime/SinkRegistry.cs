using System;

namespace TraceForge
{
    internal static class SinkRegistry
    {
        private static volatile RingBufferSink[] _ringBuffers = Array.Empty<RingBufferSink>();

        internal static RingBufferSink[] RingBuffers => _ringBuffers;

        internal static event Action Changed;

        // Logger is the only writer and holds _sinksLock for every mutation.
        internal static void Register(ILogSink sink)
        {
            var ringBuffer = sink as RingBufferSink;
            if (ringBuffer == null)
                return;

            var current = _ringBuffers;
            if (Array.IndexOf(current, ringBuffer) >= 0)
                return;
            var next = new RingBufferSink[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = ringBuffer;
            _ringBuffers = next;
        }

        internal static void Unregister(ILogSink sink)
        {
            var ringBuffer = sink as RingBufferSink;
            if (ringBuffer == null)
                return;

            var current = _ringBuffers;
            int index = Array.IndexOf(current, ringBuffer);
            if (index < 0)
                return;
            var next = new RingBufferSink[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            _ringBuffers = next;
        }

        internal static void Clear()
        {
            if (_ringBuffers.Length == 0)
                return;
            _ringBuffers = Array.Empty<RingBufferSink>();
        }

        internal static void NotifyChanged()
        {
            // Logger calls this only after releasing _sinksLock.
            var handler = Changed;
            handler?.Invoke();
        }
    }
}
