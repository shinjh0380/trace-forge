using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TraceForge.Editor
{
    [InitializeOnLoad]
    internal sealed class TraceForgeBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const int CallbackOrder = 0;
        private const string StatePath = "Library/TraceForgeBuildState.json";
        private static BuildState _state;

        static TraceForgeBuildProcessor()
        {
            EditorApplication.update += RecoverPersistedState;
        }

        public int callbackOrder => CallbackOrder;

        public void OnPreprocessBuild(BuildReport report)
        {
            EditorApplication.update -= RecoverPersistedState;
            EditorApplication.update += RecoverPersistedState;
            var settings = TraceForgeSettingsProvider.LoadSettingsIfExists();
            if (settings == null)
                return;

            var existing = PlayerSettings.GetPreloadedAssets() ?? Array.Empty<UnityEngine.Object>();
            if (ContainsSettings(existing))
                return;

            _state = new BuildState();
            _state.PreviousAssetIds = GetAssetIds(existing);
            _state.TemporaryAssetPath = "Assets/TraceForgeSettings.Build." + Guid.NewGuid().ToString("N") + ".asset";
            _state.TemporaryAssetCreated = true;
            SaveState(_state);
            try
            {
                var temporary = UnityEngine.Object.Instantiate(settings);
                temporary.name = "TraceForgeSettings.Build";
                temporary.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(temporary, _state.TemporaryAssetPath);
                AssetDatabase.SaveAssets();
                var next = new UnityEngine.Object[existing.Length + 1];
                Array.Copy(existing, next, existing.Length);
                next[existing.Length] = temporary;
                PlayerSettings.SetPreloadedAssets(next);
                SaveState(_state);
            }
            catch
            {
                RestoreWhenSafe();
                throw;
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreWhenSafe();
        }

        internal static void RestoreWhenSafe()
        {
            RestoreStateIfSafe();
        }

        private static void RecoverPersistedState()
        {
            if (BuildPipeline.isBuildingPlayer)
                return;
            if (_state == null)
                _state = LoadState();
            EditorApplication.update -= RecoverPersistedState;
            if (_state != null)
            {
                RestoreStateIfSafe();
            }
        }

        internal static void RecoverPersistedStateForTests()
        {
            _state = null;
            RecoverPersistedState();
        }

        private static bool ContainsSettings(UnityEngine.Object[] assets)
        {
            foreach (var asset in assets)
            {
                if (asset is TraceForgeSettings)
                    return true;
            }
            return false;
        }

        private static void RestoreStateIfSafe()
        {
            var state = _state ?? LoadState();
            if (state == null)
                return;
            var previous = new UnityEngine.Object[state.PreviousAssetIds == null ? 0 : state.PreviousAssetIds.Length];
            for (var i = 0; i < previous.Length; i++)
            {
                GlobalObjectId id;
                if (GlobalObjectId.TryParse(state.PreviousAssetIds[i], out id))
                    previous[i] = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            }
            PlayerSettings.SetPreloadedAssets(previous);
            if (state.TemporaryAssetCreated && !string.IsNullOrEmpty(state.TemporaryAssetPath))
            {
                AssetDatabase.DeleteAsset(state.TemporaryAssetPath);
                AssetDatabase.SaveAssets();
            }
            if (File.Exists(StatePath))
                File.Delete(StatePath);
            _state = null;
        }

        private static string[] GetAssetIds(UnityEngine.Object[] assets)
        {
            var ids = new string[assets.Length];
            for (var i = 0; i < assets.Length; i++)
                ids[i] = GlobalObjectId.GetGlobalObjectIdSlow(assets[i]).ToString();
            return ids;
        }

        private static void SaveState(BuildState state)
        {
            var directory = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(StatePath, JsonUtility.ToJson(state));
        }

        private static BuildState LoadState()
        {
            if (!File.Exists(StatePath))
                return null;
            return JsonUtility.FromJson<BuildState>(File.ReadAllText(StatePath));
        }

        [Serializable]
        internal sealed class BuildState
        {
            public string[] PreviousAssetIds;
            public bool TemporaryAssetCreated;
            public string TemporaryAssetPath;
        }
    }
}
