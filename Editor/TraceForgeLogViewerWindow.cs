using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace TraceForge.Editor
{
    internal sealed class TraceForgeLogViewerWindow : EditorWindow
    {
        private RingBufferSink _ringBuffer;
        private RingBufferSink[] _ringBuffers = Array.Empty<RingBufferSink>();
        private int _selectedRingBufferIndex = -1;
        private LogEntry[] _entries = Array.Empty<LogEntry>();
        private Vector2 _scrollPos;
        private Verbosity _filterVerbosity = Verbosity.Trace;
        private string _filterCategory = string.Empty;
        private double _lastRefreshTime;
        private int _refreshRequested;
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
            SinkRegistry.Changed += OnRegistryChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Reacquire();
        }

        private void OnDisable()
        {
            SinkRegistry.Changed -= OnRegistryChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _ringBuffer = null;
            _ringBuffers = Array.Empty<RingBufferSink>();
            _entries = Array.Empty<LogEntry>();
            _selectedRingBufferIndex = -1;
            Interlocked.Exchange(ref _refreshRequested, 0);
        }

        private void Update()
        {
            if (Interlocked.Exchange(ref _refreshRequested, 0) != 0)
                Reacquire();
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

            if (_ringBuffers.Length > 0)
            {
                var names = new string[_ringBuffers.Length];
                for (var i = 0; i < names.Length; i++)
                    names[i] = "Ring Buffer " + (i + 1) + " (" + _ringBuffers[i].Count + ")";
                var selected = EditorGUILayout.Popup(_selectedRingBufferIndex, names, GUILayout.Width(145));
                if (selected != _selectedRingBufferIndex)
                    SelectRingBuffer(selected);
            }

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
            if (_ringBuffer == null)
            {
                EditorGUILayout.HelpBox("No RingBufferSink registered. Phase 2 bootstrap registers one automatically.", MessageType.Info);
                return;
            }

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
            _entries = _ringBuffer == null ? Array.Empty<LogEntry>() : _ringBuffer.GetEntries();
        }

        private void Reacquire()
        {
            _ringBuffers = SinkRegistry.RingBuffers;
            if (_ringBuffers.Length == 0)
            {
                _ringBuffer = null;
                _selectedRingBufferIndex = -1;
                _entries = Array.Empty<LogEntry>();
                return;
            }
            var selected = Array.IndexOf(_ringBuffers, _ringBuffer);
            if (selected < 0)
                selected = 0;
            _selectedRingBufferIndex = selected;
            _ringBuffer = _ringBuffers[selected];
            RefreshEntries();
        }

        private void OnRegistryChanged()
        {
            Interlocked.Exchange(ref _refreshRequested, 1);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Reacquire();
        }

        private void SelectRingBuffer(int index)
        {
            if (index < 0 || index >= _ringBuffers.Length)
                return;
            _selectedRingBufferIndex = index;
            _ringBuffer = _ringBuffers[index];
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
