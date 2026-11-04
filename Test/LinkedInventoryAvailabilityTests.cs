using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryAvailabilityTests
    {
        [Test]
        public void GetUnassignedQuantity_Stackable_IsSourceQuantityMinusAssigned()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            Assert.AreEqual(25, linked.GetUnassignedQuantity(Potion()));

            linked.TryAdd(Potion(), 10);

            Assert.AreEqual(15, linked.GetUnassignedQuantity(Potion()));
            Assert.AreEqual(15, linked.GetUnassignedQuantity("potion"));
        }

        [Test]
        public void GetUnassignedQuantity_EverythingAssigned_IsZero()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.AddUpTo(Potion(), 25);

            Assert.AreEqual(0, linked.GetUnassignedQuantity(Potion()));
        }

        [Test]
        public void GetUnassignedQuantity_NotInSourceOrNull_IsZero()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.AreEqual(0, linked.GetUnassignedQuantity(TestItems.Stackable("ether", 10)));
            Assert.AreEqual(0, linked.GetUnassignedQuantity((TestItem)null));
            Assert.AreEqual(0, linked.GetUnassignedQuantity((string)null));
        }

        [Test]
        public void GetUnassignedQuantity_NonStackable_IsOneUntilAssigned()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.AreEqual(1, linked.GetUnassignedQuantity(sword));
            linked.TryAdd(sword);
            Assert.AreEqual(0, linked.GetUnassignedQuantity(sword));
        }

        [Test]
        public void GetUnassignedQuantity_NonStackable_NotInSource_IsZero()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.AreEqual(0, linked.GetUnassignedQuantity(TestItems.NonStackable("sword")));
        }

        [Test]
        public void GetUnassignedQuantity_ByUniqueId_CountsUnassignedInstancesOfANonStackableKind()
        {
            var source = new Inventory<TestItem>();
            var swordA = AddSword(source);
            AddSword(source);
            AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(swordA);

            Assert.AreEqual(2, linked.GetUnassignedQuantity("sword"));
        }

        [Test]
        public void GetFreeSpaceFor_IsCappedByTheSource()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            Assert.AreEqual(25, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void GetFreeSpaceFor_IsCappedBySlotCapacity()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 1);
            Assert.AreEqual(10, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void GetFreeSpaceFor_CountsExistingPartialSlotsPlusEmptySlots()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(40), 3);
            linked.TryAdd(Potion(), 4);

            Assert.AreEqual(26, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void GetFreeSpaceFor_AllowDuplicatesFalse_IsTheSingleSlotsRemainingSpace()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), allowDuplicates: false);
            Assert.AreEqual(10, linked.GetFreeSpaceFor(Potion()));

            linked.TryAdd(Potion(), 4);
            Assert.AreEqual(6, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void GetFreeSpaceFor_NullOrMissingItem_IsZero()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.AreEqual(0, linked.GetFreeSpaceFor(null));
            Assert.AreEqual(0, linked.GetFreeSpaceFor(TestItems.Stackable("ether", 10)));
        }

        [Test]
        public void GetFreeSpaceFor_NonStackable_IsOneWhenASlotIsAvailable()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 1);
            Assert.AreEqual(1, linked.GetFreeSpaceFor(sword));

            linked.TryAdd(sword);
            Assert.AreEqual(0, linked.GetFreeSpaceFor(sword));
        }

        [Test]
        public void CanAdd_AgreesWithTryAdd()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            var free = linked.GetFreeSpaceFor(Potion());

            Assert.IsTrue(linked.CanAdd(Potion(), free));
            Assert.IsFalse(linked.CanAdd(Potion(), free + 1));
            Assert.IsFalse(linked.CanAdd(Potion(), 0));
            Assert.IsFalse(linked.CanAdd(Potion(), -1));
            Assert.IsFalse(linked.CanAdd(null));
        }

        [Test]
        public void CanAdd_NonStackableQuantityAboveOne_IsFalse()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsTrue(linked.CanAdd(sword));
            Assert.IsFalse(linked.CanAdd(sword, 2));
        }

        [Test]
        public void Counts_TrackAssignments()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 5);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(Potion(), 12);

            Assert.AreEqual(2, linked.FilledSlotCount);
            Assert.AreEqual(1, linked.FreeSlotCount);
            Assert.AreEqual(12, linked.TotalQuantity);
            Assert.IsFalse(linked.IsFull);

            linked.TryAdd(TestItems.Stackable("ether", 10));
            Assert.IsTrue(linked.IsFull);
        }
    }
}
