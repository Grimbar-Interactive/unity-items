using System.Reflection;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryEventTests
    {
        [Test]
        public void TryAdd_FiresOnItemAdded_OnceWithItemAndTotalQuantity()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);
            var item = TestItems.Stackable("disc", max: 10);

            inventory.TryAdd(item, 5);

            Assert.AreEqual(1, recorder.AddedCount);
            Assert.AreSame(item, recorder.Added[0].Item);
            Assert.AreEqual(5, recorder.Added[0].Quantity);
        }

        [Test]
        public void TryAdd_SpanningThreeSlots_FiresOnItemAdded_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 25);

            Assert.AreEqual(1, recorder.AddedCount);
        }

        [Test]
        public void TryAdd_SpanningThreeSlots_FiresOnSlotChanged_OncePerAffectedSlot_Ascending()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 25);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, recorder.ChangedSlots);
        }

        [Test]
        public void TryAdd_FiresOnInventoryUpdated_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 25);

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void TryAdd_EventOrderIsItemThenSlotThenUpdated()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual("Added", recorder.Order[0]);
            Assert.AreEqual("SlotChanged", recorder.Order[1]);
            Assert.AreEqual("Updated", recorder.Order[2]);
        }

        [Test]
        public void TryAdd_Failure_FiresNothing()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(TestItems.NonStackable("patch"), 1);

            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void TryRemove_FiresOnItemRemoved_OnceWithTotalQuantity()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemove("disc", 5, out _);

            Assert.AreEqual(1, recorder.RemovedCount);
            Assert.AreEqual(5, recorder.Removed[0].Quantity);
        }

        [Test]
        public void TryRemove_FiresOnInventoryUpdated_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemove("disc", 5, out _);

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void TryRemoveAt_FiresOnItemRemoved_OnSlotChanged_AndOnInventoryUpdated()
        {
            var inventory = new Inventory<TestItem>(1);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemoveAt(0, out _, out _);

            Assert.AreEqual(1, recorder.RemovedCount);
            Assert.AreEqual(1, recorder.SlotChangedCount);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void TryRemove_SpanningTwoStacks_FiresOnItemRemoved_Once_WithCombinedQuantity()
        {
            var inventory = new Inventory<TestItem>(2, allowDuplicates: true);
            inventory.TryAdd(TestItems.Stackable("disc", max: 5), 5);
            inventory.SplitStack(0, 2, 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemove("disc", 5, out _);

            Assert.AreEqual(1, recorder.RemovedCount);
            Assert.AreEqual(5, recorder.Removed[0].Quantity);
        }

        [Test]
        public void Clear_FiresOnItemRemoved_OncePerClearedStack()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Clear();

            Assert.AreEqual(2, recorder.RemovedCount);
        }

        [Test]
        public void Clear_FiresOnInventoryUpdated_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Clear();

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Swap_FiresOnSlotChanged_ForBothIndices_AndOnInventoryUpdated_Once()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Swap(0, 1);

            CollectionAssert.AreEqual(new[] { 0, 1 }, recorder.ChangedSlots);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Swap_FiresNoItemAddedOrRemoved()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Swap(0, 1);

            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(0, recorder.RemovedCount);
        }

        [Test]
        public void Move_FiresOnSlotChanged_ForBothIndices()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Move(0, 1);

            CollectionAssert.AreEqual(new[] { 0, 1 }, recorder.ChangedSlots);
        }

        [Test]
        public void SplitStack_FiresOnSlotChanged_ForSourceAndDestination()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.SplitStack(0, 2);

            CollectionAssert.AreEqual(new[] { 0, 1 }, recorder.ChangedSlots);
        }

        [Test]
        public void MergeStacks_FiresOnSlotChanged_ForBothIndices()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 6);
            inventory.SplitStack(0, 2, 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.MergeStacks(1, 0);

            CollectionAssert.AreEqual(new[] { 0, 1 }, recorder.ChangedSlots);
        }

        [Test]
        public void Consolidate_FiresOnInventoryUpdated_Once_RegardlessOfSlotsTouched()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 12);
            inventory.SplitStack(0, 4, 2);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Consolidate();

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Compact_FiresOnInventoryUpdated_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.Swap(0, 2);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Compact();

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Sort_FiresOnInventoryUpdated_ExactlyOnce()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void Sort_FiresNoItemAddedOrRemoved()
        {
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));

            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(0, recorder.RemovedCount);
        }

        [Test]
        public void BeginBatchUpdate_TenAdds_FireOnInventoryUpdated_Once()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
                for (var i = 0; i < 10; i++) inventory.TryAdd(TestItems.NonStackable($"item{i}"), 1);
            }

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void BeginBatchUpdate_StillFiresOnItemAdded_OncePerLogicalAdd()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
                for (var i = 0; i < 10; i++) inventory.TryAdd(TestItems.NonStackable($"item{i}"), 1);
            }

            Assert.AreEqual(10, recorder.AddedCount);
        }

        [Test]
        public void BeginBatchUpdate_CoalescesRepeatedChangesToOneSlot_IntoOneOnSlotChanged()
        {
            var inventory = new Inventory<TestItem>(1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
                inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
                inventory.TryAdd(TestItems.Stackable("disc", max: 10), 3);
            }

            Assert.AreEqual(1, recorder.SlotChangedCount);
        }

        [Test]
        public void BeginBatchUpdate_Nested_FlushesOnlyOnOutermostDispose()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
                using (inventory.BeginBatchUpdate())
                {
                    inventory.TryAdd(TestItems.NonStackable("a"), 1);
                }

                Assert.AreEqual(0, recorder.UpdatedCount);
                inventory.TryAdd(TestItems.NonStackable("b"), 1);
            }

            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void BeginBatchUpdate_NoChanges_FiresNothing()
        {
            var inventory = new Inventory<TestItem>();
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
            }

            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void BeginBatchUpdate_SlotChangedIndicesAreAscendingAndDeduped()
        {
            var inventory = new Inventory<TestItem>(3);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            using (inventory.BeginBatchUpdate())
            {
                inventory.TryAdd(TestItems.NonStackable("c"), 1);
                inventory.TryAdd(TestItems.NonStackable("b"), 1);
                inventory.TryAdd(TestItems.NonStackable("a"), 1);
                inventory.TryRemoveAt(1, out _, out _);
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, recorder.ChangedSlots);
        }

        [Test]
        public void Events_WithNoSubscribers_DoNotThrow()
        {
            var inventory = new Inventory<TestItem>();
            Assert.DoesNotThrow(() => inventory.TryAdd(TestItems.NonStackable("a"), 1));
            Assert.DoesNotThrow(() => inventory.TryRemove("a", 1, out _));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void OnSlotChanged_PayloadIsTheMutatedSlotIndex(int index)
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);
            inventory.TryAdd(TestItems.NonStackable("c"), 1);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryRemoveAt(index, out _, out _);

            CollectionAssert.AreEqual(new[] { index }, recorder.ChangedSlots);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var inventory = new Inventory<TestItem>();
            var recorder = new InventoryEventRecorder<TestItem>(inventory);
            recorder.Dispose();

            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            Assert.AreEqual(0, recorder.AddedCount);
        }

        [Test]
        public void OnItemAdded_IsAnEventNotAField()
        {
            var type = typeof(Inventory<TestItem>);
            Assert.IsNotNull(type.GetEvent("OnItemAdded"));
            Assert.IsNull(type.GetField("OnItemAdded", BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
