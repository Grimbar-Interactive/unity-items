using System;
using System.Linq;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventorySourceSyncTests
    {
        [Test]
        public void SourceLosesUnits_TrimsTheHighestIndexSlotsFirst()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2

            source.TryRemove("potion", 3);

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(5, linked[0].Quantity);
            Assert.AreEqual(4, linked[1].Quantity);
            Assert.AreEqual(9, linked.GetQuantity("potion"));
        }

        [Test]
        public void SourceLosesUnits_FixedLoadoutKeepsSlotIndices()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, 3, maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2

            source.TryRemove("potion", 3);

            Assert.AreEqual(3, linked.SlotCount);
            Assert.AreEqual(5, linked[0].Quantity);
            Assert.AreEqual(4, linked[1].Quantity);
            Assert.IsTrue(linked[2].IsEmpty);
        }

        [Test]
        public void SourceLosesUnits_RaisesRemovalEventsForTheTrimmedUnits()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            source.TryRemove("potion", 3);

            Assert.AreEqual(3, recorder.Removed.Sum(r => r.Quantity));
            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void SourceLosesUnitsTheLoadoutDoesNotNeed_LoadoutIsUntouched()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            source.TryRemove("potion", 10);

            Assert.AreEqual(10, linked.GetQuantity("potion"));
            Assert.AreEqual(0, recorder.UpdatedCount);
            Assert.AreEqual(0, recorder.RemovedCount);
        }

        [Test]
        public void SourceGainsItems_LoadoutIsUntouched()
        {
            var source = SourceWithPotions(5);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 5);

            source.TryAdd(Potion(), 20);

            Assert.AreEqual(5, linked.GetQuantity("potion"));
        }

        [Test]
        public void SourceLosesAReferencedInstance_ItsSlotIsClearedAndOthersShiftDown()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var c = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);
            linked.TryAdd(c);

            source.TryRemoveInstance(b);

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreSame(a, linked[0].Item);
            Assert.AreSame(c, linked[1].Item);
        }

        [Test]
        public void SourceLosesAReferencedInstance_FixedLoadoutLeavesAnEmptySlot()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(a);
            linked.TryAdd(b);

            source.TryRemoveInstance(a);

            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreSame(b, linked[1].Item);
        }

        [Test]
        public void SourceClear_EmptiesTheLoadout()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);
            linked.TryAdd(sword);

            source.Clear();

            Assert.IsTrue(linked.IsEmpty);
            Assert.AreEqual(0, linked.SlotCount);
        }

        [Test]
        public void SourceReorganising_DoesNotDisturbTheLoadout()
        {
            var source = new Inventory<TestItem>(4);
            var a = AddSword(source, "a");
            var b = AddSword(source, "b");
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            source.Swap(0, 3);
            source.Compact();

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void SourceBatch_DefersTheSyncUntilTheBatchEnds()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 20);
            linked.TryAdd(Potion(), 20);

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 20);
                Assert.AreEqual(20, linked.GetQuantity("potion"));
            }

            Assert.AreEqual(5, linked.GetQuantity("potion"));
        }

        [Test]
        public void Reconcile_CanBeCalledExplicitlyDuringASourceBatch()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 20);
            linked.TryAdd(Potion(), 20);

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 20);
                linked.Reconcile();
                Assert.AreEqual(5, linked.GetQuantity("potion"));
            }
        }

        [Test]
        public void Reconcile_RefreshesTheCachedItemWhenTheSourceReplacesIt_Stackable()
        {
            var source = new Inventory<TestItem>();
            source.TryAdd(Potion(), 25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);
            var replacement = Potion();

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 25);
                source.TryAdd(replacement, 25);
            }

            Assert.AreEqual(10, linked.GetQuantity("potion"));
            Assert.AreSame(replacement, linked[0].Item);
        }

        [Test]
        public void Reconcile_RefreshesTheCachedItemWhenTheSourceReplacesIt_NonStackable()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(sword);
            var restored = new TestItem(new FakeItemAttributes("sword"), sword.InstanceId.Value);

            using (source.BeginBatchUpdate())
            {
                source.TryRemoveInstance(sword);
                source.TryAdd(restored);
            }

            Assert.AreEqual(1, linked.SlotCount);
            Assert.AreSame(restored, linked[0].Item);
        }

        [Test]
        public void Reconcile_WhenAlreadyInSync_RaisesNothing()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.Reconcile();

            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void Dispose_StopsFollowingTheSource()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);

            linked.Dispose();
            source.TryRemove("potion", 25);

            Assert.IsTrue(linked.IsDisposed);
            Assert.AreEqual(10, linked.GetQuantity("potion"));
        }

        [Test]
        public void Dispose_MutatorsThrow_QueriesKeepWorking()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(Potion(), 10);
            linked.Dispose();

            Assert.Throws<ObjectDisposedException>(() => linked.TryAdd(Potion()));
            Assert.Throws<ObjectDisposedException>(() => linked.AddUpTo(Potion(), 5));
            Assert.Throws<ObjectDisposedException>(() => linked.TryRemove("potion"));
            Assert.Throws<ObjectDisposedException>(() => linked.Clear());
            Assert.Throws<ObjectDisposedException>(() => linked.Swap(0, 1));
            Assert.Throws<ObjectDisposedException>(() => linked.TryConsume(Potion()));
            Assert.AreEqual(10, linked.GetQuantity("potion"));
            Assert.AreEqual(1, linked.FilledSlotCount);
            Assert.DoesNotThrow(() => linked.Reconcile());
        }

        [Test]
        public void Reconcile_ReplacedCachedItem_IsReportedAsASlotChange()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(sword);
            var restored = new TestItem(new FakeItemAttributes("sword"), sword.InstanceId.Value);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            using (source.BeginBatchUpdate())
            {
                source.TryRemoveInstance(sword);
                source.TryAdd(restored);
            }

            Assert.AreSame(restored, linked[0].Item);
            CollectionAssert.AreEqual(new[] { 0 }, recorder.ChangedSlots);
            Assert.AreEqual(1, recorder.UpdatedCount);
            Assert.AreEqual(0, recorder.RemovedCount);
        }

        [Test]
        public void TryConsume_WhileTheSourceIsBatchedAndHasLostTheUnits_FailsWithoutChangingAnything()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 20);
            linked.TryAdd(Potion(), 20);

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 20); // the loadout is stale until the batch ends

                Assert.IsFalse(linked.TryConsume(Potion(), 10));
                Assert.AreEqual(20, linked.GetQuantity("potion"));
                Assert.AreEqual(5, source.GetQuantity("potion"));
            }

            Assert.AreEqual(5, linked.GetQuantity("potion"));
            Assert.AreEqual(5, source.GetQuantity("potion"));
        }

        [Test]
        public void Dispose_EveryMutatorThrowsObjectDisposed_EvenForAStaleIndex()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(Potion(), 10);
            linked.Dispose();
            Comparison<TestItem> comparison = (a, b) => 0;

            Assert.Throws<ObjectDisposedException>(() => linked.TryAdd("potion"));
            Assert.Throws<ObjectDisposedException>(() => linked.TryAdd(sword.InstanceId.Value));
            Assert.Throws<ObjectDisposedException>(() => linked.AddUpTo(Potion(), 2));
            Assert.Throws<ObjectDisposedException>(() => linked.TryAddAt(1, Potion()));
            Assert.Throws<ObjectDisposedException>(() => linked.TryAddAt(1, "potion"));
            Assert.Throws<ObjectDisposedException>(() => linked.TryAddAt(1, sword.InstanceId.Value));
            Assert.Throws<ObjectDisposedException>(() => linked.RemoveUpTo("potion", 1));
            Assert.Throws<ObjectDisposedException>(() => linked.TryRemoveInstance(sword));
            Assert.Throws<ObjectDisposedException>(() => linked.TryRemoveAt(0, 1));
            Assert.Throws<ObjectDisposedException>(() => linked.TryRemoveAt(0, out _, out _));
            Assert.Throws<ObjectDisposedException>(() => linked.TryConsumeAt(0));
            Assert.Throws<ObjectDisposedException>(() => linked.Move(0, 1));
            Assert.Throws<ObjectDisposedException>(() => linked.Compact());
            Assert.Throws<ObjectDisposedException>(() => linked.Consolidate());
            Assert.Throws<ObjectDisposedException>(() => linked.Sort(comparison));

            // The disposed check wins over argument validation.
            Assert.Throws<ObjectDisposedException>(() => linked.Swap(0, 99));
            Assert.Throws<ObjectDisposedException>(() => linked.TryAddAt(99, Potion()));
            Assert.Throws<ObjectDisposedException>(() => linked.TryRemoveAt(99, 1));
        }

        [Test]
        public void SourceHandlerThrows_LoadoutStillReconciles()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12);
            source.OnItemRemoved += (_, _) => throw new InvalidOperationException("boom");

            Assert.Throws<InvalidOperationException>(() => source.TryRemove("potion", 3));

            Assert.AreEqual(9, source.GetQuantity("potion"));
            Assert.AreEqual(9, linked.GetQuantity("potion"));
        }

        [Test]
        public void EarlierUpdatedHandlerThrows_LoadoutStillReconciles()
        {
            var source = SourceWithPotions(12);
            source.OnInventoryUpdated += () => throw new InvalidOperationException("boom");
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12);

            Assert.Throws<InvalidOperationException>(() => source.TryRemove("potion", 3));

            Assert.AreEqual(9, linked.GetQuantity("potion"));
        }

        [Test]
        public void LoadoutHandlerThrows_OtherLoadoutsStillReconcile()
        {
            var source = SourceWithPotions(12);
            var first = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            var second = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            first.TryAdd(Potion(), 12);
            second.TryAdd(Potion(), 12);
            first.OnItemRemoved += (_, _) => throw new InvalidOperationException("boom");

            Assert.Throws<InvalidOperationException>(() => source.TryRemove("potion", 3));

            Assert.AreEqual(9, source.GetQuantity("potion"));
            Assert.AreEqual(9, first.GetQuantity("potion"));
            Assert.AreEqual(9, second.GetQuantity("potion"));
        }

        [Test]
        public void TwoLoadoutHandlersThrow_FirstExceptionSurfaces_AllReconcile()
        {
            var source = SourceWithPotions(12);
            var first = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            var second = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            var third = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            first.TryAdd(Potion(), 12);
            second.TryAdd(Potion(), 12);
            third.TryAdd(Potion(), 12);
            first.OnItemRemoved += (_, _) => throw new InvalidOperationException("first");
            second.OnItemRemoved += (_, _) => throw new InvalidOperationException("second");

            var thrown = Assert.Throws<InvalidOperationException>(() => source.TryRemove("potion", 3));

            Assert.AreEqual("first", thrown.Message);
            Assert.AreEqual(9, first.GetQuantity("potion"));
            Assert.AreEqual(9, second.GetQuantity("potion"));
            Assert.AreEqual(9, third.GetQuantity("potion"));
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            linked.Dispose();
            Assert.DoesNotThrow(() => linked.Dispose());
        }
    }
}
