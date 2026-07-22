# TraceForge Async File Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `UnityEngine.Debug.Log*` 출력 경로를 제거하고 기존 `FileSink`를 producer 호출 경로에서 무할당으로 enqueue하는 무손실 비동기 파일 sink로 교체한다.

**Architecture:** `Logger`의 동기 sink dispatch 구조와 공개 `TF` API는 유지한다. `FileSink` 내부에 사전 할당 원형 큐와 전용 background `Thread`를 두고, producer는 큐 삽입만 수행하며 포맷팅과 `TextWriter` I/O는 worker가 담당한다. 큐 포화 시 producer를 차단하고, `Flush()`와 `Dispose()`는 접수된 sequence가 실제 기록될 때까지 기다린다.

**Tech Stack:** Unity 6000.0/6000.3, C#, .NET Standard 2.1, NUnit/Unity Test Framework 1.6.0, `System.Threading.Thread`, `Monitor`, `TextWriter`/`StreamWriter`

---

## 실행 컨텍스트

- 작업 브랜치: `feature/async-file-sink`
- 작업 트리: `C:\Projects\Other\trace-forge\.worktrees\async-file-sink`
- 설계: `Documentation~/Designs/2026-07-22-async-file-logging-design.md`
- 보류 이슈: `Documentation~/DeferredIssues.md`
- `main` 직접 변경과 `dev -> main` 릴리스 병합은 이번 계획에 포함하지 않는다. `main`은 테스트를 삭제한 배포 브랜치라 수정된 `Tests/`와 modify/delete 충돌이 예상되고, 메타데이터 정리도 보류 이슈 6의 별도 작업이다.

## 파일 책임

- Modify: `Runtime/Sinks/FileSink.cs` — 고정 용량 큐, worker, 포맷팅, flush/dispose/fault 상태
- Modify: `Tests/Runtime/FileSinkTests.cs` — 순서, 무손실, 포화 차단, 동시성, fault, 할당 및 성능 검증
- Modify: `Runtime/Logger.cs` — sink 오류 fallback에서 Unity Debug API 제거
- Modify: `Tests/Runtime/SinkDispatchTests.cs` — 표준 오류 fallback과 sink 격리 검증
- Delete: `Runtime/Sinks/UnityConsoleSink.cs` — Unity Console 출력 기능 제거
- Modify: `README.md` — 권장 sink와 비동기 FileSink 사용법
- Modify: `Documentation~/TraceForge.md` — 새 데이터 흐름과 thread 특성
- Modify: `Samples~/BasicUsage/TraceForgeBasicUsage.cs` — UnityConsoleSink 등록 제거
- Modify: `Samples~/BasicUsage/README.md` — 샘플 설명 갱신
- Create: `Documentation~/Benchmarks/2026-07-22-file-sink-performance.md` — 동일 환경의 변경 전후 측정값

## Task 1: Unity 테스트 호스트와 동기 FileSink 기준값 준비

**Files:**
- Create outside the feature worktree, under the root repository's ignored `.worktrees` directory: `C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\Packages\manifest.json`
- Modify temporarily, then restore before Task 2: `Tests/Runtime/FileSinkTests.cs`

- [ ] **Step 1: 임시 Unity 테스트 호스트 생성**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe' `
  -batchmode -quit `
  -createProject 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host' `
  -logFile 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\create-project.log'
```

Expected: exit code `0`, `Packages/manifest.json` 생성.

- [ ] **Step 2: 테스트 호스트 manifest를 로컬 패키지 전용으로 교체**

Use `apply_patch` so the complete file is:

```json
{
  "dependencies": {
    "com.skrawberries.traceforge": "file:C:/Projects/Other/trace-forge/.worktrees/async-file-sink",
    "com.unity.test-framework": "1.6.0"
  },
  "testables": [
    "com.skrawberries.traceforge"
  ]
}
```

- [ ] **Step 3: 기존 Runtime 테스트 기준선 실행**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host' `
  -runTests -testPlatform EditMode `
  -assemblyNames 'TraceForge.Tests.Runtime' `
  -testResults 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\baseline-results.xml' `
  -logFile 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\baseline.log'
