using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class SinkDispatchTests
    {
        private sealed class TestSink : ILogSink
        {
            public readonly System.Collections.Generic.List<LogEntry> Entries = new System.Collections.Generic.List<LogEntry>();
            public void Write(in LogEntry entry) { Entries.Add(entry); }
            public void Flush() { }
            public int Count => Entries.Count;
        }

        private sealed class ThrowingSink : ILogSink
        {
            public void Write(in LogEntry entry) => throw new InvalidOperationException("sink error");
            public void Flush() { }
        }

        private sealed class ThrowingErrorWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
            public override void Write(char value) => throw new IOException("stderr failure");
        }

        private sealed class BoundedRecursiveSink : ILogSink
        {
            private readonly bool _captured;
            private int _depth;
            public int Calls;
            public int MaxDepth;

            public BoundedRecursiveSink(bool captured) { _captured = captured; }

            public void Write(in LogEntry entry)
            {
                Calls++;
                _depth++;
                MaxDepth = Math.Max(MaxDepth, _depth);
                try
                {
                    // Bound the pre-fix reproduction: never cause a real stack overflow.
                    if (_depth < 3)
                        WriteVia(_captured, "nested");
                }
                finally { _depth--; }
            }

            public void Flush() { }
        }

        private sealed class BlockingSink : ILogSink
        {
            public readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            public int Calls;

            public void Write(in LogEntry entry)
            {
                Interlocked.Increment(ref Calls);
                if (entry.Message == "blocked")
                {
                    Entered.Set();
                    if (!Release.Wait(5000))
                        throw new TimeoutException("Test did not release the original dispatch.");
                }
            }

            public void Flush() { }
        }

        [SetUp]
        public void SetUp() => TF.Reset();

        [TearDown]
        public void TearDown() => TF.Reset();

        [Test]
        public void AddSink_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TF.AddSink(null));
        }

        [Test]
        public void AddSink_DispatchesToSink()
        {
            var sink = new TestSink();
            TF.AddSink(sink);
            TF.Info("hello");
            Assert.AreEqual(1, sink.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyMessage_IsDispatchedUnchanged(bool captured)
        {
            var sink = new TestSink();
            TF.AddSink(sink);

            WriteVia(captured, string.Empty);

            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual(string.Empty, sink.Entries[0].Message);
        }

        [Test]
        public void RemoveSink_StopsDispatch()
        {
            var sink = new TestSink();
            TF.AddSink(sink);
            TF.RemoveSink(sink);
            TF.Info("not dispatched");
            Assert.AreEqual(0, sink.Count);
        }

        [Test]
        public void MultipleSinks_AllReceiveEntry()
        {
            var sink1 = new TestSink();
            var sink2 = new TestSink();
            TF.AddSink(sink1);
            TF.AddSink(sink2);
            TF.Info("broadcast");
            Assert.AreEqual(1, sink1.Count);
            Assert.AreEqual(1, sink2.Count);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void NestedWrite_IsDroppedAndOriginalReachesRemainingSinks(bool capturedOuter, bool capturedNested)
        {
            var recursive = new BoundedRecursiveSink(capturedNested);
            var good = new TestSink();
            TF.AddSink(recursive);
            TF.AddSink(good);

            WriteVia(capturedOuter, "original");

            TestContext.WriteLine("REENTRY calls={0} maxDepth={1}", recursive.Calls, recursive.MaxDepth);
            Assert.AreEqual(1, recursive.Calls);
            Assert.AreEqual(1, recursive.MaxDepth);
            Assert.AreEqual(1, good.Count);
            Assert.AreEqual("original", good.Entries[0].Message);
            if (capturedOuter)
                Assert.AreEqual("supplied stack", good.Entries[0].StackTrace);

            TF.Info("next");
            Assert.AreEqual(2, recursive.Calls, "The guard must be released after dispatch.");
            Assert.AreEqual(2, good.Count);
        }

        [Test]
        public void ContextWrite_NestedWriteIsDropped()
        {
            var recursive = new BoundedRecursiveSink(false);
            var good = new TestSink();
            TF.AddSink(recursive);
            TF.AddSink(good);

            Logger.Write(Verbosity.Info, Categories.Default, "context", null, 123);

            Assert.AreEqual(1, recursive.Calls);
            Assert.AreEqual(1, good.Count);
            Assert.AreEqual(123, good.Entries[0].ContextInstanceId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingSinkAndErrorWriter_GuardRecoversForNextWrite(bool captured)
        {
            var originalError = Console.Error;
            var good = new TestSink();
            using (var throwingError = new ThrowingErrorWriter())
            {
                try
                {
                    Console.SetError(throwingError);
                    TF.AddSink(new ThrowingSink());
                    TF.AddSink(good);
                    WriteVia(captured, "first");
                    WriteVia(captured, "second");
                    Assert.AreEqual(2, good.Count);
                    Assert.AreEqual("second", good.Entries[1].Message);
                }
                finally { Console.SetError(originalError); }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConcurrentWrite_IsNotDroppedWhileAnotherThreadDispatches(bool captured)
        {
            var blocking = new BlockingSink();
            var good = new RingBufferSink();
            TF.AddSink(blocking);
            TF.AddSink(good);
            var thread = new Thread(() => TF.Info("blocked"));
            thread.Start();
            try
            {
                Assert.IsTrue(blocking.Entered.Wait(5000), "Worker must enter dispatch first.");
                WriteVia(captured, "independent");
                Assert.AreEqual(2, Volatile.Read(ref blocking.Calls));
                Assert.AreEqual(1, good.Count, "Independent thread must reach the next sink before release.");
            }
            finally
            {
                blocking.Release.Set();
                Assert.IsTrue(thread.Join(5000));
                blocking.Entered.Dispose();
                blocking.Release.Dispose();
            }
            Assert.AreEqual(2, good.Count);
        }

        private static void WriteVia(bool captured, string message)
        {
            if (captured)
                Logger.WriteCaptured(Verbosity.Info, Categories.Unity, message, "supplied stack");
            else
                TF.Info(message);
        }

        [Test]
        public void ThrowingSink_DoesNotPreventOtherSinks()
        {
            var good = new TestSink();
            TF.AddSink(new ThrowingSink());
            TF.AddSink(good);
            Assert.DoesNotThrow(() => TF.Info("should not throw"));
            Assert.AreEqual(1, good.Count);
        }

        [Test]
        public void ThrowingSink_ReportsFailureToStandardError()
        {
            var originalError = Console.Error;
            var capturedError = new StringWriter();

            try
            {
                Console.SetError(capturedError);
                TF.AddSink(new ThrowingSink());

                TF.Info("trigger failure");

                StringAssert.Contains("ThrowingSink", capturedError.ToString());
                StringAssert.Contains("sink error", capturedError.ToString());
            }
            finally
            {
                Console.SetError(originalError);
                capturedError.Dispose();
            }
        }

        [Test]
        public void ThrowingStandardError_DoesNotEscapeOrPreventOtherSinks()
        {
            var originalError = Console.Error;
            var throwingError = new ThrowingErrorWriter();
            var good = new TestSink();

            try
            {
                Console.SetError(throwingError);
                TF.AddSink(new ThrowingSink());
                TF.AddSink(good);

                Assert.DoesNotThrow(() => TF.Info("stderr failure must stay isolated"));
                Assert.AreEqual(1, good.Count);
            }
            finally
            {
                Console.SetError(originalError);
                throwingError.Dispose();
            }
        }

        [Test]
        public void LogEntry_ContainsCorrectData()
        {
            var sink = new TestSink();
            TF.AddSink(sink);
            TF.Warning(Categories.Network, "test message");

            var entry = sink.Entries[0];
            Assert.AreEqual(Verbosity.Warning, entry.Verbosity);
            Assert.AreEqual("Network", entry.Category.Name);
            Assert.AreEqual("test message", entry.Message);
            Assert.IsNull(entry.Exception);
            Assert.Greater(entry.TimestampTicks, 0);
        }

        [Test]
        public void ErrorWithException_SetsExceptionOnEntry()
        {
            var sink = new TestSink();
            TF.AddSink(sink);
            var ex = new InvalidOperationException("test exception");
            TF.Error("error with ex", ex);

            Assert.AreEqual(ex, sink.Entries[0].Exception);
        }

        [Test]
        public void ClearSinks_RemovesAllSinks()
        {
            var sink = new TestSink();
            TF.AddSink(sink);
            TF.ClearSinks();
            TF.Info("not dispatched");
            Assert.AreEqual(0, sink.Count);
        }

        [Test]
        public void RuntimeAndEditorSources_ContainNoUnityDebugLogCalls()
        {
            var packageRoot = FindPackageRoot();
            var forbidden = new[] { "Debug.Log", "Debug.LogWarning", "Debug.LogError", "Debug.LogException", "Debug.LogFormat" };
            foreach (var directoryName in new[] { "Runtime", "Editor" })
            {
                var directory = Path.Combine(packageRoot, directoryName);
                foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    var source = File.ReadAllText(file);
                    foreach (var token in forbidden)
                        Assert.IsFalse(source.Contains(token), "Forbidden Unity logging call in " + file + ": " + token);
                }
            }
        }

        private static string FindPackageRoot()
        {
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TF).Assembly);
            Assert.IsNotNull(packageInfo, "TraceForge package source must be available in the Editor host.");
            return packageInfo.resolvedPath;
#else
            Assert.Ignore("TraceForge package source scan runs only in the Editor.");
            return null;
#endif
        }
    }
}
