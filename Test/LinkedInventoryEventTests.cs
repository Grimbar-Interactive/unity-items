using System;
using System.Collections.Generic;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryEventTests
    {
        [Test]
        public void Add_SpanningSlots_RaisesOneAddedWithTheTotal_SlotEventsAscending_ThenOneUpdate()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryAdd(Potion(), 12);

            Assert.AreEqual(1, recorder.AddedCount);
            Assert.AreEqual(12, recorder.Added[0].Quantity);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, recorder.ChangedSlots);
            Assert.AreEqual(1, recorder.UpdatedCount);
            CollectionAssert.AreEqual(
                new[] { "Added", "SlotChanged", "SlotChanged", "SlotChanged", "Updated" }, recorder.Order);
        }

        [Test]
        public void FailedAdd_RaisesNothing()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(5));
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryAdd(Potion(), 6);
            linked.TryAdd(TestItems.Stackable("ether", 10));

            Assert.AreEqual(0, recorder.Order.Count);
        }

        [Test]
        public void Remove_RaisesOneRemovedWithTheTotal()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 12);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryRemove("potion", 7);

            Assert.AreEqual(1, recorder.RemovedCount);
            Assert.AreEqual(7, recorder.Removed[0].Quantity);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Batch_CoalescesSeveralOperationsIntoOneUpdate()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 4);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            using (linked.BeginBatchUpdate())
            {
                linked.TryAddAt(0, Potion(), 2);
                linked.TryAddAt(0, Potion(), 2);
                linked.TryAddAt(1, Potion(), 2);
                Assert.AreEqual(0, recorder.UpdatedCount);
            }

            Assert.AreEqual(1, recorder.UpdatedCount);
            Assert.AreEqual(3, recorder.AddedCount);
            CollectionAssert.AreEqual(new[] { 0, 1 }, recorder.ChangedSlots);
        }

        [Test]
        public void Batch_DisposedTwice_DoesNotUnbalanceEvents()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            var batch = linked.BeginBatchUpdate();
            batch.Dispose();
            batch.Dispose();
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryAdd(Potion());

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void SourceSync_SeveralTrimmedSlots_ReportAsOneUpdate()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            source.Clear();

            Assert.AreEqual(1, recorder.UpdatedCount);
            Assert.AreEqual(12, TotalRemoved(recorder));
        }

        [Test]
        public void SourceSync_TrimSpanningSeveralSlots_RaisesOneRemovalWithTheTotal()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            source.TryRemove("potion", 8); // the loadout may keep 4: trims 2 + 5 + 1 across three slots

            Assert.AreEqual(4, linked.GetQuantity("potion"));
            Assert.AreEqual(1, recorder.Removed.Count);
            Assert.AreEqual(8, recorder.Removed[0].Quantity);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void SlotChanged_ForAShiftedUnboundedSlot_IsReported()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryRemoveAt(0, 1);

            CollectionAssert.AreEqual(new[] { 0 }, recorder.ChangedSlots);
        }

        [Test]
        public void Handlers_MayMutateTheLoadoutSafely()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            var reentered = false;
            linked.OnInventoryUpdated += () =>
            {
                if (reentered) return;
                reentered = true;
                linked.TryAdd(Potion());
            };

            linked.TryAdd(Potion(), 2);

            Assert.AreEqual(3, linked.GetQuantity("potion"));
        }

        [Test]
        public void Enumerating_WhileTheLoadoutChanges_Throws()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 10);

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in linked) linked.TryRemove("potion", 1);
            });
        }

        [Test]
        public void Enumeration_ListsReferencedItemsInSlotOrder_SkippingEmptySlots()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source, "a");
            var b = AddSword(source, "b");
            var linked = new LinkedInventory<TestItem>(source, 4);
            linked.TryAddAt(1, a);
            linked.TryAddAt(3, b);

            CollectionAssert.AreEqual(new[] { a, b }, new List<TestItem>(linked));
        }

        private static int TotalRemoved(InventoryEventRecorder<TestItem> recorder)
        {
            var total = 0;
            foreach (var removed in recorder.Removed) total += removed.Quantity;
            return total;
        }
    }
}
