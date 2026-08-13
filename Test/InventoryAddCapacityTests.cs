using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAddCapacityTests
    {
        [Test]
        public void Unbounded_TryAdd_NeverFailsForCapacity()
        {
            var inventory = new Inventory<TestItem>();
            var result = inventory.AddRange(TestItems.Many(500, "disc"), out var notAdded);

            Assert.AreEqual(500, result);
            Assert.IsEmpty(notAdded);
        }

        [Test]
        public void Unbounded_GetFreeSpaceForStackable_IsIntMaxValue()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(int.MaxValue, inventory.GetFreeSpaceFor(TestItems.Stackable()));
        }

        [Test]
        public void Fixed_TryAdd_QuantityExceedingTotalCapacity_Fails()
        {
            var inventory = new Inventory<TestItem>(2);
            var result = inventory.TryAdd(TestItems.Stackable(max: 10), 21);

            Assert.IsFalse(result);
        }

        [Test]
        public void Fixed_TryAdd_NonStackableItem_SucceedsWhileSlotsRemain()
        {
            var inventory = new Inventory<TestItem>(2);
            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("a"), 1));
            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("b"), 1));
        }

        [TestCase(4)]
        public void Fixed_TryAdd_NonStackableItems_FillEverySlotThenFail(int slotCount)
        {
            var inventory = new Inventory<TestItem>(slotCount);
            for (var i = 0; i < slotCount; i++)
            {
                Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable($"item{i}"), 1));
            }

            Assert.IsFalse(inventory.TryAdd(TestItems.NonStackable("overflow"), 1));
        }

        [Test]
        public void Fixed_TryAdd_StackableItem_FillsPartialStacksThenEmptySlots_UpToCapacity()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsTrue(inventory.TryAdd(TestItems.Stackable("disc", max: 10), 15));
            Assert.AreEqual(10, inventory[0].Quantity);
            Assert.AreEqual(10, inventory[1].Quantity);
        }

        [Test]
        public void Fixed_GetFreeSpaceFor_CountsPartialStacksAndEmptySlots()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            Assert.AreEqual(26, inventory.GetFreeSpaceFor(TestItems.Stackable("disc", max: 10)));
        }

        [Test]
        public void Fixed_GetFreeSpaceFor_FullInventory_IsZero()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            Assert.AreEqual(0, inventory.GetFreeSpaceFor(TestItems.NonStackable("b")));
        }

        [Test]
        public void Fixed_GetFreeSpaceFor_FullSlotsButPartialStack_IsRemainingStackSpace()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            Assert.AreEqual(6, inventory.GetFreeSpaceFor(TestItems.Stackable("disc", max: 10)));
        }

        [Test]
        public void Fixed_GetFreeSpaceFor_DoesNotOverflow_WithLargeSlotCountAndStackLimit()
        {
            var inventory = new Inventory<TestItem>(100000);
            var space = inventory.GetFreeSpaceFor(TestItems.Stackable(max: 100000));

            Assert.AreEqual(int.MaxValue, space);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(11)]
        public void CanAdd_AgreesWithGetFreeSpaceFor(int quantity)
        {
            var inventory = new Inventory<TestItem>(1);
            var item = TestItems.Stackable(max: 10);

            var expected = quantity > 0 && quantity <= inventory.GetFreeSpaceFor(item);
            Assert.AreEqual(expected, inventory.CanAdd(item, quantity));
        }

        [Test]
        public void TryAdd_SucceedsExactlyWhenQuantityIsAtMostFreeSpace()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
            var item = TestItems.Stackable("disc", max: 10);
            var freeSpace = inventory.GetFreeSpaceFor(item);

            Assert.IsTrue(inventory.TryAdd(item, freeSpace));
        }

        [Test]
        public void Fixed_IsFull_TrueWhenEverySlotOccupied_EvenWithRoomInStacks()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 1);

            Assert.IsTrue(inventory.IsFull);
        }

        [Test]
        public void CanAcceptItem_Override_RejectsFilteredItem()
        {
            var inventory = new FilteredInventory();
            Assert.IsFalse(inventory.TryAdd(TestItems.NonStackable("blocked"), 1));
        }

        [Test]
        public void CanAcceptItem_Override_GetFreeSpaceForIsZero()
        {
            var inventory = new FilteredInventory();
            Assert.AreEqual(0, inventory.GetFreeSpaceFor(TestItems.NonStackable("blocked")));
        }

        [Test]
        public void CanAcceptItem_Override_AllowsPermittedItem()
        {
            var inventory = new FilteredInventory();
            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("allowed"), 1));
        }
    }
}
