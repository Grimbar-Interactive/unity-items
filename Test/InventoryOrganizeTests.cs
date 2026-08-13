using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryOrganizeTests
    {
        [Test]
        public void Consolidate_TwoPartialStacksOfSameKind_BecomeOne()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            inventory.Consolidate();

            Assert.AreEqual(1, inventory.FilledSlotCount);
        }

        [Test]
        public void Consolidate_ReturnsNumberOfSlotsFreed()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            var freed = inventory.Consolidate();

            Assert.AreEqual(1, freed);
        }

        [Test]
        public void Consolidate_RespectsStackLimit()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 12);
            inventory.SplitStack(0, 4, 2);

            inventory.Consolidate();

            Assert.AreEqual(10, inventory[0].Quantity);
            Assert.IsTrue(inventory[1].IsEmpty);
            Assert.AreEqual(2, inventory[2].Quantity);
        }

        [Test]
        public void Consolidate_LeavesNonStackableItemsAlone()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            inventory.Consolidate();

            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        [Test]
        public void Consolidate_LeavesDifferentKindsInSeparateStacks()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 5);

            inventory.Consolidate();

            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        [Test]
        public void Consolidate_NothingToDo_ReturnsZero_AndFiresNoEvents()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            var freed = inventory.Consolidate();

            Assert.AreEqual(0, freed);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void Consolidate_Unbounded_ShrinksSlotList()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2);

            inventory.Consolidate();

            Assert.AreEqual(1, inventory.SlotCount);
        }

        [Test]
        public void Consolidate_Fixed_KeepsSlotCount()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            inventory.Consolidate();

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [Test]
        public void Compact_Fixed_MovesStacksToLowestIndices_PreservingOrder()
        {
            var inventory = new Inventory<TestItem>(4);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            inventory.Swap(0, 3);

            inventory.Compact();

            Assert.AreEqual("b", inventory[0].Item.UniqueId);
            Assert.AreEqual("a", inventory[1].Item.UniqueId);
        }

        [Test]
        public void Compact_Fixed_KeepsSlotCountUnchanged()
        {
            var inventory = new Inventory<TestItem>(4);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.Swap(0, 3);

            inventory.Compact();

            Assert.AreEqual(4, inventory.SlotCount);
        }

        [Test]
        public void Compact_Fixed_AlreadyCompact_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            Assert.IsFalse(inventory.Compact());
        }

        [Test]
        public void Compact_Unbounded_ReturnsFalse_AndIsNoOp()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            Assert.IsFalse(inventory.Compact());
        }

        [Test]
        public void Compact_DoesNotMergeStacks()
        {
            var inventory = new Inventory<TestItem>(4);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);
            inventory.Swap(1, 3);

            inventory.Compact();

            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        [Test]
        public void Sort_OrdersOccupiedSlotsByComparer()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("c"), 1);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));

            Assert.AreEqual("a", inventory[0].Item.UniqueId);
            Assert.AreEqual("b", inventory[1].Item.UniqueId);
            Assert.AreEqual("c", inventory[2].Item.UniqueId);
        }

        [Test]
        public void Sort_IsStableForEqualKeys()
        {
            var inventory = new Inventory<TestItem>(2);
            var a = TestItems.NonStackable("same");
            var b = TestItems.NonStackable("same");
            inventory.TryAdd(a, 1);
            inventory.TryAdd(b, 1);

            inventory.Sort((x, y) => 0);

            Assert.AreSame(a, inventory[0].Item);
            Assert.AreSame(b, inventory[1].Item);
        }

        [Test]
        public void Sort_Fixed_PushesEmptySlotsToEnd()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.Swap(0, 2);

            inventory.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));

            Assert.IsFalse(inventory[0].IsEmpty);
            Assert.IsTrue(inventory[1].IsEmpty);
            Assert.IsTrue(inventory[2].IsEmpty);
        }

        [Test]
        public void Sort_DoesNotChangeQuantities()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("b", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("a", max: 10), 3);

            inventory.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));

            Assert.AreEqual(3, inventory[0].Quantity);
            Assert.AreEqual(5, inventory[1].Quantity);
        }

        [Test]
        public void Sort_DoesNotMergeStacks()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            inventory.Sort((x, y) => 0);

            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        [Test]
        public void Sort_ComparisonOverload_MatchesComparerOverload()
        {
            var inventoryA = new Inventory<TestItem>(2);
            inventoryA.TryAdd(TestItems.NonStackable("b"), 1);
            inventoryA.TryAdd(TestItems.NonStackable("a"), 1);

            var inventoryB = new Inventory<TestItem>(2);
            inventoryB.TryAdd(TestItems.NonStackable("b"), 1);
            inventoryB.TryAdd(TestItems.NonStackable("a"), 1);

            inventoryA.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));
            inventoryB.Sort(Comparer<TestItem>.Create((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId)));

            Assert.AreEqual(inventoryA[0].Item.UniqueId, inventoryB[0].Item.UniqueId);
            Assert.AreEqual(inventoryA[1].Item.UniqueId, inventoryB[1].Item.UniqueId);
        }

        [Test]
        public void Sort_NullComparer_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.Sort((IComparer<TestItem>)null));
        }

        [Test]
        public void Sort_EmptyInventory_DoesNotThrow_AndFiresNoEvents()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            Assert.DoesNotThrow(() => inventory.Sort((x, y) => 0));
            Assert.AreEqual(0, recorder.UpdatedCount);
        }
    }
}
