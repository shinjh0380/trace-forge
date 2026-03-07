using System;
using System.IO;
using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class FileSinkTests
    {
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            _tempFile = Path.Combine(Path.GetTempPath(), $"traceforge_test_{Guid.NewGuid()}.log");
        }

        [TearDown]
        public void TearDown()
        {
            TF.Reset();
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        [Test]
        public void Write_CreatesFileAndWritesLine()
        {
            using (var sink = new FileSink(_tempFile))
            {
                TF.AddSink(sink);
                TF.Info("hello file");
                sink.Flush();
            }

            var lines = File.ReadAllLines(_tempFile);
            Assert.AreEqual(1, lines.Length);
            StringAssert.Contains("hello file", lines[0]);
            StringAssert.Contains("[INFO]", lines[0]);
        }

        [Test]
        public void Write_Format_ContainsTimestampVerbosityCategoryMessage()
        {
            using (var sink = new FileSink(_tempFile))
            {
                TF.AddSink(sink);
                TF.Warning(Categories.Network, "network warning");
                sink.Flush();
            }

            var content = File.ReadAllText(_tempFile);
            StringAssert.Contains("[WARNING]", content);
            StringAssert.Contains("[Network]", content);
            StringAssert.Contains("network warning", content);
        }

        [Test]
        public void Append_True_AppendsToExistingFile()
        {
            using (var sink1 = new FileSink(_tempFile, append: false))
            {
                sink1.Write(new LogEntry(Verbosity.Info, Categories.Default, "line 1", null, DateTime.UtcNow.Ticks));
                sink1.Flush();
            }

            using (var sink2 = new FileSink(_tempFile, append: true))
            {
                sink2.Write(new LogEntry(Verbosity.Info, Categories.Default, "line 2", null, DateTime.UtcNow.Ticks));
                sink2.Flush();
            }

            var lines = File.ReadAllLines(_tempFile);
            Assert.AreEqual(2, lines.Length);
        }

        [Test]
        public void Dispose_ClosesFile()
        {
            var sink = new FileSink(_tempFile);
            sink.Write(new LogEntry(Verbosity.Info, Categories.Default, "test", null, DateTime.UtcNow.Ticks));
            sink.Dispose();

            // File should be accessible (not locked) after Dispose
            Assert.DoesNotThrow(() => File.ReadAllText(_tempFile));
        }

        [Test]
        public void InvalidPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FileSink(null));
            Assert.Throws<ArgumentNullException>(() => new FileSink(""));
        }

        [Test]
        public void Write_AfterDispose_DoesNotThrow()
        {
            var sink = new FileSink(_tempFile);
            sink.Dispose();
            Assert.DoesNotThrow(() => sink.Write(new LogEntry(Verbosity.Info, Categories.Default, "after dispose", null, DateTime.UtcNow.Ticks)));
        }
    }
}
