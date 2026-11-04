using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// One InstanceId may occupy at most one slot when its item is non-stackable, whether the
    /// second candidate is the same object or a distinct object carrying the same id. Stackable
    /// items have no InstanceId and are never matched by instance.
    /// </summary>
    public class InventorySameInstanceTests
    {
        private static (TestItem First, TestItem Second) TwinsWithSameInstanceId(string uniqueId = "sword")
        {
            var attributes = new FakeItemAttributes(uniqueId);
            var id = Guid.NewGuid();
            return (new TestItem(attributes, id), new TestItem(attributes, id));
        }

        [Test]
        public void TryAdd_DistinctObjectWithSameInstanceId_IsRejected()
        {
            var inventory = new Inventory<TestItem>(5);
            var (first, second) = TwinsWithSameInstanceId();
            inventory.TryAdd(first);

            Assert.IsFalse(inventory.TryAdd(second));
            Assert.AreEqual(1, inventory.FilledSlotCount);
        }

        [Test]
        public void TryAddAt_DistinctObjectWithSameInstanceId_IsRejected()
        {
            var inventory = new Inventory<TestItem>(5);
            var (first, second) = TwinsWithSameInstanceId();
            inventory.TryAddAt(0, first);

            Assert.IsFalse(inventory.TryAddAt(3, second));
            Assert.IsTrue(inventory[3].IsEmpty);
        }

        [Test]
        public void AddRange_DistinctObjectsWithSameInstanceId_ReportsSecondAsNotAdded()
        {
            var inventory = new Inventory<TestItem>(5);
            var (first, second) = TwinsWithSameInstanceId();

            var added = inventory.AddRange(new[] { first, second }, out var notAdded);

            Assert.AreEqual(1, added);
            Assert.AreEqual(1, notAdded.Count);
            Assert.AreSame(second, notAdded[0]);
        }

        [Test]
        public void TryTransferTo_TargetHoldsSameInstanceId_FailsAndChangesNothing()
        {
            var source = new Inventory<TestItem>(3);
            var target = new Inventory<TestItem>(3);
            var (first, second) = TwinsWithSameInstanceId();
            source.TryAdd(first);
            target.TryAdd(second);
            using var recorder = new InventoryEventRecorder<TestItem>(target);

            Assert.IsFalse(source.TryTransferTo(target, 0));

            Assert.IsTrue(source.ContainsInstance(first));
            Assert.AreEqual(1, target.FilledSlotCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void InstanceLookups_Stackable_NeverMatch()
        {
            var inventory = new Inventory<TestItem>(5);
            var potion = TestItems.Stackable("potion", max: 10);
            inventory.TryAdd(potion, 3);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            Assert.AreEqual(-1, inventory.IndexOfInstance(potion));
            Assert.IsFalse(inventory.ContainsInstance(potion));
            Assert.IsFalse(inventory.TryRemoveInstance(potion));

            Assert.AreEqual(3, inventory.GetQuantity("potion"));
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void TryAdd_StackableOverflow_UsesSameReferenceInSeveralSlots()
        {
            var inventory = new Inventory<TestItem>(5);
            var potion = TestItems.Stackable("potion", max: 10);

            Assert.IsTrue(inventory.TryAdd(potion, 25));

            Assert.AreEqual(3, inventory.FilledSlotCount);
            Assert.AreEqual(25, inventory.GetQuantity("potion"));
        }

        [Test]
        public void TryAdd_SameNonStackableInstanceTwice_SecondIsRejected()
        {
            var inventory = new Inventory<TestItem>();
            var sword = TestItems.NonStackable("sword");

            Assert.IsTrue(inventory.TryAdd(sword));
            Assert.IsFalse(inventory.TryAdd(sword));
            Assert.AreEqual(1, inventory.SlotCount);
        }

        [Test]
        public void TryAdd_SameNonStackableInstanceTwice_Fixed_SecondIsRejected()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");

            inventory.TryAdd(sword);

            Assert.IsFalse(inventory.TryAdd(sword));
            Assert.AreEqual(1, inventory.FilledSlotCount);
        }

        [Test]
        public void TryAdd_RejectedRepeat_FiresNothing()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAdd(sword);

            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(0, recorder.SlotChangedCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void AddUpTo_HeldNonStackableInstance_ReturnsZero()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);

            Assert.AreEqual(0, inventory.AddUpTo(sword, 1));
        }

        [Test]
        public void GetFreeSpaceFor_HeldNonStackableInstance_IsZero()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);

            Assert.AreEqual(0, inventory.GetFreeSpaceFor(sword));
            Assert.IsFalse(inventory.CanAdd(sword));
        }

        [Test]
        public void AddRange_RepeatedInstance_ReportsTheRepeatAsNotAdded()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");

            var added = inventory.AddRange(new[] { sword, sword }, out var notAdded);

            Assert.AreEqual(1, added);
            Assert.AreEqual(1, notAdded.Count);
            Assert.AreSame(sword, notAdded[0]);
        }

        [Test]
        public void TryAdd_DistinctInstancesOfSameKind_AreStillAccepted()
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("sword")));
            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("sword")));
            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        [Test]
        public void TryAdd_AfterRemovingTheInstance_ItCanBeAddedAgain()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);
            inventory.TryRemoveInstance(sword);

            Assert.IsTrue(inventory.TryAdd(sword));
        }

        [Test]
        public void TryAdd_StackableInstance_StillStacksWithItself()
        {
            var inventory = new Inventory<TestItem>();
            var potion = TestItems.Stackable("potion", max: 10);

            Assert.IsTrue(inventory.TryAdd(potion, 3));
            Assert.IsTrue(inventory.TryAdd(potion, 4));

            Assert.AreEqual(1, inventory.SlotCount);
            Assert.AreEqual(7, inventory[0].Quantity);
        }
    }
}
