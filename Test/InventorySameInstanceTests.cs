using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// One object reference (one InstanceId) may occupy at most one slot when it is non-stackable.
    /// </summary>
    public class InventorySameInstanceTests
    {
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
