using System;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryConsumeTests
    {
        [Test]
        public void TryConsume_Stackable_RemovesFromBothTheSourceAndTheLoadout()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);

            Assert.IsTrue(linked.TryConsume(Potion(), 3));

            Assert.AreEqual(22, source.GetQuantity("potion"));
            Assert.AreEqual(7, linked.GetQuantity("potion"));
        }

        [Test]
        public void TryConsume_RaisesOneRemovalAndOneUpdateOnTheLoadout()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            linked.TryConsume(Potion(), 3);

            Assert.AreEqual(1, recorder.RemovedCount);
            Assert.AreEqual(3, recorder.Removed[0].Quantity);
            Assert.AreEqual(1, recorder.UpdatedCount);
        }

        [Test]
        public void TryConsume_MoreThanTheLoadoutHolds_FailsEvenIfTheSourceHasEnough()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 4);

            Assert.IsFalse(linked.TryConsume(Potion(), 5));
            Assert.AreEqual(25, source.GetQuantity("potion"));
            Assert.AreEqual(4, linked.GetQuantity("potion"));
        }

        [Test]
        public void TryConsume_WhenTheSourceCannotSupplyIt_FailsWithoutChangingAnything()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 20); // the loadout is stale until the batch ends

                Assert.IsFalse(linked.TryConsume(Potion(), 8));
                Assert.AreEqual(5, source.GetQuantity("potion"));
                Assert.AreEqual(10, linked.GetQuantity("potion"));
            }
        }

        [Test]
        public void TryConsume_DrainsSlotsInAscendingOrder()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2

            Assert.IsTrue(linked.TryConsume(Potion(), 7));

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(3, linked[0].Quantity);
            Assert.AreEqual(2, linked[1].Quantity);
            Assert.AreEqual(5, source.GetQuantity("potion"));
        }

        [Test]
        public void TryConsume_Everything_EmptiesBoth()
        {
            var source = SourceWithPotions(10);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 10);

            Assert.IsTrue(linked.TryConsume(Potion(), 10));

            Assert.IsTrue(source.IsEmpty);
            Assert.IsTrue(linked.IsEmpty);
        }

        [Test]
        public void TryConsume_NonStackable_RemovesTheInstanceFromBoth()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var other = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(sword);
            linked.TryAdd(other);

            Assert.IsTrue(linked.TryConsume(sword));

            Assert.IsFalse(source.ContainsInstance(sword));
            Assert.IsTrue(source.ContainsInstance(other));
            Assert.IsFalse(linked.ContainsInstance(sword));
            Assert.IsTrue(linked.ContainsInstance(other));
        }

        [Test]
        public void TryConsume_NonStackable_QuantityAboveOne_Fails()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(sword);

            Assert.IsFalse(linked.TryConsume(sword, 2));
            Assert.IsTrue(source.ContainsInstance(sword));
        }

        [Test]
        public void TryConsume_NotAssigned_FailsAndLeavesTheSourceAlone()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsFalse(linked.TryConsume(Potion()));
            Assert.IsFalse(linked.TryConsume(sword));
            Assert.AreEqual(25, source.GetQuantity("potion"));
            Assert.IsTrue(source.ContainsInstance(sword));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryConsume_NonPositiveQuantity_Fails(int quantity)
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 5);

            Assert.IsFalse(linked.TryConsume(Potion(), quantity));
            Assert.IsFalse(linked.TryConsume(null));
        }

        [Test]
        public void TryConsumeAt_DrainsExactlyTheNamedSlot()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2

            Assert.IsTrue(linked.TryConsumeAt(0, 2));

            Assert.AreEqual(3, linked[0].Quantity);
            Assert.AreEqual(5, linked[1].Quantity);
            Assert.AreEqual(2, linked[2].Quantity);
            Assert.AreEqual(10, source.GetQuantity("potion"));
        }

        [Test]
        public void TryConsumeAt_WholeSlot_RemovesItAndTheUnits()
        {
            var source = SourceWithPotions(12);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 5);
            linked.TryAdd(Potion(), 12);

            Assert.IsTrue(linked.TryConsumeAt(2, 2));

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(10, source.GetQuantity("potion"));
        }

        [Test]
        public void TryConsumeAt_NonStackableSlot_RemovesTheInstanceFromTheSource()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 2);
            linked.TryAddAt(1, sword);

            Assert.IsTrue(linked.TryConsumeAt(1));

            Assert.IsTrue(source.IsEmpty);
            Assert.IsTrue(linked[1].IsEmpty);
        }

        [Test]
        public void TryConsumeAt_EmptySlotOrTooMany_Fails()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAddAt(0, Potion(), 4);

            Assert.IsFalse(linked.TryConsumeAt(1));
            Assert.IsFalse(linked.TryConsumeAt(0, 5));
            Assert.IsFalse(linked.TryConsumeAt(0, 0));
            Assert.AreEqual(25, source.GetQuantity("potion"));
            Assert.AreEqual(4, linked[0].Quantity);
        }

        [Test]
        public void TryConsumeAt_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryConsumeAt(2));
        }

        [Test]
        public void TryConsumeAt_WhenTheSourceCannotSupplyIt_FailsWithoutChangingAnything()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source, maxStackSize: 20);
            linked.TryAdd(Potion(), 20);

            using (source.BeginBatchUpdate())
            {
                source.TryRemove("potion", 20);

                Assert.IsFalse(linked.TryConsumeAt(0, 10));
                Assert.AreEqual(20, linked[0].Quantity);
            }
        }
    }
}