```

Expected: 기존 `Logger`가 `Debug.LogError`를 호출해 Unity Test Runner의 unexpected-error 규칙을 건드리는 `TraceForge.Tests.SinkDispatchTests.ThrowingSink_DoesNotPreventOtherSinks`만 실패하고, 나머지 Runtime 테스트는 통과한다. 이 한 건은 Task 4가 직접 제거할 현재 동작으로 기준선에 명시해 계속 진행한다. 다른 실패가 하나라도 있으면 구현을 시작하지 말고 보고한다. `dev`에는 배포용 `.meta`가 없으므로 `TraceForge.Tests.Editor.PackageMetaValidationTests`는 보류 이슈 6으로 제외한다.

- [ ] **Step 4: 동기 구현 측정 테스트 추가**

Add this method to `FileSinkTests` without changing production code:

```csharp
[Test]
public void Benchmark_SynchronousFileSinkBaseline()
{
    const int entryCount = 100000;
    var entry = new LogEntry(
        Verbosity.Info,
        Categories.Default,
        "prebuilt benchmark message",
        null,
        DateTime.UtcNow.Ticks);

    using (var warmupSink = new FileSink(_tempFile))
    {
        for (int i = 0; i < 1000; i++)
            warmupSink.Write(in entry);
        warmupSink.Flush();
    }

    var producerStopwatch = new System.Diagnostics.Stopwatch();
    var completionStopwatch = new System.Diagnostics.Stopwatch();
    long allocatedBytes;

    using (var sink = new FileSink(_tempFile, append: false))
    {
        completionStopwatch.Start();
        producerStopwatch.Start();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < entryCount; i++)
            sink.Write(in entry);

        producerStopwatch.Stop();
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        sink.Flush();
        completionStopwatch.Stop();
    }

    int persistedRecords = File.ReadAllLines(_tempFile).Length;
    TestContext.Progress.WriteLine(
        $"SYNC_BASELINE count={entryCount} producerTicks={producerStopwatch.ElapsedTicks} " +
        $"completionTicks={completionStopwatch.ElapsedTicks} allocated={allocatedBytes} " +
        $"persisted={persistedRecords}");

    Assert.Greater(allocatedBytes, 0L);
    Assert.AreEqual(entryCount, persistedRecords);
}
```

- [ ] **Step 5: 기준값 테스트 실행하고 출력 보관**

Run the filtered benchmark five times so one scheduling outlier does not decide the comparison:

```powershell
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe'
$hostPath = 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host'

1..5 | ForEach-Object {
    & $unityPath `
      -batchmode -quit `
      -projectPath $hostPath `
      -runTests -testPlatform EditMode `
      -assemblyNames 'TraceForge.Tests.Runtime' `
      -testFilter 'TraceForge.Tests.FileSinkTests.Benchmark_SynchronousFileSinkBaseline' `
      -testResults (Join-Path $hostPath "baseline-benchmark-$_.xml") `
      -logFile (Join-Path $hostPath "baseline-benchmark-$_.log")

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

Expected: five PASS results and one `SYNC_BASELINE` line per log. Record all `producerTicks`, `completionTicks`, `allocated`, and `persisted` values; Task 5에서 같은 호스트와 메시지 수로 비교하고 시간은 각각 중앙값을 사용한다.

- [ ] **Step 6: 임시 기준값 테스트 제거**

Remove only `Benchmark_SynchronousFileSinkBaseline` with `apply_patch`, then run:

```powershell
git diff --exit-code -- Tests/Runtime/FileSinkTests.cs
```

Expected: exit code `0`. The five ignored-host log files retain the numeric baseline; production and tracked test files are back at their pre-benchmark state.

## Task 2: 비동기 FileSink 동작을 정의하는 실패 테스트 작성

**Files:**
- Modify: `Tests/Runtime/FileSinkTests.cs`
- Test target: `Runtime/Sinks/FileSink.cs`

- [ ] **Step 1: 테스트용 writer와 entry helper 추가**

Add these usings:

```csharp
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
```

Add these members inside `FileSinkTests`:

```csharp
private sealed class BlockingTextWriter : TextWriter
{
    private readonly ManualResetEventSlim _writeStarted = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

    public override Encoding Encoding => Encoding.UTF8;

    public bool WaitUntilWriteStarts(int millisecondsTimeout)
        => _writeStarted.Wait(millisecondsTimeout);

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
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
        => throw new IOException("simulated writer failure");
}

private static LogEntry CreateEntry(string message)
{
    return new LogEntry(
        Verbosity.Info,
        Categories.Default,
        message,
        null,
        DateTime.UtcNow.Ticks);
}

private static string ExtractMessage(string line)
{
    const string marker = "] ";
    int markerIndex = line.LastIndexOf(marker, StringComparison.Ordinal);
    return markerIndex >= 0 ? line.Substring(markerIndex + marker.Length) : line;
}
```

- [ ] **Step 2: 생성자, 순서, flush 테스트 추가**

```csharp
[Test]
public void QueueCapacity_ZeroOrNegative_ThrowsArgumentOutOfRangeException()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new FileSink(_tempFile, false, 0));
    Assert.Throws<ArgumentOutOfRangeException>(() => new FileSink(_tempFile, false, -1));
}

[Test]
public void Flush_WritesEveryEntryInEnqueueOrder()
{
    const int entryCount = 256;

    using (var sink = new FileSink(_tempFile, false, 8))
    {
        for (int i = 0; i < entryCount; i++)
            sink.Write(CreateEntry($"message-{i:D4}"));

        sink.Flush();
    }

    var lines = File.ReadAllLines(_tempFile);
    Assert.AreEqual(entryCount, lines.Length);

    for (int i = 0; i < entryCount; i++)
        Assert.AreEqual($"message-{i:D4}", ExtractMessage(lines[i]));
}
```

- [ ] **Step 3: 포화 차단과 dispose drain 테스트 추가**

```csharp
[Test]
public void Write_WhenQueueIsFull_BlocksUntilWriterReleasesCapacity()
{
    var writer = new BlockingTextWriter();
    var sink = new FileSink(writer, 1);
    var producerStarted = new ManualResetEventSlim(false);

    try
    {
        sink.Write(CreateEntry("first"));
        Assert.IsTrue(writer.WaitUntilWriteStarts(2000), "writer did not start");

        sink.Write(CreateEntry("second"));
        var blockedProducer = Task.Run(() =>
        {
            producerStarted.Set();
            sink.Write(CreateEntry("third"));
        });

        Assert.IsTrue(producerStarted.Wait(2000), "producer did not start");
        Assert.IsFalse(blockedProducer.Wait(100), "producer should block while queue is full");

        writer.Release();
        Assert.IsTrue(blockedProducer.Wait(2000), "producer did not resume");
    }
    finally
    {
        writer.Release();
        sink.Dispose();
        producerStarted.Dispose();
    }
}

