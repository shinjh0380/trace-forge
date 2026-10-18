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
            var windows = Resources.FindObjectsOfTypeAll<TraceForgeLogViewerWindow>();
            if (windows.Length > 0)
                windows[0].ShowNotification(new GUIContent("TraceForge logger reset."));
            else
                EditorUtility.DisplayDialog("TraceForge", "TraceForge logger reset.", "OK");
        }
    }
}
