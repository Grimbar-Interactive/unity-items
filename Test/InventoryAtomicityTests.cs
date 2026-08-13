using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAtomicityTests
    {
        [Test]
        public void TryAdd_WhenQuantityDoesNotFit_MutatesNothing()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 100);

            Assert.AreEqual(4, inventory[0].Quantity);
            Assert.AreEqual(1, inventory.SlotCount);
        }

        [Test]
        public void TryAdd_WhenQuantityDoesNotFit_RemainderEqualsRequestedQuantity()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 100, out var remainder);

            Assert.AreEqual(100, remainder);
        }

        [Test]
        public void TryAdd_WhenQuantityDoesNotFit_FiresNoEvents()
        {
            var inventory = new Inventory<TestItem>(1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 100);

            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(0, recorder.SlotChangedCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void TryAdd_PartialFitAcrossPartialStackAndNewSlot_LeavesPartialStackUntouched()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 100);

            Assert.AreEqual(4, inventory[0].Quantity);
            Assert.IsTrue(inventory[1].IsEmpty);
        }

        [Test]
        public void TryRemove_WhenQuantityExceedsHeldTotal_MutatesNothing()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            inventory.TryRemove("disc", 100, out _);

            Assert.AreEqual(4, inventory[0].Quantity);
        }

        [Test]
        public void TryRemove_WhenQuantityExceedsHeldTotal_ReturnsFalseWithFullRemainder()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            var result = inventory.TryRemove("disc", 100, out var remainder);

            Assert.IsFalse(result);
            Assert.AreEqual(100, remainder);
        }

        [Test]
        public void TryRemove_WhenQuantityExceedsHeldTotal_FiresNoEvents()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemove("disc", 100, out _);

            Assert.AreEqual(0, recorder.RemovedCount);
            Assert.AreEqual(0, recorder.SlotChangedCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void TryRemove_SpanningThreeStacksThatCannotBeSatisfied_LeavesAllThreeIntact()
        {
            var inventory = new Inventory<TestItem>(3, allowDuplicates: true);
            inventory.TryAdd(TestItems.Stackable("disc", max: 5), 5);
            inventory.SplitStack(0, 2, 1);
            inventory.SplitStack(0, 1, 2);

            var result = inventory.TryRemove("disc", 100, out _);

            Assert.IsFalse(result);
            Assert.AreEqual(2, inventory[0].Quantity);
            Assert.AreEqual(2, inventory[1].Quantity);
            Assert.AreEqual(1, inventory[2].Quantity);
        }

        [Test]
        public void TryRemoveAt_QuantityExceedsSlotQuantity_MutatesNothing()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            var result = inventory.TryRemoveAt(0, 100);

            Assert.IsFalse(result);
            Assert.AreEqual(4, inventory[0].Quantity);
        }

        [Test]
        public void AddUpTo_PartialFit_AddsWhatFits_AndReturnsAddedCount()
        {
            var inventory = new Inventory<TestItem>(1);
            var added = inventory.AddUpTo(TestItems.Stackable("disc", max: 10), 15);

            Assert.AreEqual(10, added);
            Assert.AreEqual(10, inventory[0].Quantity);
        }

        [Test]
        public void AddUpTo_NoSpace_ReturnsZero_AndFiresNoEvents()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            var added = inventory.AddUpTo(TestItems.NonStackable("patch"), 1);

            Assert.AreEqual(0, added);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void AddUpTo_NonStackableItem_AddsAtMostOne()
        {
            var inventory = new Inventory<TestItem>();
            var added = inventory.AddUpTo(TestItems.NonStackable("disc"), 5);

            Assert.AreEqual(1, added);
        }

        [Test]
        public void AddUpTo_FullQuantityFits_BehavesLikeTryAdd()
        {
            var inventory = new Inventory<TestItem>();
            var added = inventory.AddUpTo(TestItems.Stackable("disc", max: 10), 6);

            Assert.AreEqual(6, added);
            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void RemoveUpTo_MoreThanHeld_RemovesAllHeld_AndReturnsRemovedCount()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            var removed = inventory.RemoveUpTo("disc", 100);

            Assert.AreEqual(4, removed);
            Assert.IsTrue(inventory.IsEmpty);
        }

        [Test]
        public void RemoveUpTo_NothingHeld_ReturnsZero_AndFiresNoEvents()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            var removed = inventory.RemoveUpTo("disc", 5);

            Assert.AreEqual(0, removed);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void AddRange_PartialFit_AddsWhatFits_AndReportsNotAdded()
        {
            var inventory = new Inventory<TestItem>(2);
            var added = inventory.AddRange(TestItems.Many(3, "disc"), out var notAdded);

            Assert.AreEqual(2, added);
            Assert.AreEqual(1, notAdded.Count);
        }
    }
}
