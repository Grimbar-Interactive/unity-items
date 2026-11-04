using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryFilterTests
    {
        private static Inventory<TestItem> SourceWithEverything(out TestItem sword)
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("banned", 10), 5);
            sword = AddSword(source);
            return source;
        }

        private static FilteredLinkedInventory WeaponOnlySlotZero(Inventory<TestItem> source, int slots = 3)
        {
            return new FilteredLinkedInventory(source, slots)
            {
                RestrictedIndex = 0,
                RestrictedToId = "sword"
            };
        }

        // ---- CanAcceptItem --------------------------------------------------------------------

        [Test]
        public void CanAcceptItem_RejectedItem_CannotBeAddedAndHasNoFreeSpace()
        {
            var source = SourceWithEverything(out _);
            var linked = new FilteredLinkedInventory(source, 2);

            Assert.IsFalse(linked.TryAdd(TestItems.Stackable("banned", 10)));
            Assert.AreEqual(0, linked.GetFreeSpaceFor(TestItems.Stackable("banned", 10)));
            Assert.AreEqual(0, linked.AddUpTo(TestItems.Stackable("banned", 10), 10));
            Assert.IsFalse(linked.TryAddAt(0, TestItems.Stackable("banned", 10)));
            Assert.IsTrue(linked.IsEmpty);
        }

        [Test]
        public void CanAcceptItem_OtherItems_StillWork()
        {
            var source = SourceWithEverything(out var sword);
            var linked = new FilteredLinkedInventory(source);

            Assert.IsTrue(linked.TryAdd(Potion(), 3));
            Assert.IsTrue(linked.TryAdd(sword));
        }

        // ---- CanAcceptItemAt ------------------------------------------------------------------

        [Test]
        public void CanAcceptItemAt_AddSkipsTheRestrictedSlot()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out _));

            linked.TryAdd(Potion(), 2);

            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreEqual(2, linked[1].Quantity);
        }

        [Test]
        public void CanAcceptItemAt_AllowedItemMayUseTheRestrictedSlot()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));

            Assert.IsTrue(linked.TryAdd(sword));

            Assert.AreSame(sword, linked[0].Item);
        }

        [Test]
        public void CanAcceptItemAt_FreeSpaceExcludesSlotsThatRejectTheItem()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out _));

            Assert.AreEqual(20, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void CanAcceptItemAt_UnboundedRefusedAppendIndex_LimitsFreeSpaceAndRejectsTheAddAtomically()
        {
            // Slot 0 would take potions, but the next appended slot (index 1) is sword-only, so only
            // one stack of potions can ever be placed.
            var linked = new FilteredLinkedInventory(SourceWithEverything(out _))
            {
                RestrictedIndex = 1,
                RestrictedToId = "sword"
            };
            using var recorder = new InventoryEventRecorder<TestItem>(linked);

            Assert.AreEqual(10, linked.GetFreeSpaceFor(Potion()));
            Assert.IsFalse(linked.CanAdd(Potion(), 15));
            Assert.IsFalse(linked.TryAdd(Potion(), 15));
            Assert.IsTrue(linked.IsEmpty);
            Assert.AreEqual(0, recorder.UpdatedCount);

            Assert.AreEqual(10, linked.AddUpTo(Potion(), 99));
            Assert.AreEqual(10, linked.GetQuantity("potion"));
            Assert.AreEqual(1, recorder.Added.Count);
            Assert.AreEqual(10, recorder.Added[0].Quantity);
            Assert.AreEqual(0, linked.GetFreeSpaceFor(Potion()));
        }

        [Test]
        public void CanAcceptItemAt_TryAddAt_RejectsAForbiddenItemAndAcceptsAnAllowedOne()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));

            Assert.IsFalse(linked.TryAddAt(0, Potion()));
            Assert.IsTrue(linked.TryAddAt(0, sword));
        }

        [Test]
        public void CanAcceptItemAt_SwapThatWouldBreakTheRule_ChangesNothing()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));
            linked.TryAddAt(0, sword);
            linked.TryAddAt(1, Potion(), 3);

            Assert.IsFalse(linked.Swap(0, 1));

            Assert.AreSame(sword, linked[0].Item);
            Assert.AreEqual(3, linked[1].Quantity);
        }

        [Test]
        public void CanAcceptItemAt_MoveThatWouldBreakTheRule_ChangesNothing()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));
            linked.TryAddAt(0, sword);
            linked.TryAddAt(1, Potion(), 3);

            Assert.IsFalse(linked.Move(1, 0)); // potion into the weapon slot
            Assert.IsFalse(linked.Move(0, 1)); // would swap a potion into the weapon slot

            Assert.AreSame(sword, linked[0].Item);
            Assert.AreEqual(3, linked[1].Quantity);
        }

        [Test]
        public void CanAcceptItemAt_MoveToAnAllowedSlot_Works()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));
            linked.TryAddAt(0, sword);
            linked.TryAddAt(1, Potion(), 3);

            Assert.IsTrue(linked.Move(1, 2));

            Assert.IsTrue(linked[1].IsEmpty);
            Assert.AreEqual(3, linked[2].Quantity);
            Assert.AreSame(sword, linked[0].Item);
        }

        [Test]
        public void CanAcceptItemAt_CompactThatWouldBreakTheRule_ChangesNothing()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out _));
            linked.TryAddAt(1, Potion(), 3);

            Assert.IsFalse(linked.Compact());

            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreEqual(3, linked[1].Quantity);
        }

        [Test]
        public void CanAcceptItemAt_SortThatWouldBreakTheRule_ChangesNothing()
        {
            var linked = WeaponOnlySlotZero(SourceWithEverything(out var sword));
            linked.TryAddAt(0, sword);
            linked.TryAddAt(1, Potion(), 3);

            // Ascending by id puts "potion" ahead of "sword": potion would land in the weapon slot.
            Assert.IsFalse(linked.Sort((a, b) => string.CompareOrdinal(a.UniqueId, b.UniqueId)));

            Assert.AreSame(sword, linked[0].Item);
            Assert.AreEqual(3, linked[1].Quantity);
        }

        [Test]
        public void CanAcceptItemAt_TopUpOfAnExistingStack_IsNotBlocked()
        {
            var source = SourceWithPotions(25);
            var linked = new FilteredLinkedInventory(source, 2) { RestrictedIndex = 1, RestrictedToId = "potion" };
            linked.TryAddAt(1, Potion(), 3);

            linked.RestrictedToId = "nothing";

            Assert.IsTrue(linked.TryAddAt(1, Potion(), 2));
            Assert.AreEqual(5, linked[1].Quantity);
        }
    }
}
