using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemStackTests
    {
        [Test]
        public void NewFixedInventory_AllSlotsAreEmpty()
        {
            var inventory = new Inventory<TestItem>(3);
            foreach (var slot in inventory.Slots) Assert.IsTrue(slot.IsEmpty);
        }

        [Test]
        public void EmptySlot_ItemIsNull_AndQuantityIsZero()
        {
            var inventory = new Inventory<TestItem>(1);
            Assert.IsNull(inventory[0].Item);
            Assert.AreEqual(0, inventory[0].Quantity);
        }

        [Test]
        public void EmptySlot_RemainingSpaceIsZero_AndIsFullIsFalse()
        {
            var inventory = new Inventory<TestItem>(1);
            Assert.AreEqual(0, inventory[0].RemainingSpace);
            Assert.IsFalse(inventory[0].IsFull);
        }

        [Test]
        public void OccupiedSlot_ItemAndQuantityReflectTheAdd()
        {
            var inventory = new Inventory<TestItem>(1);
            var item = TestItems.Stackable(max: 10);
            inventory.TryAdd(item, 4);

            Assert.AreSame(item, inventory[0].Item);
            Assert.AreEqual(4, inventory[0].Quantity);
        }

        [Test]
        public void OccupiedSlot_RemainingSpace_IsStackLimitMinusQuantity()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable(max: 10), 4);

            Assert.AreEqual(6, inventory[0].RemainingSpace);
        }

        [Test]
        public void FullSlot_IsFull_IsTrue()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable(max: 10), 10);

            Assert.IsTrue(inventory[0].IsFull);
        }

        [Test]
        public void Slot_ObjectIdentityIsStableAcrossMutations()
        {
            var inventory = new Inventory<TestItem>(1);
            var slot = inventory[0];

            inventory.TryAdd(TestItems.Stackable(max: 10), 4);
            Assert.AreSame(slot, inventory[0]);

            inventory.TryRemoveAt(0, 4);
            Assert.AreSame(slot, inventory[0]);
        }

        [Test]
        public void Slots_IsALiveView_ReflectsLaterMutations()
        {
            var inventory = new Inventory<TestItem>(1);
            var slots = inventory.Slots;

            inventory.TryAdd(TestItems.Stackable(max: 10), 4);

            Assert.AreEqual(4, slots[0].Quantity);
        }

        [Test]
        public void ToString_EmptySlot()
        {
            var inventory = new Inventory<TestItem>(1);
            Assert.AreEqual("<empty>", inventory[0].ToString());
        }

        [Test]
        public void ToString_OccupiedSlot()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc_destroyer", max: 10), 3);

            Assert.AreEqual("disc_destroyer x3", inventory[0].ToString());
        }
    }
}
