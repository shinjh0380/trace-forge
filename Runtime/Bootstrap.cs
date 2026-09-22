using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TraceForge
{
    /// <summary>
    /// Creates the sinks and filter configuration supplied by TraceForge settings.
    /// </summary>
    internal static class Bootstrap
    {
        private static readonly object LifecycleLock = new object();
        private static readonly List<ILogSink> OwnedSinks = new List<ILogSink>();
        private static bool _initialized;
        private static bool _shuttingDown;
        private static int _configurationVersion;

        /// <summary>Editor-only settings provider; assigned by the Editor assembly.</summary>
        internal static Func<TraceForgeSettings> EditorSettingsLoader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            Initialize();
        }

        internal static void Initialize()
        {
            TraceForgeSettings settings;
#if UNITY_EDITOR
            settings = EditorSettingsLoader != null ? EditorSettingsLoader() : null;
#else
            settings = FindPreloadedSettings();
#endif

            lock (LifecycleLock)
            {
                if (_initialized || _shuttingDown)
                    return;
                _initialized = true;
            }

            try
            {
                ApplySettings(settings);
                Application.quitting -= Shutdown;
                Application.quitting += Shutdown;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        internal static void ApplySettings(TraceForgeSettings settings)
        {
            int configurationVersion;
            lock (LifecycleLock)
                configurationVersion = ++_configurationVersion;
            DisposeOwnedSinks();

            if (settings == null)
            {
                Logger.SetMinVerbosity(Verbosity.Debug);
                Logger.SetStackTracePolicy(Logger.GetDefaultStackTracePolicy());
                Logger.ClearAllCategoryVerbosities();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (SinkRegistry.RingBuffers.Length == 0)
                {
                    if (!AddOwnedSink(new RingBufferSink(1024), configurationVersion))
                        return;
                }
#endif
                StartUnityCapture(configurationVersion, true);
                return;
            }

            Logger.SetMinVerbosity(settings.MinVerbosity);
            Logger.SetStackTracePolicy(settings.StackTracePolicy);
            Logger.ClearAllCategoryVerbosities();
            if (settings.CategoryOverrides != null)
            {
                foreach (var categoryOverride in settings.CategoryOverrides)
                    Logger.SetCategoryVerbosity(new LogCategory(categoryOverride.Category), categoryOverride.Verbosity);
            }

            bool enableRingBuffer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            enableRingBuffer = true;
#else
            enableRingBuffer = settings.EnableRingBuffer;
#endif
            enableRingBuffer = enableRingBuffer || settings.EnableRingBuffer;
            if (enableRingBuffer && SinkRegistry.RingBuffers.Length == 0)
            {
                if (!AddOwnedSink(new RingBufferSink(Math.Max(1, settings.RingBufferCapacity)), configurationVersion))
                    return;
            }

            if (settings.EnableFileSink)
            {
                string path = settings.FilePath;
                if (string.IsNullOrEmpty(path))
                    path = "traceforge.log";
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(Application.persistentDataPath, path);
                AddOwnedSink(new FileSink(path, settings.AppendToFile, Math.Max(1, settings.FileQueueCapacity)), configurationVersion);
            }

            StartUnityCapture(configurationVersion, settings.CaptureUnityLog);
        }

        internal static void Shutdown()
        {
            lock (LifecycleLock)
            {
                if (_shuttingDown)
                    return;
                _shuttingDown = true;
                _initialized = false;
                _configurationVersion++;
                Application.quitting -= Shutdown;
            }

            try
            {
                UnityLogCapture.Stop();
                DisposeOwnedSinks();
            }
            finally
            {
                lock (LifecycleLock)
                    _shuttingDown = false;
            }
        }

        private static void StartUnityCapture(int configurationVersion, bool enabled)
        {
            if (!enabled)
            {
                UnityLogCapture.Stop();
                return;
            }
            lock (LifecycleLock)
            {
                if (configurationVersion != _configurationVersion || !_initialized || _shuttingDown)
                    return;
            }
            UnityLogCapture.Start();
            lock (LifecycleLock)
            {
                if (configurationVersion != _configurationVersion || !_initialized || _shuttingDown)
                    UnityLogCapture.Stop();
            }
        }

        private static bool AddOwnedSink(ILogSink sink, int configurationVersion)
        {
            lock (LifecycleLock)
            {
                if (configurationVersion != _configurationVersion)
                {
                    DisposeUnregisteredSink(sink);
                    return false;
                }
                OwnedSinks.Add(sink);
            }

            try
            {
                Logger.AddSink(sink);
            }
            catch
            {
                lock (LifecycleLock)
                    OwnedSinks.Remove(sink);
                try { Logger.RemoveSink(sink); }
                catch { }
                try { sink.Flush(); }
                finally
                {
                    if (sink is IDisposable disposable)
                        disposable.Dispose();
                }
                throw;
            }

            lock (LifecycleLock)
            {
                if (configurationVersion == _configurationVersion)
                    return true;
                OwnedSinks.Remove(sink);
            }

            try { Logger.RemoveSink(sink); }
            catch { }
            DisposeUnregisteredSink(sink);
            return false;
        }

        private static void DisposeUnregisteredSink(ILogSink sink)
        {
            try { sink.Flush(); }
            finally
            {
                if (sink is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private static void DisposeOwnedSinks()
        {
            ILogSink[] sinks;
            lock (LifecycleLock)
            {
                sinks = OwnedSinks.ToArray();
                OwnedSinks.Clear();
            }

            Exception firstFailure = null;
            foreach (var sink in sinks)
            {
                try { Logger.RemoveSink(sink); }
                catch (Exception exception) { firstFailure = firstFailure ?? exception; }
                try { sink.Flush(); }
                catch (Exception exception) { firstFailure = firstFailure ?? exception; }
                finally
                {
                    if (sink is IDisposable disposable)
                    {
                        try { disposable.Dispose(); }
                        catch (Exception exception) { firstFailure = firstFailure ?? exception; }
                    }
                }
            }

            if (firstFailure != null)
                throw firstFailure;
        }

        private static TraceForgeSettings FindPreloadedSettings()
        {
            var settings = Resources.FindObjectsOfTypeAll<TraceForgeSettings>();
            return settings.Length == 0 ? null : settings[0];
        }
    }
}
