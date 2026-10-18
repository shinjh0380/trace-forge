using System;
using UnityEngine;

namespace TraceForge
{
    /// <summary>
    /// Project-wide TraceForge logging settings supplied to the runtime bootstrap.
    /// </summary>
    public sealed class TraceForgeSettings : ScriptableObject
    {
        /// <summary>Global minimum verbosity.</summary>
        public Verbosity MinVerbosity = Verbosity.Debug;

        /// <summary>Per-category minimum verbosity overrides.</summary>
        public CategoryOverride[] CategoryOverrides = Array.Empty<CategoryOverride>();

        /// <summary>Whether to create the in-memory ring buffer sink.</summary>
        public bool EnableRingBuffer = true;

        /// <summary>Maximum number of entries retained by the ring buffer.</summary>
        public int RingBufferCapacity = 1024;

        /// <summary>Whether to create the asynchronous file sink.</summary>
        public bool EnableFileSink = false;

        /// <summary>File path, relative to <see cref="Application.persistentDataPath"/>.</summary>
        public string FilePath = "traceforge.log";

        /// <summary>Whether file output appends to an existing file.</summary>
        public bool AppendToFile = true;

        /// <summary>Maximum number of entries waiting in the file sink queue.</summary>
        public int FileQueueCapacity = 4096;

        /// <summary>Whether Unity log messages should be captured (consumed by Phase 3).</summary>
        public bool CaptureUnityLog = true;

        /// <summary>Policy used for stack trace capture (consumed by Phase 3).</summary>
        public StackTracePolicy StackTracePolicy = StackTracePolicy.ErrorAndAbove;

        /// <summary>A minimum verbosity override for one category.</summary>
        [Serializable]
        public struct CategoryOverride
        {
            /// <summary>Category name to override.</summary>
            public string Category;

            /// <summary>Minimum verbosity for the category.</summary>
            public Verbosity Verbosity;
        }
    }
}
