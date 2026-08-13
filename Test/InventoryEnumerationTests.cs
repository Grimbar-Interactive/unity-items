using System.Collections.Generic;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryEnumerationTests
    {
        [Test]
        public void Items_ReturnsOccupiedItemsOnly()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            var items = new List<TestItem>(inventory.Items);

            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void Items_SkipsEmptySlots_InFixedInventory()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.Swap(0, 2);

            var items = new List<TestItem>(inventory.Items);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("a", items[0].UniqueId);
        }

        [Test]
        public void Items_IsInSlotOrder()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            var items = new List<TestItem>(inventory.Items);

            Assert.AreEqual("a", items[0].UniqueId);
            Assert.AreEqual("b", items[1].UniqueId);
        }

        [Test]
        public void Items_OnEmptyInventory_IsEmptySequence()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsEmpty(new List<TestItem>(inventory.Items));
        }

        [Test]
        public void Foreach_EnumeratesSameSequenceAsItems()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            var viaForeach = new List<TestItem>();
            foreach (var item in inventory) viaForeach.Add(item);

            CollectionAssert.AreEqual(new List<TestItem>(inventory.Items), viaForeach);
        }

        [Test]
        public void Slots_ContainsEmptyStacks_InFixedMode()
        {
            var inventory = new Inventory<TestItem>(3);
            var emptyCount = 0;
            foreach (var slot in inventory.Slots)
            {
                if (slot.IsEmpty) emptyCount++;
            }

            Assert.AreEqual(3, emptyCount);
        }

        [Test]
        public void Slots_ContainsNoEmptyStacks_InUnboundedMode()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            foreach (var slot in inventory.Slots) Assert.IsFalse(slot.IsEmpty);
        }

        [Test]
        public void Slots_CountEqualsMaxSlotCount_InFixedMode()
        {
            var inventory = new Inventory<TestItem>(5);
            Assert.AreEqual(5, inventory.Slots.Count);
        }
    }
}