[Test]
public void Dispose_DrainsAcceptedEntriesBeforeClosingFile()
{
    const int entryCount = 1000;
    var sink = new FileSink(_tempFile, false, 4);

    for (int i = 0; i < entryCount; i++)
        sink.Write(CreateEntry($"dispose-{i:D4}"));

    sink.Dispose();

    var lines = File.ReadAllLines(_tempFile);
    Assert.AreEqual(entryCount, lines.Length);
    Assert.AreEqual("dispose-0999", ExtractMessage(lines[entryCount - 1]));
}
```

- [ ] **Step 4: 다중 producer와 writer fault 테스트 추가**

```csharp
[Test]
public void MultipleProducers_WriteWithoutLossOrDuplication()
{
    const int producerCount = 4;
    const int entriesPerProducer = 1000;

    using (var sink = new FileSink(_tempFile, false, 32))
    {
        Parallel.For(0, producerCount, producer =>
        {
            for (int i = 0; i < entriesPerProducer; i++)
                sink.Write(CreateEntry($"P{producer}:{i:D4}"));
        });

        sink.Flush();
    }

    var lines = File.ReadAllLines(_tempFile);
    Assert.AreEqual(producerCount * entriesPerProducer, lines.Length);

    var messages = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in lines)
        Assert.IsTrue(messages.Add(ExtractMessage(line)), $"duplicate line: {line}");

    for (int producer = 0; producer < producerCount; producer++)
    {
        for (int i = 0; i < entriesPerProducer; i++)
            Assert.IsTrue(messages.Contains($"P{producer}:{i:D4}"));
    }
}

[Test]
public void WriterFailure_UnblocksFlushAndDisposeWithIOException()
{
    var sink = new FileSink(new ThrowingTextWriter(), 4);

    try
    {
        sink.Write(CreateEntry("will fail"));

        var flushException = Assert.Throws<IOException>(() => sink.Flush());
        StringAssert.Contains("simulated writer failure", flushException.ToString());

        var writeException = Assert.Throws<IOException>(
            () => sink.Write(CreateEntry("after failure")));
        StringAssert.Contains("simulated writer failure", writeException.ToString());

        var disposeException = Assert.Throws<IOException>(() => sink.Dispose());
        StringAssert.Contains("simulated writer failure", disposeException.ToString());
    }
    finally
    {
        try { sink.Dispose(); } catch (IOException) { }
    }
}
```

- [ ] **Step 5: 새 테스트가 production API 부재로 실패하는지 확인**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host' `
  -runTests -testPlatform EditMode `
  -assemblyNames 'TraceForge.Tests.Runtime' `
  -testFilter 'TraceForge.Tests.FileSinkTests' `
  -testResults 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\file-sink-red.xml' `
  -logFile 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\file-sink-red.log'
