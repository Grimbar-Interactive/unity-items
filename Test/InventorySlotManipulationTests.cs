using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventorySlotManipulationTests
    {
        [Test]
        public void Swap_TwoOccupiedSlots_ExchangesContents()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.Swap(0, 1);

            Assert.AreEqual("b", inventory[0].Item.UniqueId);
            Assert.AreEqual("a", inventory[1].Item.UniqueId);
        }

        [Test]
        public void Swap_OccupiedAndEmpty_MovesTheItem()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            inventory.Swap(0, 1);

            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.AreEqual("a", inventory[1].Item.UniqueId);
        }

        [Test]
        public void Swap_SameIndex_ReturnsFalse_AndFiresNoEvents()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            var result = inventory.Swap(0, 0);

            Assert.IsFalse(result);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void Swap_DoesNotReplaceSlotObjects()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            var slotA = inventory[0];
            var slotB = inventory[1];

            inventory.Swap(0, 1);

            Assert.AreSame(slotA, inventory[0]);
            Assert.AreSame(slotB, inventory[1]);
        }

        [Test]
        public void Swap_TwoEmptySlots_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            Assert.IsFalse(inventory.Swap(0, 1));
        }

        [Test]
        public void Move_ToEmptySlot_RelocatesStack()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            inventory.Move(0, 1);

            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.AreEqual(5, inventory[1].Quantity);
        }

        [Test]
        public void Move_ToStackableMatchWithRoom_MergesUpToStackLimit()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 7);
            inventory.SplitStack(0, 4, 1);

            inventory.Move(0, 1);

            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.AreEqual(7, inventory[1].Quantity);
        }

        [Test]
        public void Move_ToStackableMatch_PartialMerge_LeavesRemainderInSource()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.Move(0, 1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 8);
            inventory.TryRemoveAt(1, 3);

            inventory.Move(0, 1);

            Assert.AreEqual(1, inventory[0].Quantity);
            Assert.AreEqual(10, inventory[1].Quantity);
        }

        [Test]
        public void Move_ToFullStackableMatch_SwapsInstead()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10);
            inventory.Move(0, 1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);

            inventory.Move(0, 1);

            Assert.AreEqual(10, inventory[0].Quantity);
            Assert.AreEqual(3, inventory[1].Quantity);
        }

        [Test]
        public void Move_ToNonMatchingOccupiedSlot_Swaps()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.Move(0, 1);

            Assert.AreEqual("b", inventory[0].Item.UniqueId);
            Assert.AreEqual("a", inventory[1].Item.UniqueId);
        }

        [Test]
        public void Move_SameIndex_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            Assert.IsFalse(inventory.Move(0, 0));
        }

        [Test]
        public void Move_FromEmptySlot_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            Assert.IsFalse(inventory.Move(0, 1));
        }

        [Test]
        public void Move_Unbounded_FullMerge_RemovesSourceSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2);

            inventory.Move(1, 0);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void Move_Unbounded_FullMerge_DestinationAfterSource_ReportsShiftedIndex()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Move(0, 1);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(6, inventory[0].Quantity);
            foreach (var index in recorder.ChangedSlots) Assert.Less(index, inventory.SlotCount);
        }

        [Test]
        public void Move_Fixed_FullMerge_KeepsSlotCount()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2);

            inventory.Move(1, 0);

            Assert.AreEqual(3, inventory.SlotCount);
            Assert.IsTrue(inventory[1].IsEmpty);
        }

        [Test]
        public void Move_Unbounded_ReordersSlots()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.Move(0, 1);

            Assert.AreEqual("b", inventory[0].Item.UniqueId);
            Assert.AreEqual("a", inventory[1].Item.UniqueId);
        }

        [Test]
        public void SplitStack_MovesQuantityToFirstEmptySlot()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            inventory.SplitStack(0, 2);

            Assert.AreEqual(4, inventory[0].Quantity);
            Assert.AreEqual(2, inventory[1].Quantity);
        }

        [Test]
        public void SplitStack_LeavesRemainderInSource()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            inventory.SplitStack(0, 2);

            Assert.AreEqual(4, inventory[0].Quantity);
        }

        [Test]
        public void SplitStack_Unbounded_AppendsNewSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            inventory.SplitStack(0, 2);

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void SplitStack_NonPositiveQuantity_ReturnsFalse(int quantity)
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            Assert.IsFalse(inventory.SplitStack(0, quantity));
        }

        [Test]
        public void SplitStack_QuantityEqualToStackQuantity_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            Assert.IsFalse(inventory.SplitStack(0, 6));
        }

        [Test]
        public void SplitStack_QuantityGreaterThanStackQuantity_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            Assert.IsFalse(inventory.SplitStack(0, 7));
        }

        [Test]
        public void SplitStack_EmptySlot_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            Assert.IsFalse(inventory.SplitStack(0, 1));
        }

        [Test]
        public void SplitStack_Fixed_NoEmptySlot_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            Assert.IsFalse(inventory.SplitStack(0, 2));
        }

        [Test]
        public void SplitStack_NonStackableItem_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsFalse(inventory.SplitStack(0, 1));
        }

        [Test]
        public void SplitStack_ToExplicitDestination_UsesThatSlot()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);

            inventory.SplitStack(0, 2, 2);

            Assert.AreEqual(2, inventory[2].Quantity);
            Assert.IsTrue(inventory[1].IsEmpty);
        }

        [Test]
        public void SplitStack_ToOccupiedDestination_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 1);

            Assert.IsFalse(inventory.SplitStack(0, 2, 1));
        }

        [Test]
        public void MergeStacks_SameKind_CombinesIntoDestination()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            inventory.MergeStacks(1, 0);

            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void MergeStacks_FullyMerged_EmptiesSource()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);

            inventory.MergeStacks(1, 0);

            Assert.IsTrue(inventory[1].IsEmpty);
        }

        [Test]
        public void MergeStacks_PartialMerge_FillsDestinationToLimit_AndLeavesRemainder()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.Move(0, 1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 8);
            inventory.TryRemoveAt(1, 3);

            inventory.MergeStacks(0, 1);

            Assert.AreEqual(10, inventory[1].Quantity);
            Assert.AreEqual(1, inventory[0].Quantity);
        }

        [Test]
        public void MergeStacks_DifferentUniqueIds_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 3);

            Assert.IsFalse(inventory.MergeStacks(0, 1));
        }

        [Test]
        public void MergeStacks_NonStackableItems_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsFalse(inventory.MergeStacks(0, 1));
        }

        [Test]
        public void MergeStacks_FullDestination_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsFalse(inventory.MergeStacks(1, 0));
        }

        [Test]
        public void MergeStacks_EmptySource_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsFalse(inventory.MergeStacks(1, 0));
        }

        [Test]
        public void MergeStacks_EmptyDestination_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsFalse(inventory.MergeStacks(0, 1));
        }

        [Test]
        public void MergeStacks_SameIndex_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsFalse(inventory.MergeStacks(0, 0));
        }

        [Test]
        public void MergeStacks_RespectsCanStackWithOverride()
        {
            var inventory = new Inventory<DurabilityTestItem>(2);
            var attributes = new FakeItemAttributes("disc", maxStackSize: 10);
            inventory.TryAdd(new DurabilityTestItem(attributes, 40), 1);
            inventory.TryAdd(new DurabilityTestItem(attributes, 100), 1);

            Assert.IsFalse(inventory.MergeStacks(0, 1));
        }
    }
}
