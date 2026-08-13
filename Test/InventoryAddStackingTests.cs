using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAddStackingTests
    {
        [Test]
        public void TryAdd_SingleStackableUnit_OccupiesOneSlotWithQuantityOne()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable(), 1);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(1, inventory[0].Quantity);
        }

        [Test]
        public void TryAdd_UpToStackLimit_StaysInOneSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable(max: 10), 7);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(7, inventory[0].Quantity);
        }

        [Test]
        public void TryAdd_ExactlyStackLimit_FillsSlotToLimit()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable(max: 10), 10);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(10, inventory[0].Quantity);
            Assert.IsTrue(inventory[0].IsFull);
        }

        [Test]
        public void TryAdd_QuantityExceedingStackLimit_OverflowsIntoSecondSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable(max: 10), 15);

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [TestCase(10, 25, new[] { 10, 10, 5 })]
        [TestCase(10, 30, new[] { 10, 10, 10 })]
        [TestCase(3, 7, new[] { 3, 3, 1 })]
        public void TryAdd_OverflowSlots_HaveExpectedQuantities(int max, int quantity, int[] expectedQuantities)
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable(max: max), quantity);

            Assert.AreEqual(expectedQuantities.Length, inventory.SlotCount);
            for (var i = 0; i < expectedQuantities.Length; i++)
            {
                Assert.AreEqual(expectedQuantities[i], inventory[i].Quantity);
            }
        }

        [Test]
        public void TryAdd_DoesNotMutateTheCallerItem()
        {
            var inventory = new Inventory<TestItem>();
            var item = TestItems.Stackable(max: 10);
            var uniqueIdBefore = item.UniqueId;
            var instanceIdBefore = item.InstanceId;

            inventory.TryAdd(item, 25);

            Assert.AreEqual(uniqueIdBefore, item.UniqueId);
            Assert.AreEqual(instanceIdBefore, item.InstanceId);
        }

        [Test]
        public void TryAdd_NonStackableItem_OccupiesOneSlotWithQuantityOne()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable(), 1);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(1, inventory[0].Quantity);
        }

        [Test]
        public void TryAdd_NonStackableItem_WithQuantityGreaterThanOne_IsRejected()
        {
            var inventory = new Inventory<TestItem>();
            var result = inventory.TryAdd(TestItems.NonStackable(), 2);

            Assert.IsFalse(result);
            Assert.AreEqual(0, inventory.SlotCount);
        }

        [Test]
        public void TryAdd_TwoDistinctNonStackableInstancesOfSameKind_ProduceTwoSlots()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [Test]
        public void TryAdd_TwiceSameKind_TopsUpExistingPartialStackBeforeUsingNewSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(7, inventory[0].Quantity);
        }

        [Test]
        public void TryAdd_TopsUpMultiplePartialStacks_LowestIndexFirst()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual(8, inventory[0].Quantity);
            Assert.AreEqual(3, inventory[1].Quantity);
        }

        [Test]
        public void TryAdd_DifferentUniqueIds_UseSeparateSlots()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 5);

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [Test]
        public void TryAdd_DifferentInstanceSameStackableKind_MergesIntoExistingStack()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void TryAdd_MergedStack_KeepsTheFirstItemInstance()
        {
            var inventory = new Inventory<TestItem>();
            var first = TestItems.Stackable("disc", max: 10);
            inventory.TryAdd(first, 3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);

            Assert.AreSame(first, inventory[0].Item);
        }

        [Test]
        public void TryAdd_RespectsCanStackWithOverride_DifferentStateGetsOwnSlot()
        {
            var inventory = new Inventory<DurabilityTestItem>();
            var attributes = new FakeItemAttributes("disc", isStackable: true, maxStackSize: 10);

            inventory.TryAdd(new DurabilityTestItem(attributes, 40), 1);
            inventory.TryAdd(new DurabilityTestItem(attributes, 100), 1);

            Assert.AreEqual(2, inventory.SlotCount);
        }

        [Test]
        public void AddRange_DistinctNonStackableInstances_EachGetOwnSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.AddRange(TestItems.Many(3, "disc"), out _);

            Assert.AreEqual(3, inventory.SlotCount);
        }

        [Test]
        public void AddRange_ReturnsAddedCount_AndEmptyNotAdded()
        {
            var inventory = new Inventory<TestItem>();
            var added = inventory.AddRange(TestItems.Many(3, "disc"), out var notAdded);

            Assert.AreEqual(3, added);
            Assert.IsEmpty(notAdded);
        }
    }
}