```

Expected: FAIL during compile with missing `FileSink(string, bool, int)` and `FileSink(TextWriter, int)` constructors. 이 실패를 확인하기 전에는 production 코드를 수정하지 않는다.

## Task 3: 고정 용량 비동기 FileSink 구현

**Files:**
- Modify: `Runtime/Sinks/FileSink.cs`
- Test: `Tests/Runtime/FileSinkTests.cs`

- [ ] **Step 1: FileSink를 승인된 상태 머신으로 교체**

Replace `Runtime/Sinks/FileSink.cs` with:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace TraceForge
{
    /// <summary>
    /// Queues log entries and writes them to a file on a dedicated background thread.
    /// Thread-safe. Implements <see cref="IDisposable"/>.
    /// </summary>
    public sealed class FileSink : ILogSink, IDisposable
    {
        private const int DefaultQueueCapacity = 4096;
        private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        private readonly TextWriter _writer;
        private readonly LogEntry[] _queue;
        private readonly object _syncRoot = new object();
        private readonly object _writerLock = new object();
        private readonly object _lifecycleLock = new object();
        private readonly Thread _worker;

        private int _head;
        private int _tail;
        private int _count;
        private long _acceptedCount;
        private long _writtenCount;
        private bool _accepting = true;
        private bool _disposed;
        private Exception _workerException;

        /// <summary>
        /// Opens or creates a log file with the default queue capacity of 4,096 entries.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the log file.</param>
        /// <param name="append">If true, appends to an existing file; otherwise overwrites it.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        public FileSink(string filePath, bool append = false)
            : this(filePath, append, DefaultQueueCapacity)
        {
        }

        /// <summary>
        /// Opens or creates a log file with the specified bounded queue capacity.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the log file.</param>
        /// <param name="append">If true, appends to an existing file; otherwise overwrites it.</param>
        /// <param name="queueCapacity">Maximum number of entries waiting in the queue.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="queueCapacity"/> is not positive.</exception>
        public FileSink(string filePath, bool append, int queueCapacity)
            : this(CreateWriter(filePath, append, queueCapacity), queueCapacity)
        {
        }

        internal FileSink(TextWriter writer, int queueCapacity)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            ValidateQueueCapacity(queueCapacity);

            _writer = writer;
            _queue = new LogEntry[queueCapacity];
            _worker = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "TraceForge.FileSink"
            };

            try
            {
                _worker.Start();
            }
            catch
            {
                _writer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Enqueues a log entry. Blocks only while the bounded queue is full.
        /// </summary>
        /// <param name="entry">The immutable log entry to enqueue.</param>
        /// <exception cref="IOException">Thrown when the writer thread has failed.</exception>
        public void Write(in LogEntry entry)
        {
            lock (_syncRoot)
            {
                while (_count == _queue.Length && _accepting && _workerException == null)
                    Monitor.Wait(_syncRoot);

                ThrowIfFaultedLocked();

                if (!_accepting)
                    return;

                _queue[_tail] = entry;
                _tail = (_tail + 1) % _queue.Length;
                _count++;
                _acceptedCount++;
                Monitor.PulseAll(_syncRoot);
            }
        }

        /// <summary>
        /// Waits for all entries accepted before this call and flushes the underlying writer.
        /// </summary>
        /// <exception cref="IOException">Thrown when the writer thread or underlying writer has failed.</exception>
        public void Flush()
        {
            lock (_lifecycleLock)
            {
                long targetSequence;

                lock (_syncRoot)
                {
                    if (_disposed)
                        return;

                    ThrowIfFaultedLocked();
                    targetSequence = _acceptedCount;

                    while (_writtenCount < targetSequence && _workerException == null)
                        Monitor.Wait(_syncRoot);

                    ThrowIfFaultedLocked();
                }

                try
                {
                    lock (_writerLock)
                        _writer.Flush();
                }
                catch (Exception ex)
                {
                    RecordWorkerFailure(ex);
                    throw CreateWriterException(ex);
                }
            }
        }

        /// <summary>
        /// Stops accepting entries, drains the queue, joins the writer thread, and closes the file.
        /// </summary>
        /// <exception cref="IOException">Thrown after cleanup when the underlying writer has failed.</exception>
        public void Dispose()
        {
            Exception failure = null;

            lock (_lifecycleLock)
            {
                lock (_syncRoot)
                {
                    if (_disposed)
                        return;

                    _accepting = false;
                    Monitor.PulseAll(_syncRoot);
                }

                _worker.Join();

                lock (_syncRoot)
                    failure = _workerException;

                lock (_writerLock)
                {
                    if (failure == null)
                    {
                        try { _writer.Flush(); }
                        catch (Exception ex) { failure = ex; }
                    }

                    try { _writer.Dispose(); }
                    catch (Exception ex)
                    {
                        if (failure == null)
                            failure = ex;
                    }
                }

                lock (_syncRoot)
                {
                    _disposed = true;
                    Monitor.PulseAll(_syncRoot);
                }
            }

            if (failure != null)
                throw CreateWriterException(failure);
        }

        private void WriterLoop()
        {
            try
            {
                while (TryDequeue(out LogEntry entry))
                {
                    WriteEntry(in entry);

                    lock (_syncRoot)
                    {
                        _writtenCount++;
                        Monitor.PulseAll(_syncRoot);
                    }
                }
            }
            catch (Exception ex)
            {
                RecordWorkerFailure(ex);
            }
        }

        private bool TryDequeue(out LogEntry entry)
        {
            lock (_syncRoot)
            {
                while (_count == 0 && _accepting)
                    Monitor.Wait(_syncRoot);

                if (_count == 0)
                {
                    entry = default(LogEntry);
                    return false;
                }

                entry = _queue[_head];
                _queue[_head] = default(LogEntry);
                _head = (_head + 1) % _queue.Length;
                _count--;
                Monitor.PulseAll(_syncRoot);
                return true;
            }
        }

        private void WriteEntry(in LogEntry entry)
        {
            lock (_writerLock)
            {
                Span<char> timestampBuffer = stackalloc char[24];
                var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);

                if (!timestamp.TryFormat(
                    timestampBuffer,
                    out int timestampLength,
                    TimestampFormat,
                    CultureInfo.InvariantCulture))
                {
                    throw new FormatException("TraceForge could not format the log timestamp.");
                }

                _writer.Write('[');
                _writer.Write(timestampBuffer.Slice(0, timestampLength));
                _writer.Write("] [");
                _writer.Write(GetVerbosityName(entry.Verbosity));
                _writer.Write("] [");
                _writer.Write(entry.Category.Name ?? "Default");
                _writer.Write("] ");
                _writer.WriteLine(entry.Message ?? string.Empty);

                if (entry.Exception != null)
                {
                    _writer.Write("Exception: ");
                    _writer.WriteLine(entry.Exception);
                }
            }
        }

        private void RecordWorkerFailure(Exception exception)
        {
            lock (_syncRoot)
            {
                if (_workerException == null)
                    _workerException = exception;

                _accepting = false;
                Monitor.PulseAll(_syncRoot);
            }
        }

        private void ThrowIfFaultedLocked()
        {
            if (_workerException != null)
                throw CreateWriterException(_workerException);
        }

        private static IOException CreateWriterException(Exception innerException)
        {
            return new IOException("TraceForge FileSink writer failed.", innerException);
        }

        private static TextWriter CreateWriter(string filePath, bool append, int queueCapacity)
        {
            ValidateQueueCapacity(queueCapacity);

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return new StreamWriter(filePath, append, System.Text.Encoding.UTF8)
            {
                AutoFlush = false
            };
        }

        private static void ValidateQueueCapacity(int queueCapacity)
        {
            if (queueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(queueCapacity),
                    "Queue capacity must be greater than zero.");
            }
        }

        private static string GetVerbosityName(Verbosity verbosity)
        {
            switch (verbosity)
            {
                case Verbosity.Trace: return "TRACE";
                case Verbosity.Debug: return "DEBUG";
                case Verbosity.Info: return "INFO";
                case Verbosity.Warning: return "WARNING";
                case Verbosity.Error: return "ERROR";
                case Verbosity.Fatal: return "FATAL";
                case Verbosity.Off: return "OFF";
                default: return "UNKNOWN";
            }
        }
    }
}
```

