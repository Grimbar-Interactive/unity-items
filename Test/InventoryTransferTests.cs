using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryTransferTests
    {
        // ---- success paths ----

        [Test]
        public void TryTransferTo_WholeStack_MovesEverythingAndEmptiesSource()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 6);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.IsTrue(source[0].IsEmpty);
            Assert.AreEqual(6, target.GetQuantity("potion"));
        }

        [Test]
        public void TryTransferTo_PartialQuantity_LeavesRemainderInSource()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 6);

            Assert.IsTrue(source.TryTransferTo(target, 0, 4));

            Assert.AreEqual(2, source[0].Quantity);
            Assert.AreEqual(4, target.GetQuantity("potion"));
        }

        [Test]
        public void TryTransferTo_StacksOntoMatchingPartialStackInTarget()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 4);
            target.TryAdd(TestItems.Stackable("potion", max: 10), 3);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.AreEqual(1, target.FilledSlotCount);
            Assert.AreEqual(7, target[0].Quantity);
        }

        [Test]
        public void TryTransferTo_SpillsAcrossTargetSlotsWhenNeeded()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 10);
            target.TryAdd(TestItems.Stackable("potion", max: 10), 8);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.AreEqual(10, target[0].Quantity);
            Assert.AreEqual(8, target[1].Quantity);
        }

        [Test]
        public void TryTransferTo_NonStackable_MovesTheInstance()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            var sword = TestItems.NonStackable("sword");
            source.TryAdd(sword);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.IsFalse(source.Contains(sword));
            Assert.IsTrue(target.Contains(sword));
        }

        [Test]
        public void TryTransferTo_UnboundedSource_RemovesTheEmptiedSlot()
        {
            var source = new Inventory<TestItem>();
            var target = new Inventory<TestItem>(3);
            var a = TestItems.NonStackable("a");
            var b = TestItems.NonStackable("b");
            source.TryAdd(a);
            source.TryAdd(b);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.AreEqual(1, source.SlotCount);
            Assert.AreSame(b, source[0].Item);
        }

        [Test]
        public void TryTransferTo_UnboundedTarget_GrowsToFit()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>();
            source.TryAdd(TestItems.Stackable("potion", max: 10), 10);

            Assert.IsTrue(source.TryTransferTo(target, 0));

            Assert.AreEqual(1, target.SlotCount);
            Assert.AreEqual(10, target[0].Quantity);
        }

        // ---- atomic failure ----

        [Test]
        public void TryTransferTo_TargetFull_FailsAndChangesNeitherInventory()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(1);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 5);
            target.TryAdd(TestItems.NonStackable("blocker"));
            using var sourceEvents = new InventoryEventRecorder<TestItem>(source);
            using var targetEvents = new InventoryEventRecorder<TestItem>(target);

            Assert.IsFalse(source.TryTransferTo(target, 0));

            Assert.AreEqual(5, source[0].Quantity);
            Assert.AreEqual(1, target.FilledSlotCount);
            Assert.AreEqual(0, sourceEvents.UpdatedCount);
            Assert.AreEqual(0, targetEvents.UpdatedCount);
        }

        [Test]
        public void TryTransferTo_TargetCannotHoldFullQuantity_MovesNothing()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(1);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 10);
            target.TryAdd(TestItems.Stackable("potion", max: 10), 4);

            Assert.IsFalse(source.TryTransferTo(target, 0));

            Assert.AreEqual(10, source[0].Quantity);
            Assert.AreEqual(4, target[0].Quantity);
        }

        [Test]
        public void TryTransferTo_TargetRejectsItem_Fails()
        {
            var source = new Inventory<TestItem>(3);
            var target = new FilteredInventory(3);
            source.TryAdd(TestItems.NonStackable("blocked"));

            Assert.IsFalse(source.TryTransferTo(target, 0));

            Assert.AreEqual(1, source.FilledSlotCount);
            Assert.IsTrue(target.IsEmpty);
        }

        [Test]
        public void TryTransferTo_TargetAcceptsFilteredItem_Succeeds()
        {
            var source = new Inventory<TestItem>(3);
            var target = new FilteredInventory(3);
            source.TryAdd(TestItems.NonStackable("allowed"));

            Assert.IsTrue(source.TryTransferTo(target, 0));
        }

        [Test]
        public void TryTransferTo_TargetDisallowsDuplicates_AndHoldsTheKind_Fails()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3, allowDuplicates: false);
            source.TryAdd(TestItems.NonStackable("sword"));
            target.TryAdd(TestItems.NonStackable("sword"));

            Assert.IsFalse(source.TryTransferTo(target, 0));

            Assert.AreEqual(1, source.FilledSlotCount);
            Assert.AreEqual(1, target.FilledSlotCount);
        }

        [Test]
        public void TryTransferTo_TargetAlreadyHoldsTheInstance_Fails()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            var sword = TestItems.NonStackable("sword");
            source.TryAdd(sword);
            target.TryAdd(sword);

            Assert.IsFalse(source.TryTransferTo(target, 0));
            Assert.AreEqual(1, source.FilledSlotCount);
        }

        [Test]
        public void TryTransferTo_EmptySlot_ReturnsFalse()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);

            Assert.IsFalse(source.TryTransferTo(target, 1));
        }

        [Test]
        public void TryTransferTo_MoreThanHeld_ReturnsFalse()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 3);

            Assert.IsFalse(source.TryTransferTo(target, 0, 4));
            Assert.AreEqual(3, source[0].Quantity);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryTransferTo_NonPositiveQuantity_ReturnsFalse(int quantity)
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 3);

            Assert.IsFalse(source.TryTransferTo(target, 0, quantity));
        }

        // ---- events ----

        [Test]
        public void TryTransferTo_RaisesAddedOnTargetAndRemovedOnSource_EachUpdatedOnce()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            var potion = TestItems.Stackable("potion", max: 10);
            source.TryAdd(potion, 6);
            using var sourceEvents = new InventoryEventRecorder<TestItem>(source);
            using var targetEvents = new InventoryEventRecorder<TestItem>(target);

            source.TryTransferTo(target, 0, 4);

            Assert.AreEqual(0, sourceEvents.AddedCount);
            Assert.AreEqual(1, sourceEvents.RemovedCount);
            Assert.AreEqual(4, sourceEvents.Removed[0].Quantity);
            CollectionAssert.AreEqual(new[] { 0 }, sourceEvents.ChangedSlots);
            Assert.AreEqual(1, sourceEvents.UpdatedCount);

            Assert.AreEqual(1, targetEvents.AddedCount);
            Assert.AreEqual(4, targetEvents.Added[0].Quantity);
            Assert.AreEqual(0, targetEvents.RemovedCount);
            CollectionAssert.AreEqual(new[] { 0 }, targetEvents.ChangedSlots);
            Assert.AreEqual(1, targetEvents.UpdatedCount);
        }

        [Test]
        public void TryTransferTo_WithinOuterBatchOnSource_DefersSourceEvents()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.NonStackable("a"));
            source.TryAdd(TestItems.NonStackable("b"));
            using var sourceEvents = new InventoryEventRecorder<TestItem>(source);

            using (source.BeginBatchUpdate())
            {
                source.TryTransferTo(target, 0);
                source.TryTransferTo(target, 1);
                Assert.AreEqual(0, sourceEvents.UpdatedCount);
            }

            Assert.AreEqual(1, sourceEvents.UpdatedCount);
            Assert.AreEqual(2, sourceEvents.RemovedCount);
        }

        [Test]
        public void TryTransferTo_TargetHandlerSeesSourceAlreadyUpdated()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 6);
            var sourceQuantitySeen = -1;
            target.OnItemAdded += (_, _) => sourceQuantitySeen = source.GetQuantity("potion");

            source.TryTransferTo(target, 0, 4);

            Assert.AreEqual(2, sourceQuantitySeen);
        }

        [Test]
        public void TryTransferTo_TargetHandlerThatClearsUnboundedSource_DoesNotThrowOrCorrupt()
        {
            var source = new Inventory<TestItem>();
            var target = new Inventory<TestItem>(3);
            var a = TestItems.NonStackable("a");
            source.TryAdd(a);
            source.TryAdd(TestItems.NonStackable("b"));
            target.OnItemAdded += (_, _) => source.Clear();

            Assert.DoesNotThrow(() => source.TryTransferTo(target, 0));

            Assert.IsTrue(source.IsEmpty);
            Assert.IsTrue(target.Contains(a));
        }

        // ---- TransferUpTo ----

        [Test]
        public void TransferUpTo_MovesWhatFitsAndReportsIt()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(1);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 10);
            target.TryAdd(TestItems.Stackable("potion", max: 10), 7);

            var moved = source.TransferUpTo(target, 0, 10);

            Assert.AreEqual(3, moved);
            Assert.AreEqual(7, source[0].Quantity);
            Assert.AreEqual(10, target[0].Quantity);
        }

        [Test]
        public void TransferUpTo_CapsAtRequestedQuantity()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 8);

            Assert.AreEqual(2, source.TransferUpTo(target, 0, 2));
            Assert.AreEqual(6, source[0].Quantity);
        }

        [Test]
        public void TransferUpTo_QuantityAboveHeld_MovesWhatIsHeld()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 5);

            Assert.AreEqual(5, source.TransferUpTo(target, 0, 99));
            Assert.IsTrue(source[0].IsEmpty);
        }

        [Test]
        public void TransferUpTo_NothingFits_ReturnsZeroAndFiresNothing()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(1);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 5);
            target.TryAdd(TestItems.NonStackable("blocker"));
            using var sourceEvents = new InventoryEventRecorder<TestItem>(source);

            Assert.AreEqual(0, source.TransferUpTo(target, 0, 5));
            Assert.AreEqual(0, sourceEvents.UpdatedCount);
        }

        [Test]
        public void TransferUpTo_EmptySlotOrNonPositiveQuantity_ReturnsZero()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.Stackable("potion", max: 10), 5);

            Assert.AreEqual(0, source.TransferUpTo(target, 2, 1));
            Assert.AreEqual(0, source.TransferUpTo(target, 0, 0));
        }

        // ---- guards ----

        [Test]
        public void Transfer_NullTarget_Throws()
        {
            var source = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.NonStackable("a"));

            Assert.Throws<ArgumentNullException>(() => source.TryTransferTo(null, 0));
            Assert.Throws<ArgumentNullException>(() => source.TryTransferTo(null, 0, 1));
            Assert.Throws<ArgumentNullException>(() => source.TransferUpTo(null, 0, 1));
        }

        [Test]
        public void Transfer_ToSelf_Throws()
        {
            var source = new Inventory<TestItem>(3);
            source.TryAdd(TestItems.NonStackable("a"));

            Assert.Throws<ArgumentException>(() => source.TryTransferTo(source, 0));
            Assert.Throws<ArgumentException>(() => source.TryTransferTo(source, 0, 1));
            Assert.Throws<ArgumentException>(() => source.TransferUpTo(source, 0, 1));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Transfer_InvalidIndex_Throws(int index)
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);

            Assert.Throws<ArgumentOutOfRangeException>(() => source.TryTransferTo(target, index));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TryTransferTo(target, index, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.TransferUpTo(target, index, 1));
        }
    }
}
