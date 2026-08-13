using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryRemoveTests
    {
        [Test]
        public void TryRemove_SingleUnit_DecrementsStack()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            inventory.TryRemove("disc", 1, out _);

            Assert.AreEqual(4, inventory[0].Quantity);
        }

        [Test]
        public void TryRemove_EntireStack_EmptiesSlot()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            inventory.TryRemove("disc", 5, out _);

            Assert.IsTrue(inventory[0].IsEmpty);
        }

        [Test]
        public void TryRemove_Fixed_EmptiedSlotKeepsItsIndex()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.TryRemove("a", 1, out _);

            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.AreEqual("b", inventory[1].Item.UniqueId);
        }

        [Test]
        public void TryRemove_Fixed_EmptiedSlotDoesNotChangeSlotCount()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            inventory.TryRemove("a", 1, out _);

            Assert.AreEqual(3, inventory.SlotCount);
        }

        [Test]
        public void TryRemove_Unbounded_RemovesSlot_AndShiftsLaterSlotsDown()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.TryRemove("a", 1, out _);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual("b", inventory[0].Item.UniqueId);
        }

        [Test]
        public void TryRemove_SpanningTwoStacks_DrainsBothAndSucceeds()
        {
            var inventory = new Inventory<TestItem>(2, allowDuplicates: true);
            inventory.TryAdd(TestItems.Stackable("disc", max: 5), 5);
            inventory.SplitStack(0, 2, 1);

            var result = inventory.TryRemove("disc", 5, out _);

            Assert.IsTrue(result);
            Assert.AreEqual(0, inventory.GetQuantity("disc"));
        }

        [Test]
        public void TryRemove_DrainsInAscendingSlotOrder()
        {
            var inventory = new Inventory<TestItem>(2, allowDuplicates: true);
            inventory.TryAdd(TestItems.Stackable("disc", max: 5), 5);
            inventory.SplitStack(0, 2, 1);

            inventory.TryRemove("disc", 2, out _);

            Assert.AreEqual(1, inventory[0].Quantity);
            Assert.AreEqual(2, inventory[1].Quantity);
        }

        [Test]
        public void TryRemove_ByUniqueId_Works()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsTrue(inventory.TryRemove("disc", 5, out _));
        }

        [Test]
        public void TryRemove_ByUnknownUniqueId_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryRemove("nonexistent", 1, out _));
        }

        [Test]
        public void TryRemove_ItemNotPresent_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryRemove(TestItems.NonStackable("disc"), 1));
        }

        [Test]
        public void TryRemove_MatchesByUniqueId_NotByReference()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsTrue(inventory.TryRemove(TestItems.NonStackable("disc"), 1));
        }

        [Test]
        public void TryRemoveInstance_RemovesOnlyThatReference()
        {
            var inventory = new Inventory<TestItem>();
            var a = TestItems.NonStackable("disc");
            var b = TestItems.NonStackable("disc");
            inventory.TryAdd(a, 1);
            inventory.TryAdd(b, 1);

            inventory.TryRemoveInstance(a);

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreSame(b, inventory[0].Item);
        }

        [Test]
        public void TryRemoveInstance_LeavesOtherInstancesOfSameKind()
        {
            var inventory = new Inventory<TestItem>();
            var a = TestItems.NonStackable("disc");
            var b = TestItems.NonStackable("disc");
            inventory.TryAdd(a, 1);
            inventory.TryAdd(b, 1);

            inventory.TryRemoveInstance(a);

            Assert.IsTrue(inventory.Contains(b));
        }

        [Test]
        public void TryRemoveInstance_NotPresent_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryRemoveInstance(TestItems.NonStackable("disc")));
        }

        [TestCase(0, false)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        public void CanRemove_AgreesWithTryRemove(int quantity, bool expected)
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual(expected, inventory.CanRemove(TestItems.NonStackable("disc"), quantity));
        }

        [Test]
        public void Clear_Fixed_EmptiesAllSlots_ButKeepsSlotCount()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            inventory.Clear();

            Assert.AreEqual(3, inventory.SlotCount);
            Assert.IsTrue(inventory.IsEmpty);
        }

        [Test]
        public void Clear_Unbounded_DropsToZeroSlots()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            inventory.Clear();

            Assert.AreEqual(0, inventory.SlotCount);
        }

        [Test]
        public void Clear_ResetsTotalQuantityToZero()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 7);

            inventory.Clear();

            Assert.AreEqual(0, inventory.TotalQuantity);
        }

        [Test]
        public void Clear_AlreadyEmpty_FiresNoEvents()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Clear();

            Assert.AreEqual(0, recorder.UpdatedCount);
        }
    }
}
