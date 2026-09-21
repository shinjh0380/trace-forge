using NUnit.Framework;
using System.IO;
using TraceForge.Editor;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public class SettingsAssetTests
    {
        private bool _settingsExisted;
        private byte[] _settingsBytes;

        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            TraceForgeSettingsProvider.ClearCache();
            _settingsExisted = File.Exists(TraceForgeSettingsProvider.SettingsPath);
            _settingsBytes = _settingsExisted ? File.ReadAllBytes(TraceForgeSettingsProvider.SettingsPath) : null;
            if (_settingsExisted)
                File.Delete(TraceForgeSettingsProvider.SettingsPath);
        }

        [TearDown]
        public void TearDown()
        {
            TF.Reset();
            TraceForgeSettingsProvider.ClearCache();
            if (_settingsExisted)
                File.WriteAllBytes(TraceForgeSettingsProvider.SettingsPath, _settingsBytes);
            else if (File.Exists(TraceForgeSettingsProvider.SettingsPath))
                File.Delete(TraceForgeSettingsProvider.SettingsPath);
        }

        [Test]
        public void GetOrCreateSettings_UsesSchemaDefaults()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            Assert.AreEqual(Verbosity.Debug, settings.MinVerbosity);
            Assert.IsTrue(settings.EnableRingBuffer);
            Assert.AreEqual(1024, settings.RingBufferCapacity);
            Assert.IsFalse(settings.EnableFileSink);
        }

        [Test]
        public void SaveAndReloadSettings_PreservesSerializedValuesAndAppliesImmediately()
        {
            var settings = TraceForgeSettingsProvider.GetOrCreateSettings();
            settings.MinVerbosity = Verbosity.Error;
            settings.EnableRingBuffer = false;
            TraceForgeSettingsProvider.SaveSettings(settings);
            TraceForgeSettingsProvider.ApplySettings(settings);
            Assert.AreEqual(Verbosity.Error, Logger.GetMinVerbosity());

            TraceForgeSettingsProvider.ClearCache();
            var loaded = TraceForgeSettingsProvider.LoadSettingsIfExists();
            Assert.AreEqual(Verbosity.Error, loaded.MinVerbosity);
            Assert.IsFalse(loaded.EnableRingBuffer);
        }

        [Test]
        public void EditorBootstrap_CreatesOnlyWhenRegistryIsEmpty_AndReleasesOwnedRing()
        {
            TraceForgeEditorBootstrap.ReleaseForTests();
            TF.Reset();
            TraceForgeEditorBootstrap.EnsureForTests();
            Assert.AreEqual(1, SinkRegistry.RingBuffers.Length);
            var existing = new RingBufferSink(9);
            TF.AddSink(existing);
            TraceForgeEditorBootstrap.EnsureForTests();
            Assert.AreEqual(2, SinkRegistry.RingBuffers.Length);
            TraceForgeEditorBootstrap.ReleaseForTests();
            TF.RemoveSink(existing);
            Assert.AreEqual(0, SinkRegistry.RingBuffers.Length);
        }
    }
}
