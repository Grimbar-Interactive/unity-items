using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryRemoveAtTests
    {
        [Test]
        public void TryRemoveAt_WholeStack_OutputsItemAndQuantity()
        {
            var inventory = new Inventory<TestItem>(1);
            var item = TestItems.Stackable("disc", max: 10);
            inventory.TryAdd(item, 4);

            var result = inventory.TryRemoveAt(0, out var removedItem, out var removedQuantity);

            Assert.IsTrue(result);
            Assert.AreSame(item, removedItem);
            Assert.AreEqual(4, removedQuantity);
        }

        [Test]
        public void TryRemoveAt_WholeStack_Fixed_EmptiesSlotInPlace()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            inventory.TryRemoveAt(0, out _, out _);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.IsTrue(inventory[0].IsEmpty);
        }

        [Test]
        public void TryRemoveAt_WholeStack_Unbounded_RemovesSlot()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            inventory.TryRemoveAt(0, out _, out _);

            Assert.AreEqual(0, inventory.SlotCount);
        }

        [Test]
        public void TryRemoveAt_EmptySlot_ReturnsFalse_WithDefaultOutputs()
        {
            var inventory = new Inventory<TestItem>(1);

            var result = inventory.TryRemoveAt(0, out var item, out var quantity);

            Assert.IsFalse(result);
            Assert.IsNull(item);
            Assert.AreEqual(0, quantity);
        }

        [Test]
        public void TryRemoveAt_WithQuantity_DecrementsStack()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            inventory.TryRemoveAt(0, 2);

            Assert.AreEqual(3, inventory[0].Quantity);
        }

        [Test]
        public void TryRemoveAt_WithQuantityEqualToStack_EmptiesSlot()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            inventory.TryRemoveAt(0, 5);

            Assert.IsTrue(inventory[0].IsEmpty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryRemoveAt_NonPositiveQuantity_ReturnsFalse(int quantity)
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsFalse(inventory.TryRemoveAt(0, quantity));
        }

        [Test]
        public void TryRemoveAt_WithQuantity_LeavesOtherSlotsUntouched()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 3);

            inventory.TryRemoveAt(0, 2);

            Assert.AreEqual(3, inventory[1].Quantity);
        }
    }
}
