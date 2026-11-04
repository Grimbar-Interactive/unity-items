using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryConstructionTests
    {
        [Test]
        public void NullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LinkedInventory<TestItem>(null));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveSlotCount_Throws(int slots)
        {
            var source = new Inventory<TestItem>();
            Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedInventory<TestItem>(source, slots));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NonPositiveMaxStackSize_Throws(int size)
        {
            var source = new Inventory<TestItem>();
            Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedInventory<TestItem>(source, maxStackSize: size));
        }

        [Test]
        public void NullResolver_Throws()
        {
            var source = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() =>
                new LinkedInventory<TestItem>(source, null, true, (Func<TestItem, int>)null));
        }

        [Test]
        public void Unbounded_StartsWithNoSlots()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());

            Assert.IsFalse(linked.IsFixedSize);
            Assert.IsNull(linked.MaxSlotCount);
            Assert.IsNull(linked.FreeSlotCount);
            Assert.AreEqual(0, linked.SlotCount);
            Assert.IsTrue(linked.IsEmpty);
            Assert.IsFalse(linked.IsFull);
        }

        [Test]
        public void Fixed_CreatesEmptySlotsUpFront()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>(), 3);

            Assert.IsTrue(linked.IsFixedSize);
            Assert.AreEqual(3, linked.MaxSlotCount);
            Assert.AreEqual(3, linked.SlotCount);
            Assert.AreEqual(3, linked.FreeSlotCount);
            Assert.AreEqual(0, linked.FilledSlotCount);
            Assert.IsTrue(linked.Slots[0].IsEmpty);
            Assert.IsFalse(linked.IsFull);
        }

        [Test]
        public void ExposesSourceAndSettings()
        {
            var source = new Inventory<TestItem>();
            var linked = new LinkedInventory<TestItem>(source, 4, allowDuplicates: false, maxStackSize: 7);

            Assert.AreSame(source, linked.Source);
            Assert.IsFalse(linked.AllowDuplicates);
            Assert.AreEqual(7, linked.MaxStackSizeOverride);
        }

        [Test]
        public void GetMaxStackSize_WithoutOverride_IsTheItemsOwnLimit()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.AreEqual(10, linked.GetMaxStackSize(TestItems.Stackable("potion", 10)));
        }

        [TestCase(20)]
        [TestCase(3)]
        public void GetMaxStackSize_OverrideReplacesTheItemsLimit_UpOrDown(int overrideSize)
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>(), maxStackSize: overrideSize);
            Assert.AreEqual(overrideSize, linked.GetMaxStackSize(TestItems.Stackable("potion", 10)));
        }

        [Test]
        public void GetMaxStackSize_NonStackable_IsAlwaysOne()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>(), maxStackSize: 9);
            Assert.AreEqual(1, linked.GetMaxStackSize(TestItems.NonStackable("sword")));
        }

        [Test]
        public void GetMaxStackSize_NullItem_IsZero()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.AreEqual(0, linked.GetMaxStackSize(null));
        }

        [Test]
        public void GetMaxStackSize_Resolver_IsUsedPerItem_AndClampedToOne()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>(), null, true,
                item => item.UniqueId == "potion" ? 4 : 0);

            Assert.AreEqual(4, linked.GetMaxStackSize(TestItems.Stackable("potion", 10)));
            Assert.AreEqual(1, linked.GetMaxStackSize(TestItems.Stackable("ether", 10)));
        }
    }
}
