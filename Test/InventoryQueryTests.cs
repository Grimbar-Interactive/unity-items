using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryQueryTests
    {
        [Test]
        public void Quantities_ClampToIntMaxValue_InsteadOfOverflowing()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: int.MaxValue), int.MaxValue);
            inventory.TryAdd(TestItems.Stackable("disc", max: int.MaxValue), 5);

            Assert.AreEqual(int.MaxValue, inventory.TotalQuantity);
            Assert.AreEqual(int.MaxValue, inventory.GetQuantity("disc"));
            Assert.AreEqual(int.MaxValue, inventory.GetQuantity(_ => true));
        }

        [Test]
        public void Contains_ByUniqueId_TrueWhenHeld()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsTrue(inventory.Contains("disc"));
        }

        [Test]
        public void Contains_ByUniqueId_FalseWhenAbsent()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.Contains("disc"));
        }

        [Test]
        public void Contains_ByInstance_TrueOnlyForThatReference()
        {
            var inventory = new Inventory<TestItem>();
            var a = TestItems.NonStackable("disc");
            var b = TestItems.NonStackable("disc");
            inventory.TryAdd(a, 1);

            Assert.IsTrue(inventory.Contains(a));
            Assert.IsFalse(inventory.Contains(b));
        }

        [Test]
        public void Contains_ByPredicate_Works()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsTrue(inventory.Contains(i => i.UniqueId == "disc"));
        }

        [Test]
        public void GetQuantity_ByUniqueId_SumsAcrossStacks()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(2, inventory.GetQuantity("disc"));
        }

        [Test]
        public void GetQuantity_ByUnknownUniqueId_IsZero()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(0, inventory.GetQuantity("disc"));
        }

        [Test]
        public void GetQuantity_ByItem_MatchesOnUniqueId()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual(5, inventory.GetQuantity(TestItems.NonStackable("disc")));
        }

        [Test]
        public void GetQuantity_ByPredicate_SumsMatchingStacks()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 3);

            Assert.AreEqual(8, inventory.GetQuantity(i => true));
        }

        [Test]
        public void GetSlotCount_CountsOccupiedSlotsForKind()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(2, inventory.GetSlotCount("disc"));
        }

        [Test]
        public void GetSlotCount_IgnoresEmptySlots()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(1, inventory.GetSlotCount("disc"));
        }

        [Test]
        public void GetQuantity_OnFixedInventoryWithEmptySlots_DoesNotThrow()
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.DoesNotThrow(() => inventory.GetQuantity("disc"));
        }

        [Test]
        public void Contains_OnFixedInventoryWithEmptySlots_DoesNotThrow()
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.DoesNotThrow(() => inventory.Contains("disc"));
        }

        [Test]
        public void GetSlotCount_OnFixedInventoryWithEmptySlots_DoesNotThrow()
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.DoesNotThrow(() => inventory.GetSlotCount("disc"));
        }

        [Test]
        public void TryRemove_OnFixedInventoryWithEmptySlots_DoesNotThrow()
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.DoesNotThrow(() => inventory.TryRemove("disc", 1, out _));
        }

        [Test]
        public void FilledSlotCount_IgnoresEmptySlots()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(1, inventory.FilledSlotCount);
        }

        [TestCase(0, 3)]
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void FreeSlotCount_Fixed_DecrementsAsSlotsFill(int itemsAdded, int expectedFree)
        {
            var inventory = new Inventory<TestItem>(3);
            for (var i = 0; i < itemsAdded; i++) inventory.TryAdd(TestItems.NonStackable($"item{i}"), 1);

            Assert.AreEqual(expectedFree, inventory.FreeSlotCount);
        }

        [Test]
        public void TotalQuantity_SumsEveryStack()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            inventory.TryAdd(TestItems.Stackable("patch", max: 10), 3);

            Assert.AreEqual(8, inventory.TotalQuantity);
        }

        [Test]
        public void TryFind_ReturnsFirstMatchInSlotOrder()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            inventory.TryFind(i => true, out var found);

            Assert.AreEqual("a", found.UniqueId);
        }

        [Test]
        public void TryFind_NoMatch_ReturnsFalseWithNullItem()
        {
            var inventory = new Inventory<TestItem>();
            var result = inventory.TryFind(i => false, out var item);

            Assert.IsFalse(result);
            Assert.IsNull(item);
        }

        [Test]
        public void FindAll_ReturnsAllMatchesInSlotOrder()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("b"), 1);

            var results = new System.Collections.Generic.List<TestItem>(inventory.FindAll(i => true));

            Assert.AreEqual("a", results[0].UniqueId);
            Assert.AreEqual("b", results[1].UniqueId);
        }

        [Test]
        public void FindAll_SkipsEmptySlots()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            var results = new System.Collections.Generic.List<TestItem>(inventory.FindAll(i => true));

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void FindAll_NoMatch_IsEmptySequence()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);

            var results = new System.Collections.Generic.List<TestItem>(inventory.FindAll(i => false));

            Assert.IsEmpty(results);
        }

        [Test]
        public void TryFindByInstanceId_FindsThatExactInstance()
        {
            var inventory = new Inventory<TestItem>();
            var item = TestItems.NonStackable("disc");
            inventory.TryAdd(item, 1);

            var result = inventory.TryFindByInstanceId(item.InstanceId, out var found);

            Assert.IsTrue(result);
            Assert.AreSame(item, found);
        }

        [Test]
        public void TryFindByInstanceId_UnknownId_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            Assert.IsFalse(inventory.TryFindByInstanceId(Guid.NewGuid(), out _));
        }

        [Test]
        public void IndexOfInstance_ReturnsSlotIndex()
        {
            var inventory = new Inventory<TestItem>();
            var item = TestItems.NonStackable("disc");
            inventory.TryAdd(item, 1);

            Assert.AreEqual(0, inventory.IndexOfInstance(item.InstanceId));
        }

        [Test]
        public void IndexOf_ByInstance_ReturnsSlotIndex()
        {
            var inventory = new Inventory<TestItem>();
            var item = TestItems.NonStackable("disc");
            inventory.TryAdd(item, 1);

            Assert.AreEqual(0, inventory.IndexOf(item));
        }

        [Test]
        public void IndexOf_ByInstance_NotPresent_ReturnsMinusOne()
        {
            var inventory = new Inventory<TestItem>();
            Assert.AreEqual(-1, inventory.IndexOf(TestItems.NonStackable("disc")));
        }

        [Test]
        public void IndexOf_ByUniqueId_ReturnsLowestMatchingIndex()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(1, inventory.IndexOf("disc"));
        }

        [Test]
        public void IndexOf_ByUniqueId_SkipsEmptySlots()
        {
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.Swap(0, 2);

            Assert.AreEqual(2, inventory.IndexOf("disc"));
        }

        [Test]
        public void TryGetSlotIndex_ByPredicate_Works()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("a"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            var result = inventory.TryGetSlotIndex(i => i.UniqueId == "disc", out var index);

            Assert.IsTrue(result);
            Assert.AreEqual(1, index);
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void IsValidIndex_BoundaryCases(int index, bool expected)
        {
            var inventory = new Inventory<TestItem>(3);
            Assert.AreEqual(expected, inventory.IsValidIndex(index));
        }
    }
}
