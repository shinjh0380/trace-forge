using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TraceForge.Tests
{
    [TestFixture]
    public sealed class UnityLogCaptureTests
    {
        private sealed class TestSink : ILogSink
        {
            public readonly List<LogEntry> Entries = new List<LogEntry>();
            public int LastThreadId;
            public void Write(in LogEntry entry)
            {
                LastThreadId = Thread.CurrentThread.ManagedThreadId;
                lock (Entries) Entries.Add(entry);
            }
            public void Flush() { }
        }

        private TestSink _sink;
        private TraceForgeSettings _settings;
        private Func<TraceForgeSettings> _previousLoader;
        private int _mainThreadId;

        [SetUp]
        public void SetUp()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            UnityLogCapture.Stop();
            _previousLoader = Bootstrap.EditorSettingsLoader;
            TF.Reset();
            Logger.SetMinVerbosity(Verbosity.Trace);
            _sink = new TestSink();
            TF.AddSink(_sink);
            UnityLogCapture.Start();
        }

        [TearDown]
        public void TearDown()
        {
            Bootstrap.Shutdown();
            UnityLogCapture.Stop();
            TF.Reset();
            if (_settings != null)
                UnityEngine.Object.DestroyImmediate(_settings);
            Bootstrap.EditorSettingsLoader = _previousLoader;
        }

        [Test]
        public void StartAndStop_AreIdempotent()
        {
            UnityLogCapture.Start();
            UnityLogCapture.Start();
            LogAssert.Expect(LogType.Log, "idempotent");
            Debug.Log("idempotent");
            Assert.IsTrue(SpinWait.SpinUntil(() => _sink.Entries.Count == 1, 2000));
            UnityLogCapture.Stop();
            UnityLogCapture.Stop();
            LogAssert.Expect(LogType.Log, "after-stop");
            Debug.Log("after-stop");
            Assert.AreEqual(1, _sink.Entries.Count);
        }

        [Test]
        public void UnityMessages_MapToExpectedEntries()
        {
            const string log = "capture-log";
            const string warning = "capture-warning";
            const string error = "capture-error";
            const string assertion = "capture-assert";
            const string exception = "capture-exception";

            LogAssert.Expect(LogType.Log, log);
            Debug.Log(log);
            LogAssert.Expect(LogType.Warning, warning);
            Debug.LogWarning(warning);
            LogAssert.Expect(LogType.Error, error);
            Debug.LogError(error);
            LogAssert.Expect(LogType.Assert, assertion);
            Debug.LogAssertion(assertion);
            LogAssert.Expect(LogType.Exception, new Regex("Exception: " + exception));
            Debug.LogException(new Exception(exception));

            Assert.IsTrue(SpinWait.SpinUntil(() => _sink.Entries.Count >= 5, 2000));
            Assert.AreEqual(Verbosity.Debug, _sink.Entries[0].Verbosity);
            Assert.AreEqual(Verbosity.Warning, _sink.Entries[1].Verbosity);
            Assert.IsNotNull(_sink.Entries[1].StackTrace);
            Assert.AreEqual(Verbosity.Error, _sink.Entries[2].Verbosity);
            Assert.AreEqual(Verbosity.Error, _sink.Entries[3].Verbosity);
            Assert.AreEqual(Verbosity.Fatal, _sink.Entries[4].Verbosity);
            foreach (var entry in _sink.Entries)
                Assert.AreEqual(Categories.Unity, entry.Category);
        }

        [Test]
        public void CapturedStack_IsStoredExactly_WhenPolicyIsNone()
        {
            const string stack = "  at External.Call()\n    at External.Run()";
            Logger.SetStackTracePolicy(StackTracePolicy.None);
            Logger.WriteCaptured(Verbosity.Error, Categories.Unity, "raw", stack);

            Assert.AreSame(stack, _sink.Entries[0].StackTrace);
        }

        [Test]
        public void BootstrapCaptureSetting_TogglesSubscription()
        {
            _settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            _settings.EnableRingBuffer = false;
            _settings.MinVerbosity = Verbosity.Trace;
            _settings.CaptureUnityLog = false;
            _previousLoader = Bootstrap.EditorSettingsLoader;
            Bootstrap.EditorSettingsLoader = () => _settings;
            Bootstrap.Shutdown();
            Bootstrap.Initialize();
            LogAssert.Expect(LogType.Log, "capture-off");
            Debug.Log("capture-off");
            Thread.Sleep(100);
            Assert.IsFalse(_sink.Entries.Exists(entry => entry.Message == "capture-off"));

            _settings.CaptureUnityLog = true;
            Bootstrap.ApplySettings(_settings);
            LogAssert.Expect(LogType.Log, "capture-on");
            Debug.Log("capture-on");
            Assert.IsTrue(SpinWait.SpinUntil(() => _sink.Entries.Exists(entry => entry.Message == "capture-on"), 2000));
            _settings.CaptureUnityLog = false;
            Bootstrap.ApplySettings(_settings);
            LogAssert.Expect(LogType.Log, "capture-disabled-again");
            Debug.Log("capture-disabled-again");
            Assert.IsFalse(_sink.Entries.Exists(entry => entry.Message == "capture-disabled-again"));
            _settings.CaptureUnityLog = true;
            Bootstrap.ApplySettings(_settings);
            Bootstrap.Shutdown();
            LogAssert.Expect(LogType.Log, "after-shutdown");
            Debug.Log("after-shutdown");
            Assert.IsFalse(_sink.Entries.Exists(entry => entry.Message == "after-shutdown"));
        }

        [Test]
        public void RecursiveSinkLogging_IsGuarded()
        {
            var recursive = new RecursiveSink();
            TF.AddSink(recursive);
            LogAssert.Expect(LogType.Log, "recursive");
            UnityLogCapture.Start();
            Debug.Log("recursive");
            Assert.IsTrue(SpinWait.SpinUntil(() => recursive.Count > 0, 2000));
            Assert.AreEqual(1, recursive.Count);
        }

        [Test]
        public void WorkerThreadMessage_IsCapturedOnceOffMainThread()
        {
            const string message = "worker-capture";
            LogAssert.Expect(LogType.Log, message);
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { Debug.Log(message); }
                catch (Exception exception) { failure = exception; }
            });
            thread.Start();
            thread.Join();

            Assert.IsNull(failure);
            Assert.IsTrue(SpinWait.SpinUntil(() => _sink.Entries.Exists(e => e.Message == message), 2000));
            Assert.AreNotEqual(_mainThreadId, _sink.LastThreadId);
            Assert.AreEqual(thread.ManagedThreadId, _sink.LastThreadId);
            int count = 0;
            foreach (var entry in _sink.Entries)
                if (entry.Message == message) count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ContextOverloads_RecordLiveAndDestroyedInstanceIds()
        {
            var context = new GameObject("TraceForgeContext");
            try
            {
                TF.Error("live", context);
                int id = context.GetInstanceID();
                Assert.AreEqual(id, _sink.Entries[0].ContextInstanceId);
                UnityEngine.Object.DestroyImmediate(context);
                Assert.AreEqual(id, _sink.Entries[0].ContextInstanceId);
            }
            finally
            {
                if (context != null)
                    UnityEngine.Object.DestroyImmediate(context);
            }
        }

        private sealed class RecursiveSink : ILogSink
        {
            public int Count;
            public void Write(in LogEntry entry)
            {
                Count++;
                if (Count == 1)
                    typeof(UnityLogCapture).GetMethod("OnMessage", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { "recursive callback", "nested stack", LogType.Log });
            }
            public void Flush() { }
        }
    }
}
