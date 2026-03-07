using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class VerbosityFilteringTests
    {
        private sealed class TestSink : ILogSink
        {
            public readonly System.Collections.Generic.List<LogEntry> Entries = new System.Collections.Generic.List<LogEntry>();
            public void Write(in LogEntry entry) { Entries.Add(entry); }
            public void Flush() { }
            public int Count => Entries.Count;
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
        public void DefaultMinVerbosity_IsDebug()
        {
            Assert.IsTrue(TF.IsEnabled(Verbosity.Debug));
            Assert.IsFalse(TF.IsEnabled(Verbosity.Trace));
        }

        [Test]
        public void SetMinVerbosity_Info_FiltersDebugAndTrace()
        {
            TF.SetMinVerbosity(Verbosity.Info);
            TF.Debug("debug msg");
            TF.Trace("trace msg");
            Assert.AreEqual(0, _sink.Count);
        }

        [Test]
        public void SetMinVerbosity_Info_AllowsInfoAndAbove()
        {
            TF.SetMinVerbosity(Verbosity.Info);
            TF.Info("info");
            TF.Warning("warn");
            TF.Error("error");
            TF.Fatal("fatal");
            Assert.AreEqual(4, _sink.Count);
        }

        [Test]
        public void SetMinVerbosity_Off_BlocksEverything()
        {
            TF.SetMinVerbosity(Verbosity.Off);
            TF.Fatal("fatal");
            Assert.AreEqual(0, _sink.Count);
        }

        [Test]
        public void IsEnabled_ReturnsFalse_WhenBelowMinVerbosity()
        {
            TF.SetMinVerbosity(Verbosity.Warning);
            Assert.IsFalse(TF.IsEnabled(Verbosity.Info));
            Assert.IsTrue(TF.IsEnabled(Verbosity.Warning));
            Assert.IsTrue(TF.IsEnabled(Verbosity.Error));
        }

        [Test]
        public void Reset_RestoresDefaultMinVerbosity()
        {
            TF.SetMinVerbosity(Verbosity.Fatal);
            TF.Reset();
            _sink = new TestSink();
            TF.AddSink(_sink);
            TF.Debug("should appear");
            Assert.AreEqual(1, _sink.Count);
        }
    }
}
