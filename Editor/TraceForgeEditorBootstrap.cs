using UnityEditor;
using UnityEngine;

namespace TraceForge.Editor
{
    [InitializeOnLoad]
    internal static class TraceForgeEditorBootstrap
    {
        private static RingBufferSink _ownedRingBuffer;

        static TraceForgeEditorBootstrap()
        {
            Bootstrap.EditorSettingsLoader = TraceForgeSettingsProvider.LoadSettingsIfExists;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseOwnedRingBuffer;
            AssemblyReloadEvents.beforeAssemblyReload += Bootstrap.Shutdown;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureEditModeRingBuffer();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ReleaseOwnedRingBuffer();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Bootstrap.Shutdown();
                ReleaseOwnedRingBuffer();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EnsureEditModeRingBuffer();
            }
        }

        private static void EnsureEditModeRingBuffer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;
            if (SinkRegistry.RingBuffers.Length != 0)
                return;
            var settings = TraceForgeSettingsProvider.LoadSettingsIfExists();
            var capacity = settings == null ? 1024 : Mathf.Max(1, settings.RingBufferCapacity);
            _ownedRingBuffer = new RingBufferSink(capacity);
            TF.AddSink(_ownedRingBuffer);
            if (settings != null)
            {
                TF.SetMinVerbosity(settings.MinVerbosity);
                Logger.ClearAllCategoryVerbosities();
                if (settings.CategoryOverrides != null)
                {
                    foreach (var categoryOverride in settings.CategoryOverrides)
                        TF.SetCategoryVerbosity(new LogCategory(categoryOverride.Category), categoryOverride.Verbosity);
                }
            }
        }

        private static void ReleaseOwnedRingBuffer()
        {
            if (_ownedRingBuffer == null)
                return;
            TF.RemoveSink(_ownedRingBuffer);
            _ownedRingBuffer = null;
        }

        internal static void EnsureForTests() => EnsureEditModeRingBuffer();

        internal static void ReleaseForTests() => ReleaseOwnedRingBuffer();
    }
}