- [ ] **Step 2: FileSink 테스트 재실행**

Run the Task 2 Unity command again.

Expected: all `TraceForge.Tests.FileSinkTests` PASS, including existing format/append/dispose tests and new queue/fault tests.

- [ ] **Step 3: 전체 Runtime 회귀 테스트 실행**

Run the Task 1 Runtime assembly command, writing results to `async-file-sink-green.xml` and `async-file-sink-green.log`.

Expected: 새 FileSink 관련 테스트는 모두 통과한다. 전체 결과에서는 Task 1에 기록한 `ThrowingSink_DoesNotPreventOtherSinks` 한 건만 아직 실패할 수 있으며, 그 외 실패는 이 단계의 회귀로 취급해 수정한다. 전체 Runtime의 `failed="0"`은 Task 4에서 달성한다.

- [ ] **Step 4: 테스트와 구현 커밋**

```powershell
git add Runtime/Sinks/FileSink.cs Tests/Runtime/FileSinkTests.cs
git commit -m "feat: make file sink asynchronous"
```

## Task 4: Unity Console 출력 경로 제거

**Files:**
- Modify: `Tests/Runtime/SinkDispatchTests.cs`
- Modify: `Runtime/Logger.cs`
- Delete: `Runtime/Sinks/UnityConsoleSink.cs`

- [ ] **Step 1: 표준 오류 fallback 실패 테스트 작성**

Add `using System.IO;` to `SinkDispatchTests.cs`, then add:

```csharp
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
```

- [ ] **Step 2: 테스트가 현재 Debug.LogError 구현에서 실패하는지 확인**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host' `
  -runTests -testPlatform EditMode `
  -assemblyNames 'TraceForge.Tests.Runtime' `
  -testFilter 'TraceForge.Tests.SinkDispatchTests.ThrowingSink_ReportsFailureToStandardError' `
  -testResults 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\stderr-red.xml' `
  -logFile 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host\stderr-red.log'
```

Expected: FAIL because `Console.Error` is empty while the current code calls Unity `Debug.LogError`.

- [ ] **Step 3: Logger fallback을 Console.Error로 교체**

Replace the catch block in `Logger.Write` with:

```csharp
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"[TraceForge] Sink '{sink.GetType().Name}' threw an exception: {ex}");
}
```

Keep `using UnityEngine;` because `RuntimeInitializeOnLoadMethod` still requires it. Do not alter filtering or reset behavior.

- [ ] **Step 4: UnityConsoleSink 소스 삭제**

Delete `Runtime/Sinks/UnityConsoleSink.cs` with `apply_patch`. The `dev` branch has no corresponding `.meta`; do not create or delete unrelated metadata here.

- [ ] **Step 5: fallback 테스트와 전체 회귀 테스트 실행**

Run the Step 2 filtered command, then the Task 1 Runtime assembly command.

Expected: filtered test PASS and full suite `failed="0"`.

- [ ] **Step 6: 런타임 Debug API 참조 검사**

Run:

```powershell
rg -n "Debug\.Log|Debug\.LogWarning|Debug\.LogError|Debug\.LogException|UnityConsoleSink" Runtime
```

Expected: no matches and `rg` exit code `1`.

- [ ] **Step 7: Console 제거 커밋**

```powershell
git add Runtime/Logger.cs Runtime/Sinks/UnityConsoleSink.cs Tests/Runtime/SinkDispatchTests.cs
git commit -m "feat: remove Unity console logging"
```

## Task 5: Producer 무할당 및 변경 전후 성능 검증

**Files:**
- Modify: `Tests/Runtime/FileSinkTests.cs`
- Create: `Documentation~/Benchmarks/2026-07-22-file-sink-performance.md`

- [ ] **Step 1: 최종 무할당 테스트와 비동기 benchmark 추가**

Add both tests below:

```csharp
[Test]
public void Write_UncontendedProducerPath_AllocatesZeroBytes()
{
    const int entryCount = 100000;
    var entry = CreateEntry("prebuilt allocation message");
    var sink = new FileSink(TextWriter.Null, entryCount + 1024);

    try
    {
        for (int i = 0; i < 1000; i++)
            sink.Write(in entry);
        sink.Flush();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < entryCount; i++)
            sink.Write(in entry);

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        sink.Flush();

        Assert.AreEqual(0L, allocatedBytes);
    }
    finally
    {
        sink.Dispose();
    }
}

