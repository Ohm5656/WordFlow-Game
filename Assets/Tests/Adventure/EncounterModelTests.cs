using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class EncounterModelTests
    {
        private static EncounterModel Make() => new EncounterModel(new[] { "ก", "า" });

        [Test]
        public void New_TrayFull_SlotsEmpty_NotComplete()
        {
            var m = Make();
            Assert.AreEqual(2, m.SlotCount);
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.IsInTray(1));
        }

        [Test]
        public void PlaceInOrder_BuildsTargetString()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(0)); // -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void PlacementOrderDeterminesString()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(1)); // slot 0 = า
            Assert.IsTrue(m.PlaceFromTray(0)); // slot 1 = ก
            Assert.AreEqual("าก", m.BuiltString);
        }

        [Test]
        public void PlaceAlreadyPlacedTile_Fails()
        {
            var m = Make();
            Assert.IsTrue(m.PlaceFromTray(0));
            Assert.IsFalse(m.PlaceFromTray(0));
            Assert.IsFalse(m.IsInTray(0));
        }

        [Test]
        public void PlaceWhenSlotsFull_Fails()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            Assert.IsFalse(m.PlaceFromTray(0)); // none left anyway
        }

        [Test]
        public void ReturnSlot_FreesTileBackToTray()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            Assert.IsTrue(m.ReturnSlot(0)); // returns tile that was in slot 0 (tray idx 0)
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.PlaceFromTray(0)); // re-placeable, goes to first empty slot (0)
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void ResetAll_ReturnsEverythingToTray()
        {
            var m = Make();
            m.PlaceFromTray(0); m.PlaceFromTray(1);
            m.ResetToTray();
            Assert.IsFalse(m.IsComplete);
            Assert.IsTrue(m.IsInTray(0));
            Assert.IsTrue(m.IsInTray(1));
        }

        [Test]
        public void SlotTrayIndex_ReportsWhichTileIsWhere()
        {
            var m = Make();
            m.PlaceFromTray(1);
            Assert.AreEqual(1, m.SlotTrayIndex(0)); // tray tile 1 sits in slot 0
            Assert.AreEqual(-1, m.SlotTrayIndex(1)); // slot 1 empty
        }

        // ---- distractor: 3 tray tiles, 2 slots (ก distractor on the ปา encounter) ----

        private static EncounterModel MakeDistractor() =>
            new EncounterModel(new[] { "ป", "า", "ก" }, slotCount: 2);

        [Test]
        public void Distractor_ThreeTilesTwoSlots_SlotCountIsTwo_TrayCountIsThree()
        {
            var m = MakeDistractor();
            Assert.AreEqual(2, m.SlotCount);
            Assert.AreEqual(3, m.TrayCount);
            Assert.IsFalse(m.IsComplete);
        }

        [Test]
        public void Distractor_BuildTarget_IgnoringDistractor_Completes()
        {
            var m = MakeDistractor();
            Assert.IsTrue(m.PlaceFromTray(0)); // ป -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // า -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("ปา", m.BuiltString);
            Assert.IsTrue(m.IsInTray(2));      // ก distractor never required, still in tray
        }

        [Test]
        public void Distractor_BuildWrongRealWord_WithDistractor_Completes()
        {
            var m = MakeDistractor();
            Assert.IsTrue(m.PlaceFromTray(2)); // ก -> slot 0
            Assert.IsTrue(m.PlaceFromTray(1)); // า -> slot 1
            Assert.IsTrue(m.IsComplete);
            Assert.AreEqual("กา", m.BuiltString);
        }

        [Test]
        public void Distractor_ReturningASlot_FreesTheRightTile()
        {
            var m = MakeDistractor();
            m.PlaceFromTray(2); // ก -> slot 0
            m.PlaceFromTray(1); // า -> slot 1
            Assert.IsTrue(m.ReturnSlot(0));     // free slot 0 (held ก, tray idx 2)
            Assert.IsTrue(m.IsInTray(2));       // ก back in tray
            Assert.IsTrue(m.PlaceFromTray(0));  // ป -> first empty slot (0)
            Assert.AreEqual("ปา", m.BuiltString);
        }

        [Test]
        public void Distractor_SlotCountGuards_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new EncounterModel(new[] { "ก", "า" }, slotCount: 0));
            Assert.Throws<System.ArgumentException>(() => new EncounterModel(new[] { "ก", "า" }, slotCount: 3));
        }
    }
}
