using NUnit.Framework;
using UnityEditor;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public class SettingsProviderTests
    {
        [SetUp]
        public void SetUp() => TF.Reset();

        [TearDown]
        public void TearDown()
        {
            TF.Reset();
            // Clean up EditorPrefs test keys
            EditorPrefs.DeleteKey("TraceForge.MinVerbosity");
        }

        [Test]
        public void ApplySavedSettings_AppliesDefaultVerbosityWhenNoPrefsSet()
        {
            EditorPrefs.DeleteKey("TraceForge.MinVerbosity");
            TraceForge.Editor.TraceForgeSettingsProvider.ApplySavedSettings();
            Assert.IsTrue(TF.IsEnabled(Verbosity.Debug));
        }

        [Test]
        public void ApplySavedSettings_AppliesSavedVerbosity()
        {
            EditorPrefs.SetInt("TraceForge.MinVerbosity", (int)Verbosity.Warning);
            TraceForge.Editor.TraceForgeSettingsProvider.ApplySavedSettings();
            Assert.IsFalse(TF.IsEnabled(Verbosity.Info));
            Assert.IsTrue(TF.IsEnabled(Verbosity.Warning));
        }
    }
}
