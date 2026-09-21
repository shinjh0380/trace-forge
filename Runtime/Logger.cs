using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace TraceForge
{
    /// <summary>
    /// Internal logging engine. Use <see cref="TF"/> for all external access.
    /// </summary>
    internal static class Logger
    {
        // Non-volatile: Interlocked.Exchange provides the necessary memory barriers
        private static ILogSink[] _sinks = Array.Empty<ILogSink>();
        private static readonly object _sinksLock = new object();

        // int backing field for Interlocked operations on enum
        private static int _globalMinVerbosityInt = (int)Verbosity.Debug;

        // Copy-on-write dictionary; reference itself is volatile for lock-free reads
        private static volatile Dictionary<string, Verbosity> _categoryVerbosities
            = new Dictionary<string, Verbosity>(StringComparer.Ordinal);
        private static readonly object _filterLock = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Bootstrap.Shutdown();

            ILogSink[] sinks;
            lock (_sinksLock)
            {
                // Snapshot under the writer lock, but invoke user sink callbacks outside it.
                sinks = _sinks;
            }

            foreach (var sink in sinks)
                try { sink.Flush(); } catch { /* ignore */ }

            bool hadRingBuffers;
            lock (_sinksLock)
            {
                // Logger is the single writer: clear both snapshots together after callbacks return.
                hadRingBuffers = SinkRegistry.RingBuffers.Length > 0;
                Interlocked.Exchange(ref _sinks, Array.Empty<ILogSink>());
                SinkRegistry.Clear();
            }
            if (hadRingBuffers)
                SinkRegistry.NotifyChanged();

            lock (_filterLock)
            {
                Interlocked.Exchange(ref _globalMinVerbosityInt, (int)Verbosity.Debug);
                _categoryVerbosities = new Dictionary<string, Verbosity>(StringComparer.Ordinal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsEnabled(Verbosity verbosity)
        {
            return (int)verbosity >= _globalMinVerbosityInt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsEnabled(Verbosity verbosity, in LogCategory category)
        {
            if ((int)verbosity < _globalMinVerbosityInt)
                return false;

            // Lock-free read of the dictionary reference (volatile)
            var dict = _categoryVerbosities;
            if (dict.Count > 0 && dict.TryGetValue(category.Name ?? string.Empty, out Verbosity categoryMin))
                return (int)verbosity >= (int)categoryMin;

            return true;
        }

        [HideInCallstack]
        internal static void Write(Verbosity verbosity, in LogCategory category, string message, Exception exception)
        {
            if (!IsEnabled(verbosity, category))
                return;

            var entry = new LogEntry(verbosity, category, message, exception, DateTime.UtcNow.Ticks);

            // Interlocked.Exchange guarantees we read the latest reference
            var sinks = Interlocked.CompareExchange(ref _sinks, null, null);
            if (sinks == null || sinks.Length == 0)
                return;

            foreach (var sink in sinks)
            {
                try
                {
                    sink.Write(in entry);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Console.Error.WriteLine(
                            $"[TraceForge] Sink '{sink.GetType().Name}' threw an exception: {ex}");
                    }
                    catch
                    {
                        // Last-resort reporting must not interrupt dispatch to remaining sinks.
                    }
                }
            }
        }

        internal static void SetMinVerbosity(Verbosity verbosity)
        {
            Interlocked.Exchange(ref _globalMinVerbosityInt, (int)verbosity);
        }

        internal static void SetCategoryVerbosity(in LogCategory category, Verbosity verbosity)
        {
            lock (_filterLock)
            {
                var dict = new Dictionary<string, Verbosity>(_categoryVerbosities, StringComparer.Ordinal);
                dict[category.Name ?? string.Empty] = verbosity;
                _categoryVerbosities = dict;
            }
        }

        internal static void ClearCategoryVerbosity(in LogCategory category)
        {
            lock (_filterLock)
            {
                var key = category.Name ?? string.Empty;
                if (!_categoryVerbosities.ContainsKey(key))
                    return;
                var dict = new Dictionary<string, Verbosity>(_categoryVerbosities, StringComparer.Ordinal);
                dict.Remove(key);
                _categoryVerbosities = dict;
            }
        }

        internal static void ClearAllCategoryVerbosities()
        {
            lock (_filterLock)
            {
                _categoryVerbosities = new Dictionary<string, Verbosity>(StringComparer.Ordinal);
            }
        }

        internal static void AddSink(ILogSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            lock (_sinksLock)
            {
                var current = _sinks;
                var next = new ILogSink[current.Length + 1];
                Array.Copy(current, next, current.Length);
                next[current.Length] = sink;
                Interlocked.Exchange(ref _sinks, next);
                SinkRegistry.Register(sink);
            }
            if (sink is RingBufferSink)
                SinkRegistry.NotifyChanged();
        }

        internal static bool RemoveSink(ILogSink sink)
        {
            if (sink == null) return false;
            bool registryChanged = false;
            bool removed;
            lock (_sinksLock)
            {
                var current = _sinks;
                int idx = Array.IndexOf(current, sink);
                if (idx < 0) return false;
                var next = new ILogSink[current.Length - 1];
                Array.Copy(current, 0, next, 0, idx);
                Array.Copy(current, idx + 1, next, idx, current.Length - idx - 1);
                Interlocked.Exchange(ref _sinks, next);
                if (Array.IndexOf(next, sink) < 0)
                {
                    SinkRegistry.Unregister(sink);
                    registryChanged = sink is RingBufferSink;
                }
                removed = true;
            }
            if (registryChanged)
                SinkRegistry.NotifyChanged();
            return removed;
        }

        internal static void ClearSinks()
        {
            bool hadRingBuffers;
            lock (_sinksLock)
            {
                hadRingBuffers = SinkRegistry.RingBuffers.Length > 0;
                Interlocked.Exchange(ref _sinks, Array.Empty<ILogSink>());
                SinkRegistry.Clear();
            }
            if (hadRingBuffers)
                SinkRegistry.NotifyChanged();
        }

        internal static Verbosity GetMinVerbosity() => (Verbosity)_globalMinVerbosityInt;
    }
}
