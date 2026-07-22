using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Profiling;

namespace TraceForge.Tests
{
    [TestFixture]
    public class FileSinkTests
    {
        private sealed class BlockingTextWriter : TextWriter
        {
            private readonly ManualResetEventSlim _writeStarted = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

            public override Encoding Encoding => Encoding.UTF8;

            public bool WaitUntilWriteStarts(int millisecondsTimeout) => _writeStarted.Wait(millisecondsTimeout);

            public void Release() => _release.Set();

            public override void Write(char value)
            {
                _writeStarted.Set();
                _release.Wait();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _release.Set();
                    _writeStarted.Dispose();
                    _release.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        private sealed class ThrowingTextWriter : TextWriter
        {
            private readonly ManualResetEventSlim _writeStarted = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _releaseFailure = new ManualResetEventSlim(false);
            private int _flushCallCount;

            public override Encoding Encoding => Encoding.UTF8;

            public int FlushCallCount => Volatile.Read(ref _flushCallCount);

            public bool WaitUntilWriteStarts(int millisecondsTimeout) => _writeStarted.Wait(millisecondsTimeout);

            public void ReleaseFailure() => _releaseFailure.Set();

            public override void Write(char value)
            {
                _writeStarted.Set();
                _releaseFailure.Wait();
                throw new IOException("simulated writer failure");
            }

            public override void Flush()
            {
                Interlocked.Increment(ref _flushCallCount);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _releaseFailure.Set();
                    _writeStarted.Dispose();
                    _releaseFailure.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        private string _tempFile;

        private static LogEntry CreateEntry(string message)
        {
            return new LogEntry(Verbosity.Info, Categories.Default, message, null, DateTime.UtcNow.Ticks);
        }

        private static string ExtractMessage(string line)
        {
            const string marker = "] ";
            int markerIndex = line.LastIndexOf(marker, StringComparison.Ordinal);
            return markerIndex >= 0 ? line.Substring(markerIndex + marker.Length) : line;
        }

        private static string[] ReadAllLinesWhileWriterOpen(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var lines = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);

                return lines.ToArray();
            }
        }

        private static Task StartLongRunningTask(Action action)
        {
            return Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

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

        [Test]
        public void QueueCapacity_ZeroOrNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FileSink(_tempFile, false, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FileSink(_tempFile, false, -1));
        }

        [Test]
        public void Write_UncontendedProducerPath_ProducesZeroGcAllocEvents()
        {
            const int warmupCount = 1000;
            const int entryCount = 100000;
            var entry = CreateEntry("allocation regression");

            using (var sink = new FileSink(TextWriter.Null, entryCount + 1024))
            {
                for (int i = 0; i < warmupCount; i++)
                    sink.Write(in entry);

                sink.Flush();

                using (var recorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal,
                    "GC.Alloc",
                    1024,
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    Assert.IsTrue(recorder.Valid);
                    Assert.IsTrue(recorder.IsRunning);
                    Assert.Less(recorder.Count, recorder.Capacity);

                    long countBefore = recorder.Count;

                    for (int i = 0; i < entryCount; i++)
                        sink.Write(in entry);

                    long allocationSamples = recorder.Count - countBefore;

                    sink.Flush();
                    TestContext.Out.WriteLine(
                        $"GC_ALLOC_RESULT countBefore={countBefore} capacity={recorder.Capacity} allocationSamples={allocationSamples}");
                    Assert.AreEqual(0, allocationSamples);
                }
            }
        }

        [Test]
        public void Flush_WritesEveryEntryInEnqueueOrder()
        {
            const int count = 256;

            using (var sink = new FileSink(_tempFile, false, 8))
            {
                for (int i = 0; i < count; i++)
                    sink.Write(CreateEntry($"message-{i:D4}"));

                sink.Flush();

                var lines = ReadAllLinesWhileWriterOpen(_tempFile);
                Assert.AreEqual(count, lines.Length);
                for (int i = 0; i < count; i++)
                    Assert.AreEqual($"message-{i:D4}", ExtractMessage(lines[i]));
            }
        }

        [Test]
        public void Write_WhenQueueIsFull_BlocksUntilWriterReleasesCapacity()
        {
            var writer = new BlockingTextWriter();
            var writeAttemptStarted = new ManualResetEventSlim(false);
            FileSink sink = null;
            Task firstWriteTask = null;
            Task secondWriteTask = null;
            Task thirdWriteTask = null;
            Task disposeTask = null;
            bool writerReleased = false;

            try
            {
                sink = new FileSink(writer, 1);

                firstWriteTask = StartLongRunningTask(() => sink.Write(CreateEntry("first")));
                Assert.IsTrue(writer.WaitUntilWriteStarts(2000), "Writer did not start within two seconds.");
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => firstWriteTask.IsCompleted, 2000),
                    "First Write did not complete within two seconds after enqueueing.");
                Assert.IsFalse(firstWriteTask.IsFaulted, firstWriteTask.Exception?.ToString());
                Assert.AreEqual(TaskStatus.RanToCompletion, firstWriteTask.Status);

                secondWriteTask = StartLongRunningTask(() => sink.Write(CreateEntry("second")));
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => secondWriteTask.IsCompleted, 2000),
                    "Second Write did not complete within two seconds while capacity was available.");
                Assert.IsFalse(secondWriteTask.IsFaulted, secondWriteTask.Exception?.ToString());
                Assert.AreEqual(TaskStatus.RanToCompletion, secondWriteTask.Status);

