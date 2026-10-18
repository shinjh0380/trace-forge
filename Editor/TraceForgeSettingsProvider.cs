using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TraceForge.Editor
{
    internal static class TraceForgeSettingsProvider
    {
        internal const string SettingsPath = "ProjectSettings/TraceForgeSettings.asset";
        private static TraceForgeSettings _cachedSettings;

        static TraceForgeSettingsProvider()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/TraceForge", SettingsScope.Project)
            {
                label = "TraceForge",
                guiHandler = DrawGUI,
                keywords = new HashSet<string>(new[] { "TraceForge", "Logging", "Verbosity", "Sink", "File" })
            };
        }

        internal static TraceForgeSettings LoadSettingsIfExists()
        {
            if (_cachedSettings != null)
                return _cachedSettings;
            if (!File.Exists(SettingsPath))
                return null;
            var objects = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath);
            if (objects == null)
                return null;
            foreach (var value in objects)
            {
                var settings = value as TraceForgeSettings;
                if (settings != null)
                {
                    _cachedSettings = settings;
                    return settings;
                }
            }
            return null;
        }

        internal static TraceForgeSettings GetOrCreateSettings()
        {
            var settings = LoadSettingsIfExists();
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<TraceForgeSettings>();
            settings.name = "TraceForgeSettings";
            settings.hideFlags = HideFlags.DontSave;
            _cachedSettings = settings;
            SaveSettings(settings);
            return settings;
        }

        internal static void SaveSettings(TraceForgeSettings settings)
        {
            if (settings == null)
                return;
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { settings }, SettingsPath, true);
            _cachedSettings = settings;
        }

        internal static void ApplySettings(TraceForgeSettings settings)
        {
            if (settings == null)
                return;
            if (EditorApplication.isPlaying)
            {
                Bootstrap.ApplySettings(settings);
                return;
            }
            Logger.SetMinVerbosity(settings.MinVerbosity);
            Logger.ClearAllCategoryVerbosities();
            if (settings.CategoryOverrides != null)
            {
                foreach (var categoryOverride in settings.CategoryOverrides)
                    Logger.SetCategoryVerbosity(new LogCategory(categoryOverride.Category), categoryOverride.Verbosity);
            }
        }

        internal static void ApplySavedSettings()
        {
            ApplySettings(LoadSettingsIfExists());
        }

        internal static void ClearCache()
        {
            if (_cachedSettings != null)
                Object.DestroyImmediate(_cachedSettings);
            _cachedSettings = null;
        }

        private static void DrawGUI(string searchContext)
        {
            var settings = GetOrCreateSettings();
            using var serialized = new SerializedObject(settings);
            serialized.Update();
            EditorGUILayout.LabelField("TraceForge Logger Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serialized.FindProperty("MinVerbosity"), new GUIContent("Global Min Verbosity"));
            EditorGUILayout.PropertyField(serialized.FindProperty("CategoryOverrides"), new GUIContent("Category Overrides"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sinks", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("EnableRingBuffer"));
            EditorGUILayout.PropertyField(serialized.FindProperty("RingBufferCapacity"));
            EditorGUILayout.PropertyField(serialized.FindProperty("EnableFileSink"));
            EditorGUILayout.PropertyField(serialized.FindProperty("FilePath"));
            EditorGUILayout.PropertyField(serialized.FindProperty("AppendToFile"));
            EditorGUILayout.PropertyField(serialized.FindProperty("FileQueueCapacity"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity Integration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("CaptureUnityLog"));
            EditorGUILayout.PropertyField(serialized.FindProperty("StackTracePolicy"));
            if (serialized.ApplyModifiedProperties())
            {
                SaveSettings(settings);
                ApplySettings(settings);
            }
        }
    }
}
