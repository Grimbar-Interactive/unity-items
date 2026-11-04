using System;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryRemoveTests
    {
        [Test]
        public void TryRemove_UnassignsUnitsAndLeavesTheSourceAlone()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 12);

            Assert.IsTrue(linked.TryRemove(Potion(), 5));

            Assert.AreEqual(7, linked.GetQuantity("potion"));
            Assert.AreEqual(25, source.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_DrainsInAscendingSlotOrder()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2

            linked.TryRemove("potion", 6);

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(4, linked[0].Quantity);
            Assert.AreEqual(2, linked[1].Quantity);
        }

        [Test]
        public void TryRemove_MoreThanAssigned_FailsAtomically()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            Assert.IsFalse(linked.TryRemove("potion", 5, out var remainder));
            Assert.AreEqual(5, remainder);
            Assert.AreEqual(4, linked.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_SuccessReportsZeroRemainder()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            Assert.IsTrue(linked.TryRemove(Potion(), 4, out var remainder));
            Assert.AreEqual(0, remainder);
        }

        [Test]
        public void TryRemove_InvalidArguments_Fail()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            Assert.IsFalse(linked.TryRemove((TestItem)null));
            Assert.IsFalse(linked.TryRemove("", 1));
            Assert.IsFalse(linked.TryRemove("potion", 0));
            Assert.IsFalse(linked.TryRemove("ether", 1));
        }

        [Test]
        public void CanRemove_AgreesWithTryRemove()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            Assert.IsTrue(linked.CanRemove(Potion(), 4));
            Assert.IsTrue(linked.CanRemove("potion", 1));
            Assert.IsFalse(linked.CanRemove(Potion(), 5));
            Assert.IsFalse(linked.CanRemove("potion", 0));
            Assert.IsFalse(linked.CanRemove((TestItem)null));
        }

        [Test]
        public void RemoveUpTo_ReturnsWhatWasActuallyRemoved()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            Assert.AreEqual(4, linked.RemoveUpTo(Potion(), 9));
            Assert.AreEqual(0, linked.RemoveUpTo("potion", 1));
            Assert.AreEqual(0, linked.RemoveUpTo((TestItem)null, 1));
        }

        [Test]
        public void TryRemoveInstance_UnassignsOnlyThatInstance()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);

            Assert.IsTrue(linked.TryRemoveInstance(a));

            Assert.IsFalse(linked.ContainsInstance(a));
            Assert.IsTrue(linked.ContainsInstance(b));
            Assert.IsTrue(source.ContainsInstance(a));
        }

        [Test]
        public void TryRemoveInstance_StackableOrUnassigned_ReturnsFalse()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 4);

            Assert.IsFalse(linked.TryRemoveInstance(Potion()));
            Assert.IsFalse(linked.TryRemoveInstance(sword));
            Assert.IsFalse(linked.TryRemoveInstance(null));
        }

        [Test]
        public void TryRemoveAt_Whole_ReturnsWhatWasThere()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);
            linked.TryAddAt(1, Potion(), 6);

            Assert.IsTrue(linked.TryRemoveAt(1, out var item, out var quantity));

            Assert.AreEqual("potion", item.UniqueId);
            Assert.AreEqual(6, quantity);
            Assert.IsTrue(linked[1].IsEmpty);
        }

        [Test]
        public void TryRemoveAt_EmptySlot_ReturnsFalseWithNothing()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);

            Assert.IsFalse(linked.TryRemoveAt(0, out var item, out var quantity));
            Assert.IsNull(item);
            Assert.AreEqual(0, quantity);
        }

        [Test]
        public void TryRemoveAt_Partial_LeavesTheRest_AndIsAtomic()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);
            linked.TryAddAt(0, Potion(), 6);

            Assert.IsFalse(linked.TryRemoveAt(0, 7));
            Assert.AreEqual(6, linked[0].Quantity);
            Assert.IsTrue(linked.TryRemoveAt(0, 4));
            Assert.AreEqual(2, linked[0].Quantity);
            Assert.IsFalse(linked.TryRemoveAt(0, 0));
        }

        [Test]
        public void TryRemoveAt_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryRemoveAt(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryRemoveAt(-1, out _, out _));
        }

        [Test]
        public void Unbounded_RemovingASlot_ShiftsLaterSlotsDown()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);

            linked.TryRemoveAt(0, 1);

            Assert.AreEqual(1, linked.SlotCount);
            Assert.AreSame(b, linked[0].Item);
        }

        [Test]
        public void Fixed_RemovingASlot_KeepsTheIndices()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(a);
            linked.TryAdd(b);

            linked.TryRemoveAt(0, 1);

            Assert.AreEqual(3, linked.SlotCount);
            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreSame(b, linked[1].Item);
        }

        [Test]
        public void Clear_Fixed_KeepsSlotCount_Unbounded_DropsToZero_SourceUntouched()
        {
            var source = SourceWithPotions(25);
            var fixedLoadout = new LinkedInventory<TestItem>(source, 3);
            var unbounded = new LinkedInventory<TestItem>(source);
            fixedLoadout.TryAdd(Potion(), 5);
            unbounded.TryAdd(Potion(), 5);

            fixedLoadout.Clear();
            unbounded.Clear();

            Assert.AreEqual(3, fixedLoadout.SlotCount);
            Assert.IsTrue(fixedLoadout.IsEmpty);
            Assert.AreEqual(0, unbounded.SlotCount);
            Assert.AreEqual(25, source.GetQuantity("potion"));
        }

        [Test]
        public void Clear_WhenAlreadyEmpty_RaisesNothing()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.Clear();

            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void Clear_RaisesOneRemovalPerClearedSlotAndOneUpdate()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 12);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.Clear();

            Assert.AreEqual(3, recorder.RemovedCount);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }
    }
}
