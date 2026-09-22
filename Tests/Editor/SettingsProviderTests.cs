using NUnit.Framework;
using System.IO;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public class SettingsProviderTests
    {
        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            TF.AddSink(new RingBufferSink());
            _settingsExisted = File.Exists(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath);
            _settingsBytes = _settingsExisted ? File.ReadAllBytes(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath) : null;
        }

        private bool _settingsExisted;
        private byte[] _settingsBytes;

        [TearDown]
        public void TearDown()
        {
            TF.Reset();
            TraceForge.Editor.TraceForgeSettingsProvider.ClearCache();
            if (_settingsExisted)
                File.WriteAllBytes(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath, _settingsBytes);
            else if (File.Exists(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath))
                File.Delete(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath);
        }

        [Test]
        public void ApplySavedSettings_AppliesDefaultVerbosityWhenNoSettingsAssetExists()
        {
            if (File.Exists(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath))
                File.Delete(TraceForge.Editor.TraceForgeSettingsProvider.SettingsPath);
            TraceForge.Editor.TraceForgeSettingsProvider.ApplySavedSettings();
            Assert.AreEqual(!TF.IsDebugStripped, TF.IsEnabled(Verbosity.Debug));
        }

        [Test]
        public void ApplySavedSettings_AppliesAssetVerbosity()
        {
            var settings = TraceForge.Editor.TraceForgeSettingsProvider.GetOrCreateSettings();
            settings.MinVerbosity = Verbosity.Warning;
            TraceForge.Editor.TraceForgeSettingsProvider.SaveSettings(settings);
            TraceForge.Editor.TraceForgeSettingsProvider.ApplySavedSettings();
            Assert.IsFalse(TF.IsEnabled(Verbosity.Info));
            Assert.IsTrue(TF.IsEnabled(Verbosity.Warning));
        }
    }
}
