using System;
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
    }
}
