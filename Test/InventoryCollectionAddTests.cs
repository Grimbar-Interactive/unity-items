using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryCollectionAddTests
    {
        private Inventory<BasicTestItem> _basic;
        private Inventory<UseTestItem> _use;

        [SetUp]
        public void SetUp()
        {
            _basic = new Inventory<BasicTestItem>();
            _use = new Inventory<UseTestItem>();
        }

        private InventoryCollection Both(bool allowFallback = false)
        {
            var collection = new InventoryCollection(allowFallback);
            collection.Register(_basic);
            collection.Register(_use);
            return collection;
        }

        [Test]
        public void TryAdd_UseItem_GoesToMostSpecificInventory()
        {
            var collection = Both();

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Use()));

            Assert.AreEqual(1, _use.TotalQuantity);
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_UseItem_ChoosesMostSpecificRegardlessOfRegistrationOrder()
        {
            var collection = new InventoryCollection();
            collection.Register(_use);
            collection.Register(_basic);

            collection.TryAdd(HierarchyItems.Use());

            Assert.AreEqual(1, _use.TotalQuantity);
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_BasicItem_GoesToBasicInventoryNotTheDerivedOne()
        {
            var collection = Both();

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Basic()));

            Assert.AreEqual(1, _basic.TotalQuantity);
            Assert.AreEqual(0, _use.TotalQuantity);
        }

        [Test]
        public void TryAdd_UseItem_WithOnlyBasicRegistered_GoesToBasic()
        {
            var collection = new InventoryCollection();
            collection.Register(_basic);

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Use()));

            Assert.AreEqual(1, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_SpecialItem_SkipsMissingLevelAndUsesNextMostSpecific()
        {
            var collection = Both();

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Special()));

            Assert.AreEqual(1, _use.TotalQuantity);
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_SpecialItem_WithSpecialInventoryRegistered_UsesIt()
        {
            var special = new Inventory<SpecialUseTestItem>();
            var collection = Both();
            collection.Register(special);

            collection.TryAdd(HierarchyItems.Special());

            Assert.AreEqual(1, special.TotalQuantity);
            Assert.AreEqual(0, _use.TotalQuantity);
        }

        [Test]
        public void TryAdd_ItemWithNoMatchingInventory_Fails()
        {
            var collection = Both();
            var other = HierarchyItems.Other();

            Assert.IsFalse(collection.CanAccept(other));
            Assert.IsFalse(collection.TryAdd(other));
            Assert.IsTrue(collection.IsEmpty);
        }

        [Test]
        public void TryAdd_BaseItemWhenOnlyDerivedInventoryRegistered_Fails()
        {
            var collection = new InventoryCollection();
            collection.Register(_use);

            Assert.IsFalse(collection.TryAdd(HierarchyItems.Basic()));
            Assert.AreEqual(0, _use.TotalQuantity);
        }

        [Test]
        public void TryAdd_NullItem_Fails()
        {
            var collection = Both();

            Assert.IsFalse(collection.CanAccept(null));
            Assert.IsFalse(collection.TryAdd(null));
            Assert.AreEqual(0, collection.GetFreeSpaceFor(null));
            Assert.AreEqual(0, collection.AddUpTo(null, 3));
        }

        [Test]
        public void TryAdd_NonPositiveQuantity_Fails()
        {
            var collection = Both();
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsFalse(collection.TryAdd(potion, 0));
            Assert.IsFalse(collection.TryAdd(potion, -1));
            Assert.AreEqual(0, collection.AddUpTo(potion, 0));
        }

        [Test]
        public void TryAdd_NonStackableWithQuantityAboveOne_Fails()
        {
            var collection = Both();

            Assert.IsFalse(collection.TryAdd(HierarchyItems.Use("sword"), 2));
            Assert.IsTrue(collection.IsEmpty);
        }

        [Test]
        public void TryAdd_Stackable_StacksInsideTheChosenInventory()
        {
            var collection = Both();
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsTrue(collection.TryAdd(potion, 4));
            Assert.IsTrue(collection.TryAdd(potion, 3));

            Assert.AreEqual(7, _use.GetQuantity("potion"));
            Assert.AreEqual(1, _use.FilledSlotCount);
        }

        [Test]
        public void TryAdd_WhenMostSpecificIsFull_AndFallbackOff_FailsWithoutTouchingBasic()
        {
            var use = new Inventory<UseTestItem>(1);
            use.TryAdd(HierarchyItems.Use("sword"));
            var collection = new InventoryCollection();
            collection.Register(_basic);
            collection.Register(use);

            Assert.IsFalse(collection.TryAdd(HierarchyItems.Use("axe")));

            Assert.AreEqual(0, _basic.TotalQuantity);
            Assert.AreEqual(1, use.TotalQuantity);
        }

        [Test]
        public void TryAdd_WhenMostSpecificRejectsItem_AndFallbackOff_Fails()
        {
            var collection = new InventoryCollection();
            collection.Register(_basic);
            collection.Register(new RejectingUseInventory());

            Assert.IsFalse(collection.TryAdd(HierarchyItems.Use()));
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_WhenMostSpecificIsFull_AndFallbackOn_UsesNextInventory()
        {
            var use = new Inventory<UseTestItem>(1);
            use.TryAdd(HierarchyItems.Use("sword"));
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(_basic);
            collection.Register(use);

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Use("axe")));

            Assert.AreEqual(1, _basic.TotalQuantity);
            Assert.AreEqual(1, use.TotalQuantity);
        }

        [Test]
        public void TryAdd_WhenMostSpecificRejectsItem_AndFallbackOn_UsesNextInventory()
        {
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(_basic);
            collection.Register(new RejectingUseInventory());

            Assert.IsTrue(collection.TryAdd(HierarchyItems.Use()));
            Assert.AreEqual(1, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_WithFallback_PrefersMostSpecificWhileItHasRoom()
        {
            var collection = Both(allowFallback: true);

            collection.TryAdd(HierarchyItems.Use());

            Assert.AreEqual(1, _use.TotalQuantity);
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_WithFallback_SplitsQuantityAcrossInventories()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(1);
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(basic);
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsTrue(collection.TryAdd(potion, 15));

            Assert.AreEqual(10, use.GetQuantity("potion"));
            Assert.AreEqual(5, basic.GetQuantity("potion"));
        }

        [Test]
        public void TryAdd_WithFallback_IsAtomicWhenCombinedSpaceIsTooSmall()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(1);
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(basic);
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsFalse(collection.TryAdd(potion, 25));

            Assert.AreEqual(0, use.TotalQuantity);
            Assert.AreEqual(0, basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_WithoutFallback_SplitIsNotAllowed()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(1);
            var collection = new InventoryCollection();
            collection.Register(basic);
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsFalse(collection.TryAdd(potion, 15));

            Assert.IsTrue(collection.IsEmpty);
        }

        [Test]
        public void GetFreeSpaceFor_SumsEligibleInventories()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(2);
            var potion = HierarchyItems.Use("potion", 10);

            var noFallback = new InventoryCollection();
            noFallback.Register(basic);
            noFallback.Register(use);
            var withFallback = new InventoryCollection(allowFallback: true);
            withFallback.Register(basic);
            withFallback.Register(use);

            Assert.AreEqual(10, noFallback.GetFreeSpaceFor(potion));
            Assert.AreEqual(30, withFallback.GetFreeSpaceFor(potion));
        }

        [Test]
        public void GetFreeSpaceFor_UnboundedInventory_IsIntMaxValueAndDoesNotOverflow()
        {
            var collection = Both(allowFallback: true);

            Assert.AreEqual(int.MaxValue, collection.GetFreeSpaceFor(HierarchyItems.Use("potion", 10)));
        }

        [Test]
        public void CanAdd_MatchesTryAdd()
        {
            var use = new Inventory<UseTestItem>(1);
            var collection = new InventoryCollection();
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.IsTrue(collection.CanAdd(potion, 10));
            Assert.IsFalse(collection.CanAdd(potion, 11));
            Assert.IsFalse(collection.TryAdd(potion, 11));
            Assert.IsTrue(collection.TryAdd(potion, 10));
        }

        [Test]
        public void AddUpTo_AddsAsMuchAsFits()
        {
            var use = new Inventory<UseTestItem>(1);
            var collection = new InventoryCollection();
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.AreEqual(10, collection.AddUpTo(potion, 25));
            Assert.AreEqual(10, use.TotalQuantity);
        }

        [Test]
        public void AddUpTo_WithFallback_SpillsRemainder()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(1);
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(basic);
            collection.Register(use);
            var potion = HierarchyItems.Use("potion", 10);

            Assert.AreEqual(20, collection.AddUpTo(potion, 25));
            Assert.AreEqual(10, use.TotalQuantity);
            Assert.AreEqual(10, basic.TotalQuantity);
        }

        [Test]
        public void AddUpTo_NonStackable_AddsAtMostOne()
        {
            var collection = Both();

            Assert.AreEqual(1, collection.AddUpTo(HierarchyItems.Use("sword"), 5));
            Assert.AreEqual(1, _use.TotalQuantity);
        }

        [Test]
        public void TryAdd_NonStackableInstanceAlreadyInSameInventory_IsRejected()
        {
            var collection = Both();
            var sword = HierarchyItems.Use("sword");

            Assert.IsTrue(collection.TryAdd(sword));
            Assert.IsFalse(collection.TryAdd(sword));

            Assert.AreEqual(1, _use.TotalQuantity);
        }

        [Test]
        public void TryAdd_NonStackableInstanceHeldByLessSpecificInventory_IsRejected()
        {
            var collection = Both(allowFallback: true);
            var sword = HierarchyItems.Use("sword");
            Assert.IsTrue(_basic.TryAdd(sword));

            Assert.IsFalse(collection.TryAdd(sword));
            Assert.AreEqual(0, collection.AddUpTo(sword, 1));

            Assert.AreEqual(0, _use.TotalQuantity);
            Assert.AreEqual(1, _basic.TotalQuantity);
        }

        [Test]
        public void TryAdd_NonStackableInstanceHeldByLessSpecificInventory_IsRejectedWithoutFallbackToo()
        {
            var collection = Both();
            var sword = HierarchyItems.Use("sword");
            Assert.IsTrue(_basic.TryAdd(sword));

            Assert.IsFalse(collection.TryAdd(sword));
            Assert.AreEqual(0, _use.TotalQuantity);
        }

        [Test]
        public void AddRange_AddsEachItemToItsOwnInventoryAndReportsFailures()
        {
            var collection = Both();
            var items = new List<Item>
            {
                HierarchyItems.Use("a"),
                HierarchyItems.Basic("b"),
                HierarchyItems.Other("c"),
                null,
            };

            var added = collection.AddRange(items, out var notAdded);

            Assert.AreEqual(2, added);
            Assert.AreEqual(1, _use.TotalQuantity);
            Assert.AreEqual(1, _basic.TotalQuantity);
            Assert.AreEqual(2, notAdded.Count);
            Assert.AreSame(items[2], notAdded[0]);
            Assert.IsNull(notAdded[1]);
        }

        [Test]
        public void AddRange_NullSequence_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Both().AddRange(null, out _));
        }

        [Test]
        public void TryAdd_AfterRegistrationChange_RoutesUsingTheNewRegistrations()
        {
            var collection = new InventoryCollection();
            collection.Register(_basic);
            collection.TryAdd(HierarchyItems.Use("first"));
            Assert.AreEqual(1, _basic.TotalQuantity);

            collection.Register(_use);
            collection.TryAdd(HierarchyItems.Use("second"));
            Assert.AreEqual(1, _use.TotalQuantity);

            collection.Unregister<UseTestItem>();
            collection.TryAdd(HierarchyItems.Use("third"));
            Assert.AreEqual(2, _basic.TotalQuantity);
        }
    }
}
