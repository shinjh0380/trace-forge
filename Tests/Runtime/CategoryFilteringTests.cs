using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class CategoryFilteringTests
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
        public void CategoryVerbosity_CanBeSetHigherThanGlobal()
        {
            TF.SetMinVerbosity(Verbosity.Info);
            TF.SetCategoryVerbosity(Categories.Network, Verbosity.Error);

            TF.Info(Categories.Network, "filtered info");
            TF.Error(Categories.Network, "allowed error");

            Assert.AreEqual(1, _sink.Count);
            Assert.AreEqual(Verbosity.Error, _sink.Entries[0].Verbosity);
        }

        [Test]
        public void CategoryVerbosity_CanBeSetLowerThanGlobal()
        {
            TF.SetMinVerbosity(Verbosity.Info);
            TF.SetCategoryVerbosity(Categories.Network, Verbosity.Trace);

            TF.Trace(Categories.Network, "allowed trace");

            Assert.AreEqual(TF.IsTraceStripped ? 0 : 1, _sink.Count);
            if (!TF.IsTraceStripped)
                Assert.AreEqual(Verbosity.Trace, _sink.Entries[0].Verbosity);
        }

        [Test]
        public void CategoryVerbosity_Override_DoesNotAffectOtherCategories()
        {
            TF.SetCategoryVerbosity(Categories.Network, Verbosity.Off);
            TF.Info(Categories.Gameplay, "gameplay info");

            Assert.AreEqual(1, _sink.Count);
        }

        [Test]
        public void ClearCategoryVerbosity_RestoresGlobalFilter()
        {
            TF.SetMinVerbosity(Verbosity.Info);
            TF.SetCategoryVerbosity(Categories.Network, Verbosity.Trace);
            Assert.AreEqual(!TF.IsTraceStripped, TF.IsEnabled(Verbosity.Trace, Categories.Network));
            TF.ClearCategoryVerbosity(Categories.Network);

            Assert.IsFalse(TF.IsEnabled(Verbosity.Trace, Categories.Network));
            TF.Info(Categories.Network, "now visible");
            Assert.AreEqual(1, _sink.Count);
        }

        [Test]
        public void IsEnabled_WithCategory_RespectsOverride()
        {
            TF.SetCategoryVerbosity(Categories.AI, Verbosity.Fatal);
            Assert.IsFalse(TF.IsEnabled(Verbosity.Error, Categories.AI));
            Assert.IsTrue(TF.IsEnabled(Verbosity.Fatal, Categories.AI));
        }

        [Test]
        public void IsEnabled_WithCategory_ReturnsFalse_WhenNoSinksAreRegistered()
        {
            TF.ClearSinks();

            Assert.IsFalse(TF.IsEnabled(Verbosity.Debug, Categories.AI));
            Assert.IsFalse(TF.IsEnabled(Verbosity.Fatal, Categories.AI));
        }

        [Test]
        public void IsEnabled_WithCategory_TraceAndDebug_MatchStripProbes()
        {
            TF.SetMinVerbosity(Verbosity.Trace);

            Assert.AreEqual(!TF.IsTraceStripped, TF.IsEnabled(Verbosity.Trace, Categories.AI));
            Assert.AreEqual(!TF.IsDebugStripped, TF.IsEnabled(Verbosity.Debug, Categories.AI));
        }

        [Test]
        public void CustomCategory_Works()
        {
            var myCategory = new LogCategory("MySystem");
            TF.Info(myCategory, "custom category log");
            Assert.AreEqual(1, _sink.Count);
            Assert.AreEqual("MySystem", _sink.Entries[0].Category.Name);
        }

        [Test]
        public void Reset_ClearsCategoryOverrides()
        {
            TF.SetCategoryVerbosity(Categories.Network, Verbosity.Off);
            TF.Reset();
            _sink = new TestSink();
            TF.AddSink(_sink);

            TF.Info(Categories.Network, "should appear after reset");
            Assert.AreEqual(1, _sink.Count);
        }
    }
}