[Test]
public void Benchmark_AsyncFileSinkProducerPath()
{
    const int entryCount = 100000;
    var entry = CreateEntry("prebuilt benchmark message");

    using (var warmupSink = new FileSink(_tempFile))
    {
        for (int i = 0; i < 1000; i++)
            warmupSink.Write(in entry);
        warmupSink.Flush();
    }

    var producerStopwatch = new System.Diagnostics.Stopwatch();
    var completionStopwatch = new System.Diagnostics.Stopwatch();
    long allocatedBytes;

    using (var sink = new FileSink(_tempFile, false, entryCount + 1024))
    {
        completionStopwatch.Start();
        producerStopwatch.Start();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < entryCount; i++)
            sink.Write(in entry);

        producerStopwatch.Stop();
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        sink.Flush();
        completionStopwatch.Stop();
    }

    int persistedRecords = File.ReadAllLines(_tempFile).Length;
    TestContext.Progress.WriteLine(
        $"ASYNC_RESULT count={entryCount} producerTicks={producerStopwatch.ElapsedTicks} " +
        $"completionTicks={completionStopwatch.ElapsedTicks} allocated={allocatedBytes} " +
        $"persisted={persistedRecords}");

    Assert.AreEqual(0L, allocatedBytes);
    Assert.AreEqual(entryCount, persistedRecords);
}
```

- [ ] **Step 2: 할당 및 비동기 benchmark 실행**

Run the zero-allocation test once, then run the async benchmark five times:

```powershell
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe'
$hostPath = 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host'

& $unityPath `
  -batchmode -quit `
  -projectPath $hostPath `
  -runTests -testPlatform EditMode `
  -assemblyNames 'TraceForge.Tests.Runtime' `
  -testFilter 'TraceForge.Tests.FileSinkTests.Write_UncontendedProducerPath_AllocatesZeroBytes' `
  -testResults (Join-Path $hostPath 'allocation-results.xml') `
  -logFile (Join-Path $hostPath 'allocation.log')

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

1..5 | ForEach-Object {
    & $unityPath `
      -batchmode -quit `
      -projectPath $hostPath `
      -runTests -testPlatform EditMode `
      -assemblyNames 'TraceForge.Tests.Runtime' `
      -testFilter 'TraceForge.Tests.FileSinkTests.Benchmark_AsyncFileSinkProducerPath' `
      -testResults (Join-Path $hostPath "async-benchmark-$_.xml") `
      -logFile (Join-Path $hostPath "async-benchmark-$_.log")

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

Give each benchmark run a unique result XML and log path. Expected: all PASS, every `ASYNC_RESULT allocated=0`, and every run reports `persisted=100000`. Compare the median `ASYNC_RESULT producerTicks` with the median Task 1 `SYNC_BASELINE producerTicks`; async producer ticks must be lower. Record both completion-time medians as well. If producer time is not lower, stop and profile queue contention before proceeding; do not conceal a completion-time regression in the report.

- [ ] **Step 3: benchmark 보고서 작성**

Create `Documentation~/Benchmarks/2026-07-22-file-sink-performance.md` with `apply_patch`. It must contain:

- title `# FileSink Performance — 2026-07-22`
- environment values `Unity 6000.3.8f1`, `Windows Editor`, `Edit Mode`, 100,000 prebuilt entries, and 1,000 warm-up entries
- a five-column table: implementation, median producer ticks, median completion ticks, median producer allocated bytes, persisted records
- a synchronous row populated directly from the five numeric `SYNC_BASELINE` runs
- an async row populated directly from the five numeric `ASYNC_RESULT` runs
- persisted record value `100000` for both rows and allocated byte value `0` for async
- a decision paragraph containing both numeric ratios `synchronous producer ticks / async producer ticks` and `synchronous completion ticks / async completion ticks`
- a limitation paragraph stating that producer timing excludes `Flush()`, completion timing includes it, the oversized queue measures only the non-saturated producer path, and mobile-device performance was not measured

Do not write symbolic labels or unmeasured values into the report.

- [ ] **Step 4: 전체 FileSink 테스트 재실행**

Expected: all FileSink tests PASS and no timeout or leaked worker thread.

- [ ] **Step 5: 성능 테스트와 보고서 커밋**

```powershell
git add Tests/Runtime/FileSinkTests.cs Documentation~/Benchmarks/2026-07-22-file-sink-performance.md
git commit -m "test: verify async file sink performance"
```

## Task 6: 문서와 샘플을 새 출력 경로에 맞춤

**Files:**
- Modify: `README.md`
- Modify: `Documentation~/TraceForge.md`
- Modify: `Samples~/BasicUsage/TraceForgeBasicUsage.cs`
- Modify: `Samples~/BasicUsage/README.md`

- [ ] **Step 1: README 기능 및 Quick Start 갱신**

Replace Unity Console references with these statements and example:

```markdown
- **Sink-based** — route logs to an asynchronous file, ring buffer, or your own sink
- **Asynchronous file output** — formatting and file I/O run on a dedicated background thread
```

```csharp
using System.IO;
using TraceForge;
using UnityEngine;

var fileSink = new FileSink(
    Path.Combine(Application.persistentDataPath, "traceforge.log"));
var ringBuffer = new RingBufferSink(capacity: 512);

TF.AddSink(fileSink);
TF.AddSink(ringBuffer);

TF.Info("Game started");
TF.Warning(Categories.Network, "Connection slow");
TF.Error("Something went wrong");

// Remove and dispose the owned file sink during shutdown.
TF.RemoveSink(fileSink);
fileSink.Dispose();
```

