using UnityEditor;
using UnityEngine;

namespace TraceForge.Editor
{
    internal static class TraceForgeMenuItems
    {
        [MenuItem("Window/TraceForge/Open Settings", priority = 1)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/TraceForge");
        }

        [MenuItem("Window/TraceForge/Reset Logger", priority = 100)]
        private static void ResetLogger()
        {
            TF.Reset();
            // Direct Debug.Log: Editor-only menu action, TF.Log() would be silent after Reset() (no sinks)
            Debug.Log("[TraceForge] Logger reset to defaults.");
        }
    }
}
