using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryConstructionTests
    {
        [Test]
        public void Default_IsUnbounded_AndAllowsDuplicates()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsNull(inventory.MaxSlotCount);
            Assert.IsTrue(inventory.AllowDuplicates);
        }

        [Test]
        public void Unbounded_MaxSlotCountIsNull_AndSlotCountIsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsNull(inventory.MaxSlotCount);
            Assert.AreEqual(0, inventory.SlotCount);
        }

        [Test]
        public void Unbounded_FreeSlotCountIsNull()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsNull(inventory.FreeSlotCount);
        }

        [Test]
        public void Unbounded_IsFull_IsAlwaysFalse()
        {
            var inventory = new Inventory<TestItem>();
            inventory.AddRange(TestItems.Many(50), out _);
            Assert.IsFalse(inventory.IsFull);
        }

        [Test]
        public void Fixed_PreallocatesExactlyMaxSlotCountEmptySlots()
        {
            var inventory = new Inventory<TestItem>(5);
            Assert.AreEqual(5, inventory.SlotCount);
            Assert.AreEqual(0, inventory.FilledSlotCount);
        }

        [Test]
        public void Fixed_FreeSlotCountEqualsMaxSlotCount()
        {
            var inventory = new Inventory<TestItem>(5);
            Assert.AreEqual(5, inventory.FreeSlotCount);
        }

        [Test]
        public void Fixed_IsFixedSizeIsTrue_UnboundedIsFalse()
        {
            Assert.IsTrue(new Inventory<TestItem>(5).IsFixedSize);
            Assert.IsFalse(new Inventory<TestItem>().IsFixedSize);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveMaxSlotCount_Throws(int maxSlotCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory<TestItem>(maxSlotCount));
        }

        [Test]
        public void Constructor_AllowDuplicatesFalse_IsExposed()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            Assert.IsFalse(inventory.AllowDuplicates);
        }

        [Test]
        public void New_IsEmpty_AndTotalQuantityIsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsTrue(inventory.IsEmpty);
            Assert.AreEqual(0, inventory.TotalQuantity);
        }
    }
}