Delete the `UnityConsoleSink` subsection. Update the `FileSink` subsection to state that `Write()` enqueues and that `Flush()`/`Dispose()` wait for persistence. Show the optional capacity overload:

```csharp
var fileSink = new FileSink(logPath, append: true, queueCapacity: 4096);
```

State explicitly that this threaded `FileSink` supports Windows, macOS, Linux, Android, and iOS, and is not supported on WebGL.

- [ ] **Step 2: architecture 문서 갱신**

Use this sink diagram in `Documentation~/TraceForge.md`:

```text
TF (facade) -> Logger (internal) -> ILogSink[] -> RingBufferSink
                                             -> FileSink queue -> writer thread -> file
                                             -> Your custom sink
```

In the thread-safety section, describe `RingBufferSink` as lock-based and `FileSink` as a bounded `Monitor` queue with a single writer thread. Do not fix the deferred category override, stripping, or automatic application-quit flush claims in this task.

- [ ] **Step 3: BasicUsage 샘플 갱신**

Delete:

```csharp
// Add Unity Console sink (routes to Debug.Log/LogWarning/LogError)
TF.AddSink(new UnityConsoleSink());
```

Change the class summary to:

```csharp
/// Demonstrates TraceForge logging setup and usage patterns.
/// Attach to any GameObject to write logs asynchronously to a file and retain recent entries in memory.
```

Change the file sink comment to:

```csharp
// Add asynchronous file sink. File formatting and I/O run on its writer thread.
```

Keep the existing `OnDestroy()` disposal. In `Samples~/BasicUsage/README.md`, change the initialization bullet to `Initializing sinks (RingBuffer, asynchronous File)`.

- [ ] **Step 4: 제거된 API 참조 검사**

Run:

```powershell
rg -n "UnityConsoleSink|Debug\.Log|Unity Console" README.md Documentation~ Samples~ Runtime
```

Expected: no references that claim TraceForge outputs through Unity Console. A historical explanation in the approved design or deferred issue documents is allowed; review those matches manually rather than editing deferred scope.

- [ ] **Step 5: 샘플 포함 C# API 컴파일**

Run:

```powershell
$editorDataPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data'
$monoPath = Join-Path $editorDataPath 'MonoBleedingEdge\bin\mono.exe'
$compilerPath = Join-Path $editorDataPath 'MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$frameworkRefs = Get-ChildItem -File (Join-Path $editorDataPath 'NetStandard\ref\2.1.0') -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$unityCoreRef = '/reference:' + (Join-Path $editorDataPath 'Managed\UnityEngine\UnityEngine.CoreModule.dll')
$sourcePaths = @(
    (Get-ChildItem -Recurse -File 'Runtime' -Filter '*.cs' | ForEach-Object { $_.FullName })
    (Resolve-Path 'Samples~\BasicUsage\TraceForgeBasicUsage.cs').Path
)
$compilerOptions = @('/nologo', '/target:library', '/langversion:latest', '/nostdlib+', '/out:NUL') + $frameworkRefs + $unityCoreRef + $sourcePaths
& $monoPath $compilerPath $compilerOptions
exit $LASTEXITCODE
```

Expected: exit code `0`, no missing `UnityConsoleSink` reference.

- [ ] **Step 6: 문서와 샘플 커밋**

```powershell
git add README.md Documentation~/TraceForge.md Samples~/BasicUsage/TraceForgeBasicUsage.cs Samples~/BasicUsage/README.md
git commit -m "docs: document asynchronous file output"
```

## Task 7: 전체 검증과 작업 브랜치 인계

**Files:**
- Verify: all changed production, test, sample, and documentation files
- Do not modify: `Documentation~/DeferredIssues.md` except to correct a factual contradiction introduced by this feature

- [ ] **Step 1: Unity 6000.3 전체 Runtime 테스트 실행**

Run the Task 1 Runtime assembly command with result files `final-results.xml` and `final.log`.

Expected: `failed="0"`; record total passed count.

- [ ] **Step 2: Unity 6000.0 Runtime .NET Standard 2.1 컴파일**

Run:

```powershell
$editorDataPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.59f2\Editor\Data'
$monoPath = Join-Path $editorDataPath 'MonoBleedingEdge\bin\mono.exe'
$compilerPath = Join-Path $editorDataPath 'MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$frameworkRefs = Get-ChildItem -File (Join-Path $editorDataPath 'NetStandard\ref\2.1.0') -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$unityCoreRef = '/reference:' + (Join-Path $editorDataPath 'Managed\UnityEngine\UnityEngine.CoreModule.dll')
$sourcePaths = Get-ChildItem -Recurse -File 'Runtime' -Filter '*.cs' | ForEach-Object { $_.FullName }
$compilerOptions = @('/nologo', '/target:library', '/langversion:latest', '/nostdlib+', '/warnaserror+', '/out:NUL') + $frameworkRefs + $unityCoreRef + $sourcePaths
& $monoPath $compilerPath $compilerOptions
exit $LASTEXITCODE
```

Expected: exit code `0`, zero warnings and errors.

