using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public sealed class StackTraceTests
    {
        private sealed class TestSink : ILogSink
        {
            public readonly List<LogEntry> Entries = new List<LogEntry>();
            public void Write(in LogEntry entry) => Entries.Add(entry);
            public void Flush() { }
        }

        private TestSink _sink;

        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            _sink = new TestSink();
            TF.AddSink(_sink);
        }

        [TearDown]
        public void TearDown() => TF.Reset();

        [Test]
        public void None_DoesNotCaptureStackTrace()
        {
            Logger.SetStackTracePolicy(StackTracePolicy.None);

            TF.Error("error");

            Assert.IsNull(_sink.Entries[0].StackTrace);
        }

        [Test]
        public void ErrorAndAbove_CapturesOnlyErrorAndAbove()
        {
            Logger.SetStackTracePolicy(StackTracePolicy.ErrorAndAbove);

            TF.Info("info");
            TF.Error("error");

            Assert.IsNull(_sink.Entries[0].StackTrace);
            Assert.IsNotNull(_sink.Entries[1].StackTrace);
            AssertFirstCallerFrame(
                _sink.Entries[1].StackTrace,
                "StackTraceTests.ErrorAndAbove_CapturesOnlyErrorAndAbove");
        }

        [Test]
        public void All_CapturesInfoStackTrace()
        {
            Logger.SetStackTracePolicy(StackTracePolicy.All);

            TF.Info("info");

            Assert.IsNotNull(_sink.Entries[0].StackTrace);
            AssertFirstCallerFrame(_sink.Entries[0].StackTrace, "StackTraceTests.All_CapturesInfoStackTrace");
        }

        private static void AssertFirstCallerFrame(string stackTrace, string expectedMethod)
        {
            string[] lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.IsNotEmpty(lines);
            StringAssert.Contains("TraceForge.Tests." + expectedMethod, lines[0]);
            StringAssert.DoesNotContain("TraceForge.Logger.", lines[0]);
            StringAssert.DoesNotContain("TraceForge.TF.", lines[0]);
        }
    }
}
