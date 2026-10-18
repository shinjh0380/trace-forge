// Host-only benchmark. Copy to Assets/Editor; not part of the package assembly.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using TraceForge;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class ReadmeLoggingComparison
{
    private const int WarmupCount = 1000;
    private const int EntryCount = 100000;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable]
    private sealed class Result
    {
        public string protocol = "readme-unity-vs-traceforge-timing-v2";
        public string mode;
        public string unityVersion;
        public string outputPath;
        public string nativeLogPath;
        public string handler;
        public string startedUtc;
        public int warmupCalls = WarmupCount;
        public int measuredCalls = EntryCount;
        public int queueCapacity;
        public int registeredSinks;
        public int ringBuffers;
        public bool unityCapture;
        public bool loggingEnabled;
        public string unityLogFilter;
        public string unityStackPolicy;
        public string traceforgeStackPolicy;
        public bool batchMode;
        public long stopwatchFrequency;
        public long producerTicks;
        public bool hasFlushCompletion;
        public long producerAndFlushTicks;
        public long acceptedEntries;
        public long writtenEntries;
        public bool allocationRecorderEnabled = false;
        public bool profilerEnabled;
        public bool allocationCallstacksEnabled;
    }

    public static void Run()
    {
        string mode = Argument("-tfMode");
        string outputDirectory = Path.GetFullPath(Argument("-tfOut"));
        if (mode != "unity" && mode != "traceforge")
            throw new ArgumentException("Expected unity or traceforge mode.");
        Directory.CreateDirectory(outputDirectory);

        var assembly = typeof(TF).Assembly;
        var logger = assembly.GetType("TraceForge.Logger", true);
        var capture = assembly.GetType("TraceForge.UnityLogCapture", true);
        var registry = assembly.GetType("TraceForge.SinkRegistry", true);
        var originalStack = Application.GetStackTraceLogType(LogType.Log);
        bool originalLogEnabled = Debug.unityLogger.logEnabled;
        var originalFilter = Debug.unityLogger.filterLogType;
        bool originalProfilerEnabled = Profiler.enabled;
        bool originalAllocationCallstacks = Profiler.enableAllocationCallstacks;
        FileSink fileSink = null;
        try
        {
            Profiler.enabled = false;
            Profiler.enableAllocationCallstacks = false;
            TF.Reset();
            capture.GetMethod("Stop", StaticFlags).Invoke(null, null);
            logger.GetMethod("SetStackTracePolicy", StaticFlags).Invoke(null, new object[] { StackTracePolicy.None });
            TF.SetMinVerbosity(Verbosity.Info);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Debug.unityLogger.logEnabled = true;
            Debug.unityLogger.filterLogType = LogType.Log;
            if (Debug.unityLogger.logHandler.GetType().FullName != "UnityEngine.DebugLogHandler")
                throw new InvalidOperationException("Unity logger is wrapped or replaced.");

            string filePath = Path.Combine(outputDirectory, "traceforge.log");
            if (mode == "traceforge")
            {
                fileSink = new FileSink(filePath, false, 4096);
                TF.AddSink(fileSink);
                if (!TF.IsEnabled(Verbosity.Info, Categories.Default))
                    throw new InvalidOperationException("TF.Info is filtered out.");
            }

            var result = new Result
            {
                mode = mode,
                unityVersion = Application.unityVersion,
                outputPath = mode == "unity" ? Path.GetFullPath(Argument("-logFile")) : filePath,
                nativeLogPath = Path.GetFullPath(Argument("-logFile")),
                handler = Debug.unityLogger.logHandler.GetType().FullName,
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                queueCapacity = mode == "traceforge" ? 4096 : 0,
                loggingEnabled = Debug.unityLogger.logEnabled,
                unityLogFilter = Debug.unityLogger.filterLogType.ToString(),
                unityStackPolicy = Application.GetStackTraceLogType(LogType.Log).ToString(),
                traceforgeStackPolicy = logger.GetMethod("GetStackTracePolicy", StaticFlags).Invoke(null, null).ToString(),
                batchMode = Application.isBatchMode,
                profilerEnabled = Profiler.enabled,
                allocationCallstacksEnabled = Profiler.enableAllocationCallstacks,
                stopwatchFrequency = Stopwatch.Frequency
            };
            AssertState(logger, capture, registry, fileSink, result);

            string[] warmup = Messages("WARM", WarmupCount);
            string[] messages = Messages("DATA", EntryCount);
            if (mode == "unity")
                for (int i = 0; i < WarmupCount; i++) Debug.Log(warmup[i]);
            else
                for (int i = 0; i < WarmupCount; i++) TF.Info(warmup[i]);
            if (fileSink != null) fileSink.Flush();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // Timing-only cohort: no allocation recorder or active Profiler.
            // The complete profiled cohort remains published separately.
            long start = Stopwatch.GetTimestamp();
            if (mode == "unity")
                for (int i = 0; i < EntryCount; i++) Debug.Log(messages[i]);
            else
                for (int i = 0; i < EntryCount; i++) TF.Info(messages[i]);
            long producerEnd = Stopwatch.GetTimestamp();
            result.producerTicks = producerEnd - start;
            if (fileSink != null)
            {
                fileSink.Flush();
                result.hasFlushCompletion = true;
                result.producerAndFlushTicks = Stopwatch.GetTimestamp() - start;
            }
            AssertState(logger, capture, registry, fileSink, result);
            if (fileSink != null)
            {
                result.acceptedEntries = (long)typeof(FileSink).GetField("_acceptedCount", InstanceFlags).GetValue(fileSink);
                result.writtenEntries = (long)typeof(FileSink).GetField("_writtenCount", InstanceFlags).GetValue(fileSink);
                if (result.acceptedEntries != WarmupCount + EntryCount || result.writtenEntries != WarmupCount + EntryCount)
                    throw new InvalidOperationException("FileSink did not accept/write every entry.");
                TF.RemoveSink(fileSink);
                fileSink.Dispose();
                fileSink = null;
            }
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonUtility.ToJson(result, true));
        }
        finally
        {
            if (fileSink != null)
            {
                TF.RemoveSink(fileSink);
                fileSink.Dispose();
            }
            TF.Reset();
            Application.SetStackTraceLogType(LogType.Log, originalStack);
            Debug.unityLogger.logEnabled = originalLogEnabled;
            Debug.unityLogger.filterLogType = originalFilter;
            Profiler.enableAllocationCallstacks = originalAllocationCallstacks;
            Profiler.enabled = originalProfilerEnabled;
        }
    }

    private static void AssertState(Type logger, Type capture, Type registry, FileSink sink, Result result)
    {
        if (Profiler.enabled || Profiler.enableAllocationCallstacks)
            throw new InvalidOperationException("Timing-only profiler settings changed.");
        var sinks = (ILogSink[])logger.GetField("_sinks", StaticFlags).GetValue(null);
        bool started = (bool)capture.GetField("_started", StaticFlags).GetValue(null);
        var rings = (Array)registry.GetProperty("RingBuffers", StaticFlags).GetValue(null);
        if (started || rings.Length != 0 || sinks.Length != (sink == null ? 0 : 1) ||
            (sink != null && !ReferenceEquals(sinks[0], sink)))
            throw new InvalidOperationException("Unexpected capture or registered sink state.");
        result.unityCapture = started;
        result.registeredSinks = sinks.Length;
        result.ringBuffers = rings.Length;
    }

    private static string[] Messages(string phase, int count)
    {
        var messages = new string[count];
        for (int i = 0; i < count; i++)
            messages[i] = "TFBENCH " + phase + " " + i.ToString("D6", CultureInfo.InvariantCulture)
                + " | Player=7 Health=100 X=12.5 Y=0 Z=8.0";
        return messages;
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        throw new ArgumentException("Missing " + name);
    }
}
