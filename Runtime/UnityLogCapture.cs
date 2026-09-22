using System;
using UnityEngine;

namespace TraceForge
{
    internal static class UnityLogCapture
    {
        private static readonly object StateLock = new object();
        private static bool _started;
        [ThreadStatic] private static bool _handling;

        internal static void Start()
        {
            lock (StateLock)
            {
                if (_started)
                    return;
                Application.logMessageReceivedThreaded += OnMessage;
                _started = true;
            }
        }

        internal static void Stop()
        {
            lock (StateLock)
            {
                if (!_started)
                    return;
                Application.logMessageReceivedThreaded -= OnMessage;
                _started = false;
            }
        }

        private static void OnMessage(string condition, string stackTrace, LogType type)
        {
            if (_handling)
                return;

            _handling = true;
            try
            {
                Verbosity verbosity;
                switch (type)
                {
                    case LogType.Exception: verbosity = Verbosity.Fatal; break;
                    case LogType.Error:
                    case LogType.Assert: verbosity = Verbosity.Error; break;
                    case LogType.Warning: verbosity = Verbosity.Warning; break;
                    default: verbosity = Verbosity.Debug; break;
                }
                Logger.WriteCaptured(verbosity, Categories.Unity, condition, stackTrace);
            }
            finally
            {
                _handling = false;
            }
        }
    }
}
