using System;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryAddTests
    {
        // ---- stackable items ------------------------------------------------------------------

        [Test]
        public void Stackable_Add_ClaimsUnitsWithoutRemovingThemFromTheSource()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsTrue(linked.TryAdd(Potion(), 4));

            Assert.AreEqual(4, linked.GetQuantity("potion"));
            Assert.AreEqual(25, source.GetQuantity("potion"));
        }

        [Test]
        public void Stackable_Add_OverflowsIntoNewSlotAtTheStackLimit()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.IsTrue(linked.TryAdd(Potion(), 15));

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(10, linked[0].Quantity);
            Assert.AreEqual(5, linked[1].Quantity);
        }

        [Test]
        public void Stackable_Add_FillsExistingSlotBeforeStartingANewOne()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 4);

            linked.TryAdd(Potion(), 8);

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(10, linked[0].Quantity);
            Assert.AreEqual(2, linked[1].Quantity);
        }

        [Test]
        public void Stackable_Add_MoreThanTheSourceHolds_IsRejectedAtomically()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 3);

            Assert.IsFalse(linked.TryAdd(Potion(), 23));
            Assert.AreEqual(3, linked.GetQuantity("potion"));
            Assert.AreEqual(1, linked.SlotCount);
        }

        [Test]
        public void Stackable_Add_ExactlyTheRemainingSourceUnits_Succeeds()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 3);

            Assert.IsTrue(linked.TryAdd(Potion(), 22));
            Assert.AreEqual(25, linked.GetQuantity("potion"));
        }

        [Test]
        public void Stackable_Add_KindMissingFromSource_IsRejected()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            Assert.IsFalse(linked.TryAdd(TestItems.Stackable("ether", 10)));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Add_NonPositiveQuantity_IsRejected(int quantity)
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            Assert.IsFalse(linked.TryAdd(Potion(), quantity));
        }

        [Test]
        public void Add_NullItem_IsRejected()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            Assert.IsFalse(linked.TryAdd((TestItem)null));
        }

        [Test]
        public void Add_FailureReportsFullQuantityAsRemainder_SuccessReportsZero()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(5));

            Assert.IsFalse(linked.TryAdd(Potion(), 9, out var failedRemainder));
            Assert.AreEqual(9, failedRemainder);

            Assert.IsTrue(linked.TryAdd(Potion(), 4, out var okRemainder));
            Assert.AreEqual(0, okRemainder);
        }

        // ---- AllowDuplicates -------------------------------------------------------------------

        [Test]
        public void AllowDuplicatesFalse_Stackable_FillsOneSlotToTheLimit()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), allowDuplicates: false);

            Assert.IsTrue(linked.TryAdd(Potion(), 10));
            Assert.AreEqual(1, linked.SlotCount);
        }

        [Test]
        public void AllowDuplicatesFalse_Stackable_NeverOverflowsIntoSecondSlot()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), allowDuplicates: false);

            Assert.IsFalse(linked.TryAdd(Potion(), 11));
            linked.TryAdd(Potion(), 10);
            Assert.IsFalse(linked.TryAdd(Potion(), 1));
            Assert.AreEqual(1, linked.GetSlotCount("potion"));
        }

        [Test]
        public void AllowDuplicatesFalse_Stackable_TopsUpTheSingleSlot()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), allowDuplicates: false);
            linked.TryAdd(Potion(), 4);

            Assert.IsTrue(linked.TryAdd(Potion(), 6));
            Assert.AreEqual(10, linked[0].Quantity);
        }

        [Test]
        public void AllowDuplicatesFalse_DifferentKinds_AreAccepted()
        {
            var source = SourceWithPotions(5);
            source.TryAdd(TestItems.Stackable("ether", 10), 5);
            var linked = new LinkedInventory<TestItem>(source, allowDuplicates: false);

            Assert.IsTrue(linked.TryAdd(Potion()));
            Assert.IsTrue(linked.TryAdd(TestItems.Stackable("ether", 10)));
        }

        // ---- stack size override / resolver ---------------------------------------------------

        [Test]
        public void MaxStackSizeOverride_AboveTheItemsLimit_LetsASlotHoldMore()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 20);

            Assert.IsTrue(linked.TryAdd(Potion(), 20));

            Assert.AreEqual(1, linked.SlotCount);
            Assert.AreEqual(20, linked[0].Quantity);
            Assert.AreEqual(20, linked[0].MaxStackSize);
            Assert.IsTrue(linked[0].IsFull);
        }

        [Test]
        public void MaxStackSizeOverride_BelowTheItemsLimit_SplitsAcrossMoreSlots()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 3);

            Assert.IsTrue(linked.TryAdd(Potion(), 7));

            Assert.AreEqual(3, linked.SlotCount);
            Assert.AreEqual(3, linked[0].Quantity);
            Assert.AreEqual(3, linked[1].Quantity);
            Assert.AreEqual(1, linked[2].Quantity);
        }

        [Test]
        public void StackSizeResolver_AppliesPerItem()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 10);
            var linked = new LinkedInventory<TestItem>(source, null, true, item => item.UniqueId == "potion" ? 4 : 99);

            linked.TryAdd(Potion(), 9);
            linked.TryAdd(TestItems.Stackable("ether", 10), 10);

            Assert.AreEqual(4, linked[0].Quantity);
            Assert.AreEqual(4, linked[1].Quantity);
            Assert.AreEqual(1, linked[2].Quantity);
            Assert.AreEqual(10, linked[3].Quantity);
        }

        // ---- non-stackable items ---------------------------------------------------------------

        [Test]
        public void NonStackable_TwoInstancesOfTheSameKind_TakeTwoSlots()
        {
            var source = new Inventory<TestItem>();
            var swordA = AddSword(source);
            var swordB = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsTrue(linked.TryAdd(swordA));
            Assert.IsTrue(linked.TryAdd(swordB));

            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(2, linked.GetSlotCount("sword"));
            Assert.AreSame(swordA, linked[0].Item);
            Assert.AreSame(swordB, linked[1].Item);
        }

        [Test]
        public void NonStackable_TheSameInstanceTwice_IsRejected()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(sword);

            Assert.IsFalse(linked.TryAdd(sword));
            Assert.AreEqual(1, linked.SlotCount);
        }

        [Test]
        public void NonStackable_AllowDuplicatesFalse_SecondInstanceOfTheKindIsRejected()
        {
            var source = new Inventory<TestItem>();
            var swordA = AddSword(source);
            var swordB = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, allowDuplicates: false);

            Assert.IsTrue(linked.TryAdd(swordA));
            Assert.IsFalse(linked.TryAdd(swordB));
        }

        [Test]
        public void NonStackable_QuantityAboveOne_IsRejected()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsFalse(linked.TryAdd(sword, 2));
        }

        [Test]
        public void NonStackable_NotInTheSource_IsRejected()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.IsFalse(linked.TryAdd(TestItems.NonStackable("sword")));
        }

        [Test]
        public void NonStackable_IsMatchedByInstanceIdNotObjectReference()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var lookalike = new TestItem(new FakeItemAttributes("sword"), sword.InstanceId.Value);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsTrue(linked.TryAdd(lookalike));
            Assert.AreSame(sword, linked[0].Item);
        }

        [Test]
        public void NonStackable_SameKindButDifferentInstanceNotInSource_IsRejected()
        {
            var source = new Inventory<TestItem>();
            AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.IsFalse(linked.TryAdd(TestItems.NonStackable("sword")));
        }

        [Test]
        public void Stackable_StoresTheSourcesOwnObject_NotTheCallers()
        {
            var source = SourceWithPotions(25);
            var linked = new LinkedInventory<TestItem>(source);

            linked.TryAdd(Potion());

            Assert.AreSame(source.Slots[0].Item, linked[0].Item);
        }

        // ---- fixed-size ------------------------------------------------------------------------

        [Test]
        public void Fixed_Add_UsesTheLowestEmptySlot()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);

            linked.TryAdd(Potion(), 2);

            Assert.AreEqual(2, linked[0].Quantity);
            Assert.IsTrue(linked[1].IsEmpty);
            Assert.AreEqual(3, linked.SlotCount);
        }

        [Test]
        public void Fixed_Full_RejectsNewKinds_ButStillTopsUpExistingStacks()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 10);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 2);
            linked.TryAdd(Potion());
            linked.TryAdd(TestItems.Stackable("ether", 10));

            Assert.IsTrue(linked.IsFull);
            Assert.IsFalse(linked.TryAdd(sword));
            Assert.IsTrue(linked.TryAdd(Potion()));
            Assert.AreEqual(2, linked[0].Quantity);
        }

        [Test]
        public void Fixed_Add_OverflowBeyondFreeSlots_IsRejectedAtomically()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);

            Assert.IsFalse(linked.TryAdd(Potion(), 21));
            Assert.IsTrue(linked.IsEmpty);
        }

        // ---- AddUpTo ---------------------------------------------------------------------------

        [Test]
        public void AddUpTo_ReturnsHowManyFit()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);

            Assert.AreEqual(20, linked.AddUpTo(Potion(), 99));
            Assert.AreEqual(20, linked.GetQuantity("potion"));
        }

        [Test]
        public void AddUpTo_NonStackable_AddsAtMostOne()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.AreEqual(1, linked.AddUpTo(sword, 5));
        }

        [Test]
        public void AddUpTo_InvalidArguments_ReturnZero()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.AreEqual(0, linked.AddUpTo(null, 3));
            Assert.AreEqual(0, linked.AddUpTo(Potion(), 0));
        }

        [Test]
        public void AddUpTo_Unbounded_TakesEverythingTheSourceHolds()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.AreEqual(25, linked.AddUpTo(Potion(), 99));
            Assert.AreEqual(3, linked.SlotCount);
            Assert.AreEqual(5, linked[2].Quantity);
        }

        [Test]
        public void AddUpTo_OverrideAboveItemLimit_IsCappedByTheSourceNotTheItemLimit()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 1, maxStackSize: 20);
            Assert.AreEqual(20, linked.AddUpTo(Potion(), 99));

            var roomy = new LinkedInventory<TestItem>(SourceWithPotions(25), 1, maxStackSize: 99);
            Assert.AreEqual(25, roomy.AddUpTo(Potion(), 99));
        }

        [Test]
        public void AddUpTo_AfterPartialAssignment_AddsOnlyTheUnassignedRemainder()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 5);

            Assert.AreEqual(20, linked.AddUpTo(Potion(), 99));
            Assert.AreEqual(25, linked.GetQuantity("potion"));
            Assert.AreEqual(0, linked.AddUpTo(Potion(), 99));
        }

        [Test]
        public void AddUpTo_AllowDuplicatesFalse_FillsTheSingleSlotOnly()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), allowDuplicates: false);

            Assert.AreEqual(10, linked.AddUpTo(Potion(), 99));
            Assert.AreEqual(1, linked.SlotCount);
        }

        [Test]
        public void AddUpTo_NonStackable_AddsOneThenNothing()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);

            Assert.AreEqual(1, linked.AddUpTo(sword, 5));
            Assert.AreEqual(0, linked.AddUpTo(sword, 5));
        }

        [Test]
        public void AddUpTo_ItemNotInSource_AddsNothing()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.AreEqual(0, linked.AddUpTo(Potion(), 99));
        }

        // ---- TryAddAt --------------------------------------------------------------------------

        [Test]
        public void TryAddAt_EmptySlot_PlacesExactlyThere()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);

            Assert.IsTrue(linked.TryAddAt(1, Potion(), 4));

            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreEqual(4, linked[1].Quantity);
            Assert.IsTrue(linked[2].IsEmpty);
        }

        [Test]
        public void TryAddAt_OccupiedSlotOfSameKind_TopsUpOnlyIfItFits()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);
            linked.TryAddAt(1, Potion(), 4);

            Assert.IsFalse(linked.TryAddAt(1, Potion(), 7));
            Assert.IsTrue(linked.TryAddAt(1, Potion(), 6));
            Assert.AreEqual(10, linked[1].Quantity);
            Assert.IsFalse(linked.TryAddAt(1, Potion(), 1));
        }

        [Test]
        public void TryAddAt_OccupiedSlotOfDifferentKind_IsRejected()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 10);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAddAt(1, Potion(), 4);

            Assert.IsFalse(linked.TryAddAt(1, TestItems.Stackable("ether", 10)));
        }

        [Test]
        public void TryAddAt_QuantityAboveStackLimit_IsRejected()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3);
            Assert.IsFalse(linked.TryAddAt(2, Potion(), 11));
        }

        [Test]
        public void TryAddAt_AllowDuplicatesFalse_RejectsAnotherSlotOfTheSameKind()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 3, allowDuplicates: false);
            linked.TryAddAt(0, Potion(), 2);

            Assert.IsFalse(linked.TryAddAt(2, Potion(), 2));
        }

        [Test]
        public void TryAddAt_NonStackableInstanceAlreadyAssigned_IsRejected()
        {
            var source = new Inventory<TestItem>();
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAddAt(0, sword);

            Assert.IsFalse(linked.TryAddAt(2, sword));
        }

        [Test]
        public void TryAddAt_Unbounded_OnlyTopsUpExistingSlots()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryAddAt(0, Potion()));

            linked.TryAdd(Potion(), 4);
            Assert.IsTrue(linked.TryAddAt(0, Potion(), 2));
            Assert.AreEqual(6, linked[0].Quantity);
        }

        [Test]
        public void TryAddAt_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryAddAt(2, Potion()));
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.TryAddAt(-1, Potion()));
        }
    }
}
