using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using TraceForge.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public class BuildProcessorTests
    {
        private UnityEngine.Object[] _previous;
        private bool _settingsExisted;
        private byte[] _settingsBytes;
        private string _markerPath;

        [SetUp]
        public void SetUp()
        {
            _markerPath = "Assets/TraceForgeBuildProcessorTest-" + Guid.NewGuid().ToString("N") + ".asset";
            _previous = PlayerSettings.GetPreloadedAssets() ?? System.Array.Empty<UnityEngine.Object>();
            _settingsExisted = File.Exists(TraceForgeSettingsProvider.SettingsPath);
            _settingsBytes = _settingsExisted ? File.ReadAllBytes(TraceForgeSettingsProvider.SettingsPath) : null;
        }

        [TearDown]
        public void TearDown()
        {
            TraceForgeBuildProcessor.RecoverPersistedStateForTests();
            PlayerSettings.SetPreloadedAssets(_previous);
            TraceForgeSettingsProvider.ClearCache();
            if (_settingsExisted)
                File.WriteAllBytes(TraceForgeSettingsProvider.SettingsPath, _settingsBytes);
            else if (File.Exists(TraceForgeSettingsProvider.SettingsPath))
                File.Delete(TraceForgeSettingsProvider.SettingsPath);
            AssetDatabase.DeleteAsset(_markerPath);
        }

        [Test]
        public void ExistingSettingsAssetInPreloadedAssets_IsUnchanged()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            var imported = UnityEngine.Object.Instantiate(settings);
            imported.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(imported, _markerPath);
            var assets = new[] { (UnityEngine.Object)imported };
            PlayerSettings.SetPreloadedAssets(assets);
            new TraceForgeBuildProcessor().OnPreprocessBuild(null);
            CollectionAssert.AreEqual(assets, PlayerSettings.GetPreloadedAssets());
            new TraceForgeBuildProcessor().OnPostprocessBuild(null);
            CollectionAssert.AreEqual(assets, PlayerSettings.GetPreloadedAssets());
        }

        [Test]
        public void PreprocessThenPostprocess_RestoresExactPreviousList()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            settings.EnableRingBuffer = true;
            TraceForgeSettingsProvider.SaveSettings(settings);
            var marker = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(marker, _markerPath);
            AssetDatabase.SaveAssets();
            PlayerSettings.SetPreloadedAssets(new UnityEngine.Object[] { marker });
            new TraceForgeBuildProcessor().OnPreprocessBuild(null);
            Assert.AreEqual(2, PlayerSettings.GetPreloadedAssets().Length);
            new TraceForgeBuildProcessor().OnPostprocessBuild(null);
            CollectionAssert.AreEqual(new UnityEngine.Object[] { marker }, PlayerSettings.GetPreloadedAssets());
        }

        [UnityTest]
        public IEnumerator FailedBuildWithoutPostprocess_RestoresOnEditorUpdate()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            TraceForgeSettingsProvider.SaveSettings(settings);
            var before = PlayerSettings.GetPreloadedAssets() ?? System.Array.Empty<UnityEngine.Object>();
            new TraceForgeBuildProcessor().OnPreprocessBuild(null);
            var during = PlayerSettings.GetPreloadedAssets();
            Assert.AreEqual(before.Length + 1, during.Length);
            var temporaryPath = AssetDatabase.GetAssetPath(during[during.Length - 1]);
            // A failed build omits the postprocess callback. Its next idle update must recover.
            var deadline = EditorApplication.timeSinceStartup + 2d;
            while (File.Exists("Library/TraceForgeBuildState.json") && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(before, PlayerSettings.GetPreloadedAssets());
            Assert.IsFalse(File.Exists(temporaryPath));
            Assert.IsFalse(File.Exists("Library/TraceForgeBuildState.json"));
        }

        [Test]
        public void PersistedState_RestoresAfterReloadWithoutPostprocess()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            TraceForgeSettingsProvider.SaveSettings(settings);
            var marker = new Texture2D(2, 2);
            AssetDatabase.CreateAsset(marker, _markerPath);
            AssetDatabase.SaveAssets();
            var before = new UnityEngine.Object[] { marker, null, marker };
            PlayerSettings.SetPreloadedAssets(before);
            new TraceForgeBuildProcessor().OnPreprocessBuild(null);
            Assert.AreEqual(4, PlayerSettings.GetPreloadedAssets().Length);
            TraceForgeBuildProcessor.RecoverPersistedStateForTests();
            CollectionAssert.AreEqual(before, PlayerSettings.GetPreloadedAssets());
            Assert.IsFalse(File.Exists("Library/TraceForgeBuildState.json"));
        }

    }
}