- [ ] **Step 3: Unity 6000.3 Runtime, Editor, Tests, Sample 컴파일**

Run:

```powershell
$editorDataPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Data'
$monoPath = Join-Path $editorDataPath 'MonoBleedingEdge\bin\mono.exe'
$compilerPath = Join-Path $editorDataPath 'MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe'
$apiPath = Join-Path $editorDataPath 'UnityReferenceAssemblies\unity-4.8-api'
$nunitPath = Join-Path $editorDataPath 'Resources\PackageManager\BuiltInPackages\com.unity.ext.nunit\net40\unity-custom\nunit.framework.dll'
$referencePaths = @(
    (Join-Path $apiPath 'mscorlib.dll'),
    (Join-Path $apiPath 'System.dll'),
    (Join-Path $apiPath 'System.Core.dll'),
    (Join-Path $apiPath 'Facades\netstandard.dll'),
    (Join-Path $editorDataPath 'Managed\UnityEngine\UnityEngine.CoreModule.dll'),
    (Join-Path $editorDataPath 'Managed\UnityEngine\UnityEngine.IMGUIModule.dll'),
    (Join-Path $editorDataPath 'Managed\UnityEngine\UnityEditor.CoreModule.dll'),
    $nunitPath
)
$referenceOptions = $referencePaths | ForEach-Object { '/reference:' + $_ }
$sourcePaths = Get-ChildItem -Recurse -File 'Runtime','Editor','Tests','Samples~\BasicUsage' -Filter '*.cs' | ForEach-Object { $_.FullName }
$compilerOptions = @('/nologo', '/target:library', '/langversion:latest', '/nostdlib+', '/warnaserror+', '/out:NUL') + $referenceOptions + $sourcePaths
& $monoPath $compilerPath $compilerOptions
exit $LASTEXITCODE
```

Expected: exit code `0`, zero warnings and errors.

- [ ] **Step 4: 정적 성능 및 API 검토**

Run:

```powershell
rg -n "Debug\.Log|UnityConsoleSink" Runtime README.md Documentation~ Samples~
rg -n "new |string\.Format|ToString\(|\$\"" Runtime/Sinks/FileSink.cs
rg -n "public FileSink|public void Write|public void Flush|public void Dispose" Runtime/Sinks/FileSink.cs
```

Expected:

- Runtime에는 Unity Debug 출력과 UnityConsoleSink가 없다.
- producer `Write()` 안에는 새 객체, 문자열 포맷, I/O가 없다.
- 기존 두 인자 constructor, `Write`, `Flush`, `Dispose`가 유지되고 세 인자 constructor만 추가된다.

- [ ] **Step 5: 저장소 무결성 검사**

Run:

```powershell
git diff --check
git status --short
git log --oneline --decorate -8
```

Expected: 공백 오류 없음. 계획 실행으로 의도한 파일만 변경 또는 커밋됨. `.claude/`, `CLAUDE.md`, 보류 이슈의 범위 밖 코드는 변경되지 않음.

- [ ] **Step 6: 최종 검토 수정이 있으면 검증 후 커밋**

Only if Step 1–5 finds an in-scope defect, make the smallest correction, rerun the affected test plus the full Runtime suite, then commit:

```powershell
git add Runtime/Sinks/FileSink.cs Runtime/Logger.cs Tests/Runtime/FileSinkTests.cs Tests/Runtime/SinkDispatchTests.cs README.md Documentation~/TraceForge.md Samples~/BasicUsage/TraceForgeBasicUsage.cs Samples~/BasicUsage/README.md
git commit -m "fix: address async file sink verification"
```

Do not create this commit when no correction is needed.

- [ ] **Step 7: 임시 Unity 테스트 호스트 제거**

After all numeric results have been copied into the benchmark report and handoff notes, verify the exact generated host and remove it:

```powershell
$expectedHost = 'C:\Projects\Other\trace-forge\.worktrees\async-file-sink-test-host'
$allowedRoot = (Resolve-Path -LiteralPath 'C:\Projects\Other\trace-forge\.worktrees').Path + [IO.Path]::DirectorySeparatorChar
$resolvedHost = (Resolve-Path -LiteralPath $expectedHost).Path
$manifestPath = Join-Path $resolvedHost 'Packages\manifest.json'

if ($resolvedHost -ne $expectedHost) { throw "Unexpected host path: $resolvedHost" }
if (-not $resolvedHost.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Host is outside the allowed .worktrees directory: $resolvedHost"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Test-host manifest is missing: $manifestPath"
}
if ((Get-Content -Raw -LiteralPath $manifestPath) -notmatch 'com\.skrawberries\.traceforge') {
    throw "Path does not look like the generated TraceForge test host: $resolvedHost"
}

Remove-Item -Recurse -LiteralPath $resolvedHost
```

Expected: only the generated ignored test host is removed; the feature worktree remains intact.

- [ ] **Step 8: feature branch 상태로 인계**

Report:

- worktree and branch name
- commit list
- Unity Test Runner passed count
- Unity 6000.0/6000.3 compile results
- explicit platform-validation scope: Windows Editor benchmark/reference compilation only; no Android or iOS device benchmark
- synchronous and async benchmark medians, producer ratio, and completion ratio
- producer allocated bytes
- confirmation that the temporary Unity test host was removed
- explicit note that `main` integration remains unperformed and requires a separate approved release-integration task
