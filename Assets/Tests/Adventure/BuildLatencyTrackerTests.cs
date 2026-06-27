using System.Collections.Generic;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class BuildLatencyTrackerTests
    {
        [Test]
        public void Idle_HasNoResult()
        {
            var t = new BuildLatencyTracker();
            Assert.IsFalse(t.HasResult);
            Assert.AreEqual(0, t.PerTilePlacementMs.Count);
        }

        [Test]
        public void TwoTiles_TotalAndPerTile_AreMilliseconds()
        {
            var t = new BuildLatencyTracker();
            t.Start(10.0);
            t.RecordPlacement(11.0);   // 1.0s after start
            t.RecordPlacement(12.5);   // 1.5s after previous placement
            t.Complete(12.5);
            Assert.IsTrue(t.HasResult);
            Assert.AreEqual(2500, t.TotalMs);
            CollectionAssert.AreEqual(new List<long> { 1000, 1500 }, t.PerTilePlacementMs);
        }

        [Test]
        public void Reset_ClearsResultAndPlacements()
        {
            var t = new BuildLatencyTracker();
            t.Start(0.0);
            t.RecordPlacement(0.4);
            t.Complete(0.4);
            t.Reset();
            Assert.IsFalse(t.HasResult);
            Assert.AreEqual(0, t.TotalMs);
            Assert.AreEqual(0, t.PerTilePlacementMs.Count);
        }

        [Test]
        public void StartAfterUse_StartsFresh()
        {
            var t = new BuildLatencyTracker();
            t.Start(0.0); t.RecordPlacement(0.5); t.Complete(0.5);
            t.Start(100.0);
            t.RecordPlacement(100.2);
            Assert.IsFalse(t.HasResult);              // not Complete yet this round
            CollectionAssert.AreEqual(new List<long> { 200 }, t.PerTilePlacementMs);
        }
    }
}
