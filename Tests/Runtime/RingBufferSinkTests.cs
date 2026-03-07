using System;
using NUnit.Framework;

namespace TraceForge.Tests
{
    [TestFixture]
    public class RingBufferSinkTests
    {
        private RingBufferSink _sink;

        [SetUp]
        public void SetUp()
        {
            TF.Reset();
            _sink = new RingBufferSink(4);
            TF.AddSink(_sink);
        }

        [TearDown]
        public void TearDown() => TF.Reset();

        [Test]
        public void InitialState_IsEmpty()
        {
            Assert.AreEqual(0, _sink.Count);
            Assert.AreEqual(0, _sink.GetEntries().Length);
        }

        [Test]
        public void Write_SingleEntry_RetrievableViaGetEntries()
        {
            TF.Info("first");
            var entries = _sink.GetEntries();
            Assert.AreEqual(1, entries.Length);
            Assert.AreEqual("first", entries[0].Message);
        }

        [Test]
        public void Write_UpToCapacity_AllEntriesPresent()
        {
            TF.Info("a");
            TF.Info("b");
            TF.Info("c");
            TF.Info("d");
            Assert.AreEqual(4, _sink.Count);
            Assert.AreEqual(4, _sink.GetEntries().Length);
        }

        [Test]
        public void Write_BeyondCapacity_OldestEntryEvicted()
        {
            TF.Info("a");
            TF.Info("b");
            TF.Info("c");
            TF.Info("d");
            TF.Info("e"); // evicts "a"

            var entries = _sink.GetEntries();
            Assert.AreEqual(4, entries.Length);
            Assert.AreEqual("b", entries[0].Message, "Oldest entry should be 'b'");
            Assert.AreEqual("e", entries[3].Message, "Newest entry should be 'e'");
        }

        [Test]
        public void GetEntries_ReturnsOldestFirst()
        {
            TF.Info("first");
            TF.Info("second");
            TF.Info("third");

            var entries = _sink.GetEntries();
            Assert.AreEqual("first", entries[0].Message);
            Assert.AreEqual("second", entries[1].Message);
            Assert.AreEqual("third", entries[2].Message);
        }

        [Test]
        public void Clear_ResetsBuffer()
        {
            TF.Info("before clear");
            _sink.Clear();
            Assert.AreEqual(0, _sink.Count);
            Assert.AreEqual(0, _sink.GetEntries().Length);
        }

        [Test]
        public void InvalidCapacity_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBufferSink(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBufferSink(-1));
        }

        [Test]
        public void Capacity_ReflectsConstructorArgument()
        {
            var sink = new RingBufferSink(100);
            Assert.AreEqual(100, sink.Capacity);
        }
    }
}
