using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryGuardTests
    {
        [Test]
        public void TryAdd_NullItem_ReturnsFalse_AndFiresNothing()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            Assert.IsFalse(inventory.TryAdd(null, 1));
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryAdd_NonPositiveQuantity_ReturnsFalse(int quantity)
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryAdd(TestItems.NonStackable("disc"), quantity));
        }

        [Test]
        public void TryRemove_NullItem_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryRemove((TestItem)null, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryRemove_NonPositiveQuantity_ReturnsFalse(int quantity)
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            Assert.IsFalse(inventory.TryRemove("disc", quantity, out _));
        }

        [Test]
        public void TryRemove_NullUniqueId_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryRemove((string)null, 1, out _));
        }

        [Test]
        public void AddUpTo_NullItem_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.AddUpTo(null, 1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void AddUpTo_NonPositiveQuantity_ReturnsZero(int quantity)
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.AddUpTo(TestItems.NonStackable("disc"), quantity));
        }

        [Test]
        public void RemoveUpTo_NullItem_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.RemoveUpTo((TestItem)null, 1));
        }

        [Test]
        public void GetFreeSpaceFor_NullItem_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.GetFreeSpaceFor(null));
        }

        [Test]
        public void CanAdd_NullItem_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.CanAdd(null, 1));
        }

        [Test]
        public void GetQuantity_NullItem_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.GetQuantity((TestItem)null));
        }

        [Test]
        public void GetQuantity_NullUniqueId_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.GetQuantity((string)null));
        }

        [Test]
        public void Contains_NullItem_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.ContainsInstance(null));
        }

        [Test]
        public void Contains_NullUniqueId_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.Contains((string)null));
        }

        [Test]
        public void Contains_NullPredicate_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.Contains((Func<TestItem, bool>)null));
        }

        [Test]
        public void FindAll_NullPredicate_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.FindAll(null));
        }

        [Test]
        public void TryFind_NullPredicate_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.TryFind(null, out _));
        }

        [Test]
        public void GetQuantity_NullPredicate_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.GetQuantity((Func<TestItem, bool>)null));
        }

        [Test]
        public void TryGetSlotIndex_NullPredicate_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.TryGetSlotIndex(null, out _));
        }

        [Test]
        public void AddRange_NullSequence_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentNullException>(() => inventory.AddRange(null, out _));
        }

        [Test]
        public void AddRange_SequenceContainingNulls_SkipsThem_AndReportsThem()
        {
            var inventory = new Inventory<TestItem>();
            var items = new List<TestItem> { TestItems.NonStackable("a"), null, TestItems.NonStackable("b") };

            var added = inventory.AddRange(items, out var notAdded);

            Assert.AreEqual(2, added);
            Assert.AreEqual(1, notAdded.Count);
            Assert.IsNull(notAdded[0]);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void Indexer_InvalidIndex_Throws(int index)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = inventory[index]);
        }

        [TestCase(-1, 0)]
        [TestCase(0, 3)]
        public void Swap_InvalidIndex_Throws(int a, int b)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Swap(a, b));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 3)]
        public void Move_InvalidIndex_Throws(int a, int b)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Move(a, b));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void SplitStack_InvalidIndex_Throws(int index)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SplitStack(index, 1));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 3)]
        public void MergeStacks_InvalidIndex_Throws(int a, int b)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.MergeStacks(a, b));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TryRemoveAt_InvalidIndex_Throws(int index)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryRemoveAt(index, out _, out _));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TryRemoveAtWithQuantity_InvalidIndex_Throws(int index)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryRemoveAt(index, 1));
        }

        [Test]
        public void Unbounded_Indexer_OnEmptyInventory_Throws()
        {
            var inventory = new Inventory<TestItem>();
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = inventory[0]);
        }

        [Test]
        public void Unbounded_IndicesShiftAfterRemoval_SoStaleIndexThrows()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryRemoveAt(0, out _, out _);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = inventory[0]);
        }
    }
}
