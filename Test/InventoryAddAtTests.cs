using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAddAtTests
    {
        // ---- success paths ----

        [Test]
        public void TryAddAt_EmptyFixedSlot_PlacesAtExactIndex()
        {
            var inventory = new Inventory<TestItem>(5);
            var potion = TestItems.Stackable("potion", max: 10);

            Assert.IsTrue(inventory.TryAddAt(3, potion, 4));

            Assert.AreSame(potion, inventory[3].Item);
            Assert.AreEqual(4, inventory[3].Quantity);
            Assert.IsTrue(inventory[0].IsEmpty);
            Assert.IsTrue(inventory[4].IsEmpty);
        }

        [Test]
        public void TryAddAt_OccupiedMatchingStack_TopsItUp()
        {
            var inventory = new Inventory<TestItem>(5);
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 4);

            Assert.IsTrue(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 3));

            Assert.AreEqual(7, inventory[0].Quantity);
            Assert.AreEqual(1, inventory.FilledSlotCount);
        }

        [Test]
        public void TryAddAt_Unbounded_TopsUpExistingStack()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 4);

            Assert.IsTrue(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 2));
            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void TryAddAt_DefaultQuantityIsOne()
        {
            var inventory = new Inventory<TestItem>(2);

            Assert.IsTrue(inventory.TryAddAt(1, TestItems.NonStackable("sword")));
            Assert.AreEqual(1, inventory[1].Quantity);
        }

        [Test]
        public void TryAddAt_FillsAStackToExactlyItsLimit()
        {
            var inventory = new Inventory<TestItem>(2);

            Assert.IsTrue(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 10));
            Assert.IsTrue(inventory[0].IsFull);
        }

        [Test]
        public void TryAddAt_RestoresAFixedInventoryAtSavedPositions()
        {
            var attributes = new FakeItemAttributes("sword");
            var savedId = Guid.NewGuid();
            var inventory = new Inventory<TestItem>(6);

            inventory.TryAddAt(4, new TestItem(attributes, savedId));
            inventory.TryAddAt(1, TestItems.Stackable("potion", max: 10), 7);

            Assert.AreEqual(savedId, inventory[4].Item.InstanceId);
            Assert.AreEqual(4, inventory.IndexOfInstance(savedId));
            Assert.AreEqual(7, inventory[1].Quantity);
            Assert.AreEqual(2, inventory.FilledSlotCount);
        }

        // ---- events ----

        [Test]
        public void TryAddAt_Success_RaisesAddedSlotChangedAndUpdatedOnce()
        {
            var inventory = new Inventory<TestItem>(5);
            var potion = TestItems.Stackable("potion", max: 10);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            inventory.TryAddAt(3, potion, 4);

            Assert.AreEqual(1, recorder.AddedCount);
            Assert.AreSame(potion, recorder.Added[0].Item);
            Assert.AreEqual(4, recorder.Added[0].Quantity);
            CollectionAssert.AreEqual(new[] { 3 }, recorder.ChangedSlots);
            Assert.AreEqual(1, recorder.UpdatedCount);
            CollectionAssert.AreEqual(new[] { "Added", "SlotChanged", "Updated" }, recorder.Order);
        }

        [Test]
        public void TryAddAt_Failure_FiresNothing()
        {
            var inventory = new Inventory<TestItem>(5);
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 9);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 2));

            Assert.AreEqual(0, recorder.AddedCount);
            Assert.AreEqual(0, recorder.SlotChangedCount);
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        // ---- rejections ----

        [Test]
        public void TryAddAt_EmptySlot_QuantityAboveStackLimit_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 11));
            Assert.IsTrue(inventory[0].IsEmpty);
        }

        [Test]
        public void TryAddAt_NonStackableWithQuantityAboveOne_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.NonStackable("sword"), 2));
        }

        [Test]
        public void TryAddAt_TopUpPastStackLimit_RejectedAndLeavesStackUntouched()
        {
            var inventory = new Inventory<TestItem>(5);
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 8);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 3));
            Assert.AreEqual(8, inventory[0].Quantity);
        }

        [Test]
        public void TryAddAt_OccupiedSlotOfDifferentKind_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.NonStackable("axe")));
            Assert.AreSame(sword, inventory[0].Item);
        }

        [Test]
        public void TryAddAt_OccupiedSlotOfNonStackableKind_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);
            inventory.TryAdd(TestItems.NonStackable("sword"));

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.NonStackable("sword")));
        }

        [Test]
        public void TryAddAt_OccupiedSlot_ItemsThatRefuseToStack_Rejected()
        {
            var attributes = new FakeItemAttributes("blade", maxStackSize: 10);
            var inventory = new Inventory<DurabilityTestItem>(5);
            inventory.TryAdd(new DurabilityTestItem(attributes, 100), 2);

            Assert.IsFalse(inventory.TryAddAt(0, new DurabilityTestItem(attributes, 50), 1));
            Assert.AreEqual(2, inventory[0].Quantity);
        }

        [Test]
        public void TryAddAt_DuplicatesDisallowed_KindAlreadyHeldElsewhere_Rejected()
        {
            var inventory = new Inventory<TestItem>(5, allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 3);

            Assert.IsFalse(inventory.TryAddAt(3, TestItems.Stackable("potion", max: 10), 3));
            Assert.IsTrue(inventory[3].IsEmpty);
        }

        [Test]
        public void TryAddAt_DuplicatesDisallowed_TopUpOfTheSingleStack_Accepted()
        {
            var inventory = new Inventory<TestItem>(5, allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("potion", max: 10), 3);

            Assert.IsTrue(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), 3));
            Assert.AreEqual(6, inventory[0].Quantity);
        }

        [Test]
        public void TryAddAt_NonStackableInstanceAlreadyHeld_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);
            var sword = TestItems.NonStackable("sword");
            inventory.TryAdd(sword);

            Assert.IsFalse(inventory.TryAddAt(3, sword));
            Assert.IsTrue(inventory[3].IsEmpty);
        }

        [Test]
        public void TryAddAt_ItemRejectedByFilter_Rejected()
        {
            var inventory = new FilteredInventory(5);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.NonStackable("blocked")));
            Assert.IsTrue(inventory.TryAddAt(0, TestItems.NonStackable("allowed")));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryAddAt_NonPositiveQuantity_Rejected(int quantity)
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.IsFalse(inventory.TryAddAt(0, TestItems.Stackable("potion", max: 10), quantity));
        }

        [Test]
        public void TryAddAt_NullItem_Rejected()
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.IsFalse(inventory.TryAddAt(0, null));
        }

        [TestCase(-1)]
        [TestCase(5)]
        public void TryAddAt_InvalidIndex_Throws(int index)
        {
            var inventory = new Inventory<TestItem>(5);

            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryAddAt(index, TestItems.NonStackable("sword")));
        }

        [Test]
        public void TryAddAt_Unbounded_HasNoEmptySlotToTarget()
        {
            var inventory = new Inventory<TestItem>();

            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.TryAddAt(0, TestItems.NonStackable("sword")));
        }
    }
}
