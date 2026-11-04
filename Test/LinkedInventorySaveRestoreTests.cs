using System;
using System.Collections.Generic;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventorySaveRestoreTests
    {
        private readonly struct SavedSlot
        {
            public readonly int Index;
            public readonly string UniqueId;
            public readonly Guid? InstanceId;
            public readonly int Quantity;

            public SavedSlot(int index, string uniqueId, Guid? instanceId, int quantity)
            {
                Index = index;
                UniqueId = uniqueId;
                InstanceId = instanceId;
                Quantity = quantity;
            }
        }

        private static List<SavedSlot> Save(LinkedInventory<TestItem> linked)
        {
            var saved = new List<SavedSlot>();
            for (var i = 0; i < linked.SlotCount; i++)
            {
                var slot = linked[i];
                if (!slot.IsEmpty) saved.Add(new SavedSlot(i, slot.UniqueId, slot.InstanceId, slot.Quantity));
            }

            return saved;
        }

        private static int RestoreAt(LinkedInventory<TestItem> linked, List<SavedSlot> saved)
        {
            var failures = 0;
            foreach (var entry in saved)
            {
                var ok = entry.InstanceId.HasValue
                    ? linked.TryAddAt(entry.Index, entry.InstanceId.Value)
                    : linked.TryAddAt(entry.Index, entry.UniqueId, entry.Quantity);
                if (!ok) failures++;
            }

            return failures;
        }

        [Test]
        public void SlotKeys_StackableUsesUniqueId_NonStackableUsesInstanceId()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 2);
            linked.TryAddAt(0, Potion(), 4);
            linked.TryAddAt(1, sword);

            Assert.AreEqual("potion", linked[0].UniqueId);
            Assert.IsNull(linked[0].InstanceId);
            Assert.AreEqual("sword", linked[1].UniqueId);
            Assert.AreEqual(sword.InstanceId, linked[1].InstanceId);
        }

        [Test]
        public void FixedRoundTrip_RestoresEverySlotAtItsSavedIndex()
        {
            var source = SourceWithPotions(25);
            var swordA = AddSword(source);
            var swordB = AddSword(source);
            var original = new LinkedInventory<TestItem>(source, 5);
            original.TryAddAt(0, Potion(), 7);
            original.TryAddAt(2, swordB);
            original.TryAddAt(4, swordA);
            var saved = Save(original);

            var restored = new LinkedInventory<TestItem>(source, 5);
            var failures = RestoreAt(restored, saved);

            Assert.AreEqual(0, failures);
            Assert.AreEqual(7, restored[0].Quantity);
            Assert.AreSame(swordB, restored[2].Item);
            Assert.AreSame(swordA, restored[4].Item);
            Assert.IsTrue(restored[1].IsEmpty);
            Assert.IsTrue(restored[3].IsEmpty);
        }

        [Test]
        public void UnboundedRoundTrip_RestoresInSavedOrder()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var original = new LinkedInventory<TestItem>(source);
            original.TryAdd(sword);
            original.TryAdd(Potion(), 12);
            var saved = Save(original);

            var restored = new LinkedInventory<TestItem>(source);
            foreach (var entry in saved)
            {
                var ok = entry.InstanceId.HasValue
                    ? restored.TryAdd(entry.InstanceId.Value)
                    : restored.TryAdd(entry.UniqueId, entry.Quantity);
                Assert.IsTrue(ok);
            }

            Assert.AreEqual(original.SlotCount, restored.SlotCount);
            Assert.AreSame(sword, restored[0].Item);
            Assert.AreEqual(10, restored[1].Quantity);
            Assert.AreEqual(2, restored[2].Quantity);
        }

        [Test]
        public void Restore_ItemMissingFromTheSource_FailsAndChangesNothing()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);

            Assert.IsFalse(linked.TryAddAt(0, "ghost", 1));
            Assert.IsFalse(linked.TryAddAt(0, Guid.NewGuid()));
            Assert.IsFalse(linked.TryAdd("ghost"));
            Assert.IsFalse(linked.TryAdd(Guid.NewGuid()));
            Assert.IsTrue(linked.IsEmpty);
        }

        [Test]
        public void Restore_MoreThanTheSourceNowHolds_Fails()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(5), 3);

            Assert.IsFalse(linked.TryAddAt(0, "potion", 9));
            Assert.IsTrue(linked.IsEmpty);
        }

        [Test]
        public void Restore_ByUniqueId_PicksTheFirstUnassignedInstanceOfANonStackableKind()
        {
            var source = new Inventory<TestItem>();
            AddSword(source);
            AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsTrue(linked.TryAdd("sword"));
            Assert.IsTrue(linked.TryAdd("sword"));
            Assert.IsFalse(linked.TryAdd("sword"));
            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreNotSame(linked[0].Item, linked[1].Item);
        }

        [Test]
        public void Restore_NonStackableAlreadyAssigned_Fails()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAddAt(0, sword.InstanceId.Value);

            Assert.IsFalse(linked.TryAddAt(1, sword.InstanceId.Value));
        }

        [Test]
        public void Restore_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryAddAt(2, "potion", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryAddAt(-1, Guid.NewGuid()));
        }
    }
}
