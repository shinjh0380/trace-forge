using System;
using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class LogEntryTests
    {
        [SetUp]
        public void SetUp() => TF.Reset();

        [TearDown]
        public void TearDown() => TF.Reset();

        [Test]
        public void LogEntry_StoresAllFields()
        {
            var ex = new InvalidOperationException("test");
            var ticks = DateTime.UtcNow.Ticks;
            var entry = new LogEntry(Verbosity.Error, Categories.Network, "message", ex, ticks);

            Assert.AreEqual(Verbosity.Error, entry.Verbosity);
            Assert.AreEqual("Network", entry.Category.Name);
            Assert.AreEqual("message", entry.Message);
            Assert.AreEqual(ex, entry.Exception);
            Assert.AreEqual(ticks, entry.TimestampTicks);
        }

        [Test]
        public void LogEntry_Exception_CanBeNull()
        {
            var entry = new LogEntry(Verbosity.Info, Categories.Default, "no exception", null, DateTime.UtcNow.Ticks);
            Assert.IsNull(entry.Exception);
        }

        [Test]
        public void LogCategory_Equality()
        {
            var cat1 = new LogCategory("Network");
            var cat2 = new LogCategory("Network");
            var cat3 = new LogCategory("Audio");

            Assert.IsTrue(cat1 == cat2);
            Assert.IsFalse(cat1 == cat3);
            Assert.IsTrue(cat1.Equals(cat2));
            Assert.AreEqual(cat1.GetHashCode(), cat2.GetHashCode());
        }

        [Test]
        public void LogCategory_NullName_EqualsEmptyString()
        {
            var catNull = new LogCategory(null);
            var catEmpty = new LogCategory("");
            Assert.IsTrue(catNull == catEmpty);
        }

        [Test]
        public void LogCategory_ToString_ReturnsName()
        {
            var cat = new LogCategory("Physics");
            Assert.AreEqual("Physics", cat.ToString());
        }

        [Test]
        public void Categories_PredefinedValues_HaveCorrectNames()
        {
            Assert.AreEqual("Default", Categories.Default.Name);
            Assert.AreEqual("Gameplay", Categories.Gameplay.Name);
            Assert.AreEqual("Network", Categories.Network.Name);
            Assert.AreEqual("UI", Categories.UI.Name);
            Assert.AreEqual("Audio", Categories.Audio.Name);
            Assert.AreEqual("Physics", Categories.Physics.Name);
            Assert.AreEqual("AI", Categories.AI.Name);
            Assert.AreEqual("Performance", Categories.Performance.Name);
        }

        [Test]
        public void Verbosity_Off_IsHighestValue()
        {
            Assert.Greater((int)Verbosity.Off, (int)Verbosity.Fatal);
        }
    }
}
