using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using TraceForge.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public sealed class LogViewerWindowTests
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<TraceForgeLogViewerWindow> _windows = new List<TraceForgeLogViewerWindow>();
        private bool _previousPlayModeOptionsEnabled;
        private EnterPlayModeOptions _previousPlayModeOptions;

        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            _previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
        }

        [TearDown]
        public void TearDown()
        {
            TF.Reset();
            foreach (var window in _windows)
                if (window != null)
                    UnityEngine.Object.DestroyImmediate(window);
            _windows.Clear();
            EditorSettings.enterPlayModeOptionsEnabled = _previousPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = _previousPlayModeOptions;
        }

        [Test]
        public void RegisterRingBuffer_ReacquireDisplaysEntries()
        {
            var window = CreateWindow();
            var sink = new RingBufferSink();
            TF.AddSink(sink);
            TF.Info("visible");
            SetField(window, "_lastRefreshTime", EditorApplication.timeSinceStartup - 0.251d);
            Invoke(window, "Update");

            Assert.AreSame(sink, GetField<RingBufferSink>(window, "_ringBuffer"));
            Assert.AreEqual(1, GetField<LogEntry[]>(window, "_entries").Length);
        }

        [Test]
        public void RemoveOrReset_ReacquireClearsStaleSinkAndEntries()
        {
            var window = CreateWindow();
            var sink = new RingBufferSink();
            TF.AddSink(sink);
            TF.Info("before remove");
            SetField(window, "_lastRefreshTime", EditorApplication.timeSinceStartup - 0.251d);
            Invoke(window, "Update");

            TF.RemoveSink(sink);
            Invoke(window, "Update");
            Assert.IsNull(GetField<RingBufferSink>(window, "_ringBuffer"));
            Assert.AreEqual(0, GetField<LogEntry[]>(window, "_entries").Length);

            TF.AddSink(sink);
            Invoke(window, "Update");
            TF.Reset();
            Invoke(window, "Update");
            Assert.IsNull(GetField<RingBufferSink>(window, "_ringBuffer"));
        }

        [Test]
        public void TwoRingBuffers_AreAvailableToViewerAndSelectionChangesEntries()
        {
            var first = new RingBufferSink();
            var second = new RingBufferSink();
            var window = CreateWindow();
            TF.AddSink(first);
            TF.Info("first message");
            TF.AddSink(second);
            TF.Info("second message");
            Invoke(window, "Update");
            var buffers = GetField<RingBufferSink[]>(window, "_ringBuffers");
            Assert.AreEqual(2, buffers.Length);
            Assert.AreSame(first, GetField<RingBufferSink>(window, "_ringBuffer"));

            Invoke(window, "SelectRingBuffer", 1);
            Assert.AreSame(second, GetField<RingBufferSink>(window, "_ringBuffer"));
            Assert.AreEqual("second message", GetField<LogEntry[]>(window, "_entries")[0].Message);
            Invoke(window, "SelectRingBuffer", 0);
            Assert.AreEqual("first message", GetField<LogEntry[]>(window, "_entries")[0].Message);
        }

        [Test]
        public void PlayModeTransition_ReacquiresCurrentSessionSink()
        {
            var oldSink = new RingBufferSink();
            TF.AddSink(oldSink);
            var window = CreateWindow();
            Invoke(window, "Reacquire");

            TF.Reset();
            var currentSink = new RingBufferSink();
            TF.AddSink(currentSink);
            Invoke(window, "OnPlayModeStateChanged", PlayModeStateChange.EnteredPlayMode);

            Assert.AreSame(currentSink, GetField<RingBufferSink>(window, "_ringBuffer"));
        }

        [UnityTest]
        public IEnumerator EnterAndExitPlayMode_ReacquiresCurrentSessionSink()
        {
            var window = CreateWindow();
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            var oldSink = new RingBufferSink();
            TF.AddSink(oldSink);
            yield return null;
            yield return new EnterPlayMode();
            Assert.IsEmpty(SinkRegistry.RingBuffers, "SubsystemRegistration must clear the previous session.");
            TF.Reset();
            var currentSink = new RingBufferSink();
            TF.AddSink(currentSink);
            yield return null;
            Invoke(window, "Update");
            Assert.AreSame(currentSink, GetField<RingBufferSink>(window, "_ringBuffer"));
            yield return new ExitPlayMode();
            TF.Reset();
            Invoke(window, "Update");
            Assert.IsNull(GetField<RingBufferSink>(window, "_ringBuffer"));
        }

        [Test]
        public void RingBufferRegistry_ChangedRunsOutsideLoggerSinkLock()
        {
            var loggerType = typeof(TF).Assembly.GetType("TraceForge.Logger");
            var lockField = loggerType.GetField("_sinksLock", BindingFlags.Static | BindingFlags.NonPublic);
            var sinkLock = lockField.GetValue(null);
            var calls = 0;
            Action handler = () =>
            {
                calls++;
                Assert.IsFalse(Monitor.IsEntered(sinkLock), "Changed must run outside _sinksLock.");
            };
            SinkRegistry.Changed += handler;
            try
            {
                var sink = new RingBufferSink();
                TF.AddSink(sink);
                TF.RemoveSink(sink);
                TF.AddSink(sink);
                TF.ClearSinks();
                TF.AddSink(sink);
                TF.Reset();
                TF.AddSink(sink);
                loggerType.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Assert.AreEqual(8, calls);
                Assert.IsEmpty(SinkRegistry.RingBuffers);
            }
            finally
            {
                SinkRegistry.Changed -= handler;
            }
        }

        [Test]
        public void DuplicateRingBufferAdd_RemainsRegisteredUntilLastRemoval()
        {
            var sink = new RingBufferSink();
            TF.AddSink(sink);
            TF.AddSink(sink);
            Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
            Assert.IsTrue(TF.RemoveSink(sink));
            Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
            Assert.IsTrue(TF.RemoveSink(sink));
            Assert.AreEqual(0, SinkRegistry.RingBuffers.Length);
        }

        [Test]
        public void LoggerReset_FlushReentryOccursOutsideSinkLock()
        {
            var loggerType = typeof(TF).Assembly.GetType("TraceForge.Logger");
            var lockField = loggerType.GetField("_sinksLock", BindingFlags.Static | BindingFlags.NonPublic);
            var sinkLock = lockField.GetValue(null);
            var lockStates = new List<bool>();
            var reentrantSink = new ReentrantFlushSink(sinkLock, lockStates);
            TF.AddSink(reentrantSink);
            Action changed = () => lockStates.Add(Monitor.IsEntered(sinkLock));
            SinkRegistry.Changed += changed;
            try
            {
                InvokeStatic(loggerType, "Reset");
                Assert.IsTrue(reentrantSink.FlushCompleted);
                Assert.IsNotEmpty(lockStates);
                Assert.IsTrue(lockStates.TrueForAll(state => !state));
                Assert.AreEqual(0, SinkRegistry.RingBuffers.Length);
                Assert.AreEqual(Verbosity.Debug, InvokeStaticResult(loggerType, "GetMinVerbosity"));
            }
            finally
            {
                SinkRegistry.Changed -= changed;
                TF.ClearSinks();
            }
        }

        private TraceForgeLogViewerWindow CreateWindow()
        {
            var window = ScriptableObject.CreateInstance<TraceForgeLogViewerWindow>();
            _windows.Add(window);
            return window;
        }

        private static void Invoke(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, InstanceFlags);
            Assert.IsNotNull(method, "Expected Viewer method was not found: " + name);
            method.Invoke(instance, args);
        }

        private static void InvokeStatic(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected Logger method was not found: " + name);
            method.Invoke(null, null);
        }

        private static object InvokeStaticResult(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Expected Logger method was not found: " + name);
            return method.Invoke(null, null);
        }

        private sealed class ReentrantFlushSink : ILogSink
        {
            private readonly object _sinkLock;
            private readonly List<bool> _lockStates;
            public bool FlushCompleted { get; private set; }

            public ReentrantFlushSink(object sinkLock, List<bool> lockStates)
            {
                _sinkLock = sinkLock;
                _lockStates = lockStates;
            }

            public void Write(in LogEntry entry) { }

            public void Flush()
            {
                _lockStates.Add(Monitor.IsEntered(_sinkLock));
                TF.AddSink(new RingBufferSink());
                FlushCompleted = true;
            }
        }

        private static T GetField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, InstanceFlags);
            Assert.IsNotNull(field, "Expected Viewer field was not found: " + name);
            return (T)field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, InstanceFlags);
            Assert.IsNotNull(field, "Expected Viewer field was not found: " + name);
            field.SetValue(instance, value);
        }
    }
}
