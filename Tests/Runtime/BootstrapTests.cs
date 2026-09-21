using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TraceForge.Tests
{
    [TestFixture]
    public sealed class BootstrapTests
    {
        private TraceForgeSettings _settings;
        private Func<TraceForgeSettings> _previousSettingsLoader;

        [SetUp]
        public void SetUp()
        {
            Bootstrap.Shutdown();
            TF.Reset();
            _previousSettingsLoader = Bootstrap.EditorSettingsLoader;
        }

        [TearDown]
        public void TearDown()
        {
            Bootstrap.Shutdown();
            TF.Reset();
            Bootstrap.EditorSettingsLoader = _previousSettingsLoader;
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Settings_Defaults_MatchSchema()
        {
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();

            Assert.AreEqual(Verbosity.Debug, _settings.MinVerbosity);
            Assert.IsNotNull(_settings.CategoryOverrides);
            Assert.IsTrue(_settings.EnableRingBuffer);
            Assert.AreEqual(1024, _settings.RingBufferCapacity);
            Assert.IsFalse(_settings.EnableFileSink);
            Assert.AreEqual("traceforge.log", _settings.FilePath);
            Assert.IsTrue(_settings.AppendToFile);
            Assert.AreEqual(4096, _settings.FileQueueCapacity);
            Assert.IsTrue(_settings.CaptureUnityLog);
            Assert.AreEqual(StackTracePolicy.ErrorAndAbove, _settings.StackTracePolicy);
        }

        [Test]
        public void Settings_Schema_RoundTripsThroughUnitySerialization()
        {
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.MinVerbosity = Verbosity.Warning;
            _settings.CategoryOverrides = new[]
            {
                new TraceForgeSettings.CategoryOverride { Category = "Network", Verbosity = Verbosity.Trace }
            };
            _settings.EnableRingBuffer = false;
            _settings.RingBufferCapacity = 31;
            _settings.EnableFileSink = true;
            _settings.FilePath = "custom.log";
            _settings.AppendToFile = false;
            _settings.FileQueueCapacity = 71;
            _settings.CaptureUnityLog = false;
            _settings.StackTracePolicy = StackTracePolicy.All;

            var copy = ScriptableObject.CreateInstance<TraceForgeSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_settings), copy);
                Assert.AreEqual(_settings.MinVerbosity, copy.MinVerbosity);
                Assert.AreEqual(_settings.CategoryOverrides[0].Category, copy.CategoryOverrides[0].Category);
                Assert.AreEqual(_settings.CategoryOverrides[0].Verbosity, copy.CategoryOverrides[0].Verbosity);
                Assert.AreEqual(_settings.EnableRingBuffer, copy.EnableRingBuffer);
                Assert.AreEqual(_settings.RingBufferCapacity, copy.RingBufferCapacity);
                Assert.AreEqual(_settings.EnableFileSink, copy.EnableFileSink);
                Assert.AreEqual(_settings.FilePath, copy.FilePath);
                Assert.AreEqual(_settings.AppendToFile, copy.AppendToFile);
                Assert.AreEqual(_settings.FileQueueCapacity, copy.FileQueueCapacity);
                Assert.AreEqual(_settings.CaptureUnityLog, copy.CaptureUnityLog);
                Assert.AreEqual(_settings.StackTracePolicy, copy.StackTracePolicy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void NoSettingsInEditor_CreatesOneDefaultRingBuffer()
        {
            Bootstrap.EditorSettingsLoader = null;
            Bootstrap.Initialize();

            Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
            Assert.AreEqual(1024, SinkRegistry.RingBuffers[0].Capacity);
        }

        [Test]
        public void ConfiguredSettings_AreAppliedBeforeLogging()
        {
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.MinVerbosity = Verbosity.Debug;
            _settings.RingBufferCapacity = 17;
            _settings.CategoryOverrides = new[]
            {
                new TraceForgeSettings.CategoryOverride
                {
                    Category = "Network",
                    Verbosity = Verbosity.Warning
                }
            };
            Bootstrap.EditorSettingsLoader = () => _settings;

            Bootstrap.Initialize();

            Assert.AreEqual(Verbosity.Debug, Logger.GetMinVerbosity());
            Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
            Assert.AreEqual(17, SinkRegistry.RingBuffers[0].Capacity);
            TF.Debug(Categories.Network, "filtered");
            TF.Info("default category");
            TF.Warning(Categories.Network, "category override");
            Assert.AreEqual(2, SinkRegistry.RingBuffers[0].Count);
        }

        [Test]
        public void Shutdown_FlushesAndDisposesConfiguredFileSink()
        {
            string path = Path.Combine(Path.GetTempPath(), "traceforge-bootstrap-" + Guid.NewGuid().ToString("N") + ".log");
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.EnableRingBuffer = false;
            _settings.EnableFileSink = true;
            _settings.FilePath = path;
            _settings.AppendToFile = false;
            Bootstrap.EditorSettingsLoader = () => _settings;

            Bootstrap.Initialize();
            TF.Info("last entry");
            Bootstrap.Shutdown();

            StringAssert.Contains("last entry", File.ReadAllText(path));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
            }
            File.Delete(path);
        }

        [Test]
        public void InitializeAndShutdown_ThreeCycles_ReopensFileAndKeepsOneRing()
        {
            string path = Path.Combine(Path.GetTempPath(), "traceforge-cycles-" + Guid.NewGuid().ToString("N") + ".log");
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.EnableFileSink = true;
            _settings.FilePath = path;
            _settings.AppendToFile = false;
            Bootstrap.EditorSettingsLoader = () => _settings;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Bootstrap.Initialize();
                Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
                TF.Info("cycle " + cycle);
                Bootstrap.Shutdown();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                }
            }

            File.Delete(path);
        }

        [Test]
        public void ResetAndShutdown_DoNotDisposeUserSink()
        {
            var sink = new DisposableSink();
            TF.AddSink(sink);

            TF.Reset();
            Assert.IsFalse(sink.Disposed);

            Bootstrap.Shutdown();
            Assert.IsFalse(sink.Disposed);
        }

        [Test]
        public void ReentrantShutdown_DuringRingRegistration_LeavesNoOwnedSinks()
        {
            string path = Path.Combine(Path.GetTempPath(), "traceforge-reentrant-" + Guid.NewGuid().ToString("N") + ".log");
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.EnableFileSink = true;
            _settings.FilePath = path;
            _settings.AppendToFile = false;
            Bootstrap.EditorSettingsLoader = () => _settings;

            bool shutdownRequested = false;
            Action changed = null;
            changed = () =>
            {
                if (shutdownRequested)
                    return;
                shutdownRequested = true;
                Bootstrap.Shutdown();
            };

            try
            {
                SinkRegistry.Changed += changed;
                Bootstrap.Initialize();

                Assert.IsTrue(shutdownRequested);
                Assert.AreEqual(0, SinkRegistry.RingBuffers.Length);
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                SinkRegistry.Changed -= changed;
                Bootstrap.Shutdown();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private sealed class DisposableSink : ILogSink, IDisposable
        {
            public bool Disposed { get; private set; }
            public void Write(in LogEntry entry) { }
            public void Flush() { }
            public void Dispose() { Disposed = true; }
        }
    }
}