                thirdWriteTask = StartLongRunningTask(
                    () =>
                    {
                        writeAttemptStarted.Set();
                        sink.Write(CreateEntry("third"));
                    });

                Assert.IsTrue(writeAttemptStarted.Wait(2000), "Third writer did not start within two seconds.");
                Assert.IsFalse(
                    SpinWait.SpinUntil(() => thirdWriteTask.IsCompleted, 100),
                    "Write completed while the queue was full.");

                writer.Release();
                writerReleased = true;
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => thirdWriteTask.IsCompleted, 2000),
                    "Write remained blocked after capacity was released.");
                Assert.IsFalse(thirdWriteTask.IsFaulted, thirdWriteTask.Exception?.ToString());
                Assert.AreEqual(TaskStatus.RanToCompletion, thirdWriteTask.Status);

                disposeTask = StartLongRunningTask(() => sink.Dispose());
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000),
                    "Dispose did not complete within two seconds.");
                Assert.IsFalse(disposeTask.IsFaulted, disposeTask.Exception?.ToString());
                Assert.AreEqual(TaskStatus.RanToCompletion, disposeTask.Status);
            }
            finally
            {
                if (!writerReleased)
                {
                    writer.Release();
                    writerReleased = true;
                }

                if (firstWriteTask != null)
                    SpinWait.SpinUntil(() => firstWriteTask.IsCompleted, 2000);
                if (secondWriteTask != null)
                    SpinWait.SpinUntil(() => secondWriteTask.IsCompleted, 2000);
                if (thirdWriteTask != null)
                    SpinWait.SpinUntil(() => thirdWriteTask.IsCompleted, 2000);

                if (sink != null && disposeTask == null)
                    disposeTask = StartLongRunningTask(() => sink.Dispose());
                if (disposeTask != null)
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000);

                if (sink == null)
                    writer.Dispose();
                if (thirdWriteTask == null || thirdWriteTask.IsCompleted || writeAttemptStarted.IsSet)
                    writeAttemptStarted.Dispose();
            }
        }

        [Test]
        public void Dispose_DrainsAcceptedEntriesBeforeClosingFile()
        {
            const int count = 1000;

            using (var sink = new FileSink(_tempFile, false, 4))
            {
                for (int i = 0; i < count; i++)
                    sink.Write(CreateEntry($"dispose-{i:D4}"));
            }

            var lines = File.ReadAllLines(_tempFile);
            Assert.AreEqual(count, lines.Length);
            for (int i = 0; i < count; i++)
                Assert.AreEqual($"dispose-{i:D4}", ExtractMessage(lines[i]));
        }

        [Test]
        public void MultipleProducers_WriteWithoutLossOrDuplication()
        {
            const int producerCount = 4;
            const int entriesPerProducer = 1000;
            const int expectedCount = producerCount * entriesPerProducer;

            using (var sink = new FileSink(_tempFile, false, 32))
            {
                Parallel.For(0, producerCount, producer =>
                {
                    for (int i = 0; i < entriesPerProducer; i++)
                        sink.Write(CreateEntry($"P{producer}:{i:D4}"));
                });

                sink.Flush();

                var lines = ReadAllLinesWhileWriterOpen(_tempFile);
                Assert.AreEqual(expectedCount, lines.Length);

                var messages = new HashSet<string>();
                foreach (var line in lines)
                {
                    var message = ExtractMessage(line);
                    Assert.IsTrue(messages.Add(message), $"Duplicate message: {message}");
                }

                for (int producer = 0; producer < producerCount; producer++)
                {
                    for (int i = 0; i < entriesPerProducer; i++)
                    {
                        var expected = $"P{producer}:{i:D4}";
                        Assert.IsTrue(messages.Contains(expected), $"Missing message: {expected}");
                    }
                }
            }
        }

        [Test]
        public void WriterFailure_UnblocksWriteWaitingOnFullQueueWithIOException()
        {
            var writer = new ThrowingTextWriter();
            var blockedWriteStarted = new ManualResetEventSlim(false);
            FileSink sink = null;
            Task firstWriteTask = null;
            Task secondWriteTask = null;
            Task blockedWriteTask = null;
            Task disposeTask = null;
            bool failureReleased = false;

            try
            {
                sink = new FileSink(writer, 1);

                firstWriteTask = StartLongRunningTask(() => sink.Write(CreateEntry("first fails")));
                Assert.IsTrue(writer.WaitUntilWriteStarts(2000), "Writer did not start within two seconds.");
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => firstWriteTask.IsCompleted, 2000),
                    "First Write did not complete within two seconds after enqueueing.");
                Assert.IsFalse(firstWriteTask.IsFaulted, firstWriteTask.Exception?.ToString());

                secondWriteTask = StartLongRunningTask(() => sink.Write(CreateEntry("fills queue")));
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => secondWriteTask.IsCompleted, 2000),
                    "Second Write did not fill the available queue slot within two seconds.");
                Assert.IsFalse(secondWriteTask.IsFaulted, secondWriteTask.Exception?.ToString());

                blockedWriteTask = StartLongRunningTask(
                    () =>
                    {
                        blockedWriteStarted.Set();
                        sink.Write(CreateEntry("blocked by full queue"));
                    });
                Assert.IsTrue(blockedWriteStarted.Wait(2000), "Blocked writer did not start within two seconds.");
                Assert.IsFalse(
                    SpinWait.SpinUntil(() => blockedWriteTask.IsCompleted, 100),
                    "Write completed while the queue was full.");

                writer.ReleaseFailure();
                failureReleased = true;

                Assert.IsTrue(
                    SpinWait.SpinUntil(() => blockedWriteTask.IsCompleted, 2000),
                    "Blocked Write did not complete within two seconds after the writer failed.");
                Assert.IsTrue(blockedWriteTask.IsFaulted, "Blocked Write completed without the writer failure.");
                var writeException = blockedWriteTask.Exception.GetBaseException();
                Assert.IsInstanceOf<IOException>(writeException);
                StringAssert.Contains("simulated writer failure", writeException.ToString());

                disposeTask = StartLongRunningTask(() => sink.Dispose());
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000),
                    "Dispose did not complete within two seconds after the writer failed.");
                Assert.IsTrue(disposeTask.IsFaulted, "Dispose completed without the writer failure.");
                StringAssert.Contains("simulated writer failure", disposeTask.Exception.GetBaseException().ToString());
            }
            finally
            {
                if (!failureReleased)
                    writer.ReleaseFailure();

                if (firstWriteTask != null)
                    SpinWait.SpinUntil(() => firstWriteTask.IsCompleted, 2000);
                if (secondWriteTask != null)
                    SpinWait.SpinUntil(() => secondWriteTask.IsCompleted, 2000);
                if (blockedWriteTask != null)
                    SpinWait.SpinUntil(() => blockedWriteTask.IsCompleted, 2000);

                if (sink != null && disposeTask == null)
                {
                    disposeTask = StartLongRunningTask(
                        () =>
                        {
                            try { sink.Dispose(); }
                            catch (IOException) { }
                        });
                }

                if (disposeTask != null)
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000);

                if (sink == null)
                    writer.Dispose();
                if (blockedWriteTask == null || blockedWriteTask.IsCompleted || blockedWriteStarted.IsSet)
                    blockedWriteStarted.Dispose();
            }
        }

        [Test]
        public void WriterFailure_UnblocksFlushAndDisposeWithIOException()
        {
            var writer = new ThrowingTextWriter();
            var flushAttemptStarted = new ManualResetEventSlim(false);
            FileSink sink = null;
            Task enqueueTask = null;
            Task flushTask = null;
            Task futureWriteTask = null;
            Task disposeTask = null;
            bool failureReleased = false;

            try
            {
                sink = new FileSink(writer, 4);

                enqueueTask = StartLongRunningTask(() => sink.Write(CreateEntry("will fail")));
                Assert.IsTrue(writer.WaitUntilWriteStarts(2000), "Writer did not start within two seconds.");
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => enqueueTask.IsCompleted, 2000),
                    "Initial Write did not complete within two seconds after enqueueing.");
                Assert.IsFalse(enqueueTask.IsFaulted, enqueueTask.Exception?.ToString());
                Assert.AreEqual(TaskStatus.RanToCompletion, enqueueTask.Status);

                flushTask = StartLongRunningTask(
                    () =>
                    {
                        flushAttemptStarted.Set();
                        sink.Flush();
                    });
                Assert.IsTrue(flushAttemptStarted.Wait(2000), "Flush did not start within two seconds.");
                Assert.IsFalse(
                    SpinWait.SpinUntil(() => flushTask.IsCompleted, 100),
                    "Flush completed before the writer failure was released.");

                writer.ReleaseFailure();
                failureReleased = true;
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => flushTask.IsCompleted, 2000),
                    "Flush did not complete within two seconds after the writer failed.");
                Assert.IsTrue(flushTask.IsFaulted, "Flush completed without an exception after the writer failed.");
                var flushException = flushTask.Exception.GetBaseException();
                Assert.IsInstanceOf<IOException>(flushException);
                StringAssert.Contains("simulated writer failure", flushException.ToString());
                Assert.AreEqual(0, writer.FlushCallCount, "Underlying Flush ran after the writer failure was recorded.");

                futureWriteTask = StartLongRunningTask(() => sink.Write(CreateEntry("after failure")));
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => futureWriteTask.IsCompleted, 2000),
                    "Write did not complete within two seconds after the writer failed.");
                Assert.IsTrue(futureWriteTask.IsFaulted, "Write completed without an exception after the writer failed.");
                var writeException = futureWriteTask.Exception.GetBaseException();
                Assert.IsInstanceOf<IOException>(writeException);
                StringAssert.Contains("simulated writer failure", writeException.ToString());

                disposeTask = StartLongRunningTask(() => sink.Dispose());
                Assert.IsTrue(
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000),
                    "Dispose did not complete within two seconds after the writer failed.");
                Assert.IsTrue(disposeTask.IsFaulted, "Dispose completed without an exception after the writer failed.");
                var disposeException = disposeTask.Exception.GetBaseException();
                Assert.IsInstanceOf<IOException>(disposeException);
                StringAssert.Contains("simulated writer failure", disposeException.ToString());
            }
            finally
            {
                if (!failureReleased)
                {
                    writer.ReleaseFailure();
                    failureReleased = true;
                }

                if (enqueueTask != null)
                    SpinWait.SpinUntil(() => enqueueTask.IsCompleted, 2000);
                if (flushTask != null)
                    SpinWait.SpinUntil(() => flushTask.IsCompleted, 2000);
                if (futureWriteTask != null)
                    SpinWait.SpinUntil(() => futureWriteTask.IsCompleted, 2000);
                if (disposeTask != null)
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000);

                if (sink == null)
                {
                    writer.Dispose();
                }
                else if (disposeTask == null && (flushTask == null || flushTask.IsCompleted))
                {
                    disposeTask = StartLongRunningTask(() =>
                    {
                        try
                        {
                            sink.Dispose();
                        }
                        catch (IOException)
                        {
                        }
                    });
                    SpinWait.SpinUntil(() => disposeTask.IsCompleted, 2000);
                }

                if (flushTask == null || flushTask.IsCompleted || flushAttemptStarted.IsSet)
                    flushAttemptStarted.Dispose();
            }
        }
    }
}
