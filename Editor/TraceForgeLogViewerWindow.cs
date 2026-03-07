using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TraceForge.Editor
{
    internal sealed class TraceForgeLogViewerWindow : EditorWindow
    {
        private RingBufferSink _ringBuffer;
        private LogEntry[] _entries = Array.Empty<LogEntry>();
        private Vector2 _scrollPos;
        private Verbosity _filterVerbosity = Verbosity.Trace;
        private string _filterCategory = string.Empty;
        private double _lastRefreshTime;
        private const double RefreshInterval = 0.25;

        [MenuItem("Window/TraceForge/Log Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<TraceForgeLogViewerWindow>("TraceForge Log Viewer");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            // Try to find an existing RingBufferSink
            RefreshEntries();
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshEntries();
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawEntries();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Min Verbosity:", GUILayout.Width(90));
            _filterVerbosity = (Verbosity)EditorGUILayout.EnumPopup(_filterVerbosity, GUILayout.Width(80));

            EditorGUILayout.LabelField("Category:", GUILayout.Width(60));
            _filterCategory = EditorGUILayout.TextField(_filterCategory, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                _ringBuffer?.Clear();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
                RefreshEntries();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntries()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var entry in _entries)
            {
                if ((int)entry.Verbosity < (int)_filterVerbosity)
                    continue;

                if (!string.IsNullOrEmpty(_filterCategory) &&
                    !entry.Category.Name.StartsWith(_filterCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                var color = GetVerbosityColor(entry.Verbosity);
                var prevColor = GUI.contentColor;
                GUI.contentColor = color;

                var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc).ToString("HH:mm:ss.fff");
                EditorGUILayout.LabelField($"[{timestamp}] [{entry.Verbosity}] [{entry.Category.Name}] {entry.Message}");

                GUI.contentColor = prevColor;
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshEntries()
        {
            if (_ringBuffer != null)
                _entries = _ringBuffer.GetEntries();
        }

        /// <summary>
        /// Attach a RingBufferSink to display its contents in the viewer.
        /// </summary>
        internal void SetRingBuffer(RingBufferSink ringBuffer)
        {
            _ringBuffer = ringBuffer;
            RefreshEntries();
        }

        private static Color GetVerbosityColor(Verbosity verbosity)
        {
            switch (verbosity)
            {
                case Verbosity.Trace: return new Color(0.6f, 0.6f, 0.6f);
                case Verbosity.Debug: return Color.white;
                case Verbosity.Info: return new Color(0.5f, 0.8f, 1f);
                case Verbosity.Warning: return new Color(1f, 0.85f, 0.3f);
                case Verbosity.Error: return new Color(1f, 0.4f, 0.4f);
                case Verbosity.Fatal: return new Color(1f, 0f, 0.3f);
                default: return Color.white;
            }
        }
    }
}
