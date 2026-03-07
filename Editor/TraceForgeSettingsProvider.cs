using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TraceForge.Editor
{
    internal static class TraceForgeSettingsProvider
    {
        private const string EditorPrefsKeyPrefix = "TraceForge.";
        private const string MinVerbosityKey = EditorPrefsKeyPrefix + "MinVerbosity";

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/TraceForge", SettingsScope.Project)
            {
                label = "TraceForge",
                guiHandler = (searchContext) => DrawGUI(),
                keywords = new HashSet<string>(new[] { "TraceForge", "Logging", "Verbosity", "Sink" })
            };
        }

        private static void DrawGUI()
        {
            EditorGUILayout.LabelField("TraceForge Logger Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Min Verbosity
            var currentVerbosity = (Verbosity)EditorPrefs.GetInt(MinVerbosityKey, (int)Verbosity.Debug);
            var newVerbosity = (Verbosity)EditorGUILayout.EnumPopup("Global Min Verbosity", currentVerbosity);

            if (newVerbosity != currentVerbosity)
            {
                EditorPrefs.SetInt(MinVerbosityKey, (int)newVerbosity);
                TF.SetMinVerbosity(newVerbosity);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Sinks", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sinks are registered at runtime. Use TF.AddSink() in your initialization code.",
                MessageType.Info
            );
        }

        internal static void ApplySavedSettings()
        {
            var verbosity = (Verbosity)EditorPrefs.GetInt(MinVerbosityKey, (int)Verbosity.Debug);
            TF.SetMinVerbosity(verbosity);
        }
    }
}
