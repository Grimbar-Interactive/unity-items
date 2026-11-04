using System;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventorySlotManipulationTests
    {
        private static Inventory<TestItem> PotionsAndEthers()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 10);
            return source;
        }

        [Test]
        public void Swap_ExchangesTwoSlots_AndKeepsTheSlotObjects()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 4);
            linked.TryAddAt(0, Potion(), 3);
            linked.TryAddAt(2, TestItems.Stackable("ether", 10), 5);
            var slot0 = linked[0];

            Assert.IsTrue(linked.Swap(0, 2));

            Assert.AreEqual("ether", linked[0].UniqueId);
            Assert.AreEqual(5, linked[0].Quantity);
            Assert.AreEqual("potion", linked[2].UniqueId);
            Assert.AreEqual(3, linked[2].Quantity);
            Assert.AreSame(slot0, linked[0]);
        }

        [Test]
        public void Swap_WithAnEmptySlot_MovesTheContents()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            linked.TryAddAt(0, Potion(), 3);

            Assert.IsTrue(linked.Swap(0, 1));
            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreEqual(3, linked[1].Quantity);
        }

        [Test]
        public void Swap_EqualIndicesOrBothEmpty_ReturnsFalse()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            linked.TryAddAt(0, Potion(), 3);

            Assert.IsFalse(linked.Swap(0, 0));
            Assert.IsFalse(linked.Swap(1, 2));
        }

        [Test]
        public void Swap_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.Swap(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.Swap(-1, 0));
        }

        [Test]
        public void Move_ToEmptySlot_RelocatesTheStack()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 4);
            linked.TryAddAt(0, Potion(), 4);

            Assert.IsTrue(linked.Move(0, 2));

            Assert.IsTrue(linked[0].IsEmpty);
            Assert.AreEqual(4, linked[2].Quantity);
        }

        [Test]
        public void Move_ToSameKindWithRoom_MergesAndLeavesTheRemainderBehind()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 4);
            linked.TryAddAt(0, Potion(), 4);
            linked.TryAddAt(1, Potion(), 9);

            Assert.IsTrue(linked.Move(0, 1));

            Assert.AreEqual(10, linked[1].Quantity);
            Assert.AreEqual(3, linked[0].Quantity);
        }

        [Test]
        public void Move_ToDifferentKind_Swaps()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            linked.TryAddAt(0, Potion(), 4);
            linked.TryAddAt(1, TestItems.Stackable("ether", 10), 2);

            Assert.IsTrue(linked.Move(0, 1));

            Assert.AreEqual("ether", linked[0].UniqueId);
            Assert.AreEqual("potion", linked[1].UniqueId);
        }

        [Test]
        public void Move_ToAFullSlotOfTheSameKind_Swaps()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            linked.TryAddAt(0, Potion(), 4);
            linked.TryAddAt(1, Potion(), 10);

            Assert.IsTrue(linked.Move(0, 1));

            Assert.AreEqual(10, linked[0].Quantity);
            Assert.AreEqual(4, linked[1].Quantity);
        }

        [Test]
        public void Move_UnboundedMergeThatEmptiesTheSource_RemovesTheSlot()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 7); // 5, 2
            linked.TryRemoveAt(0, 2); // 3, 2

            Assert.IsTrue(linked.Move(1, 0));

            Assert.AreEqual(1, linked.SlotCount);
            Assert.AreEqual(5, linked[0].Quantity);
        }

        [Test]
        public void Move_EqualIndicesOrEmptySource_ReturnsFalse()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            linked.TryAddAt(0, Potion(), 4);

            Assert.IsFalse(linked.Move(0, 0));
            Assert.IsFalse(linked.Move(2, 0));
        }

        [Test]
        public void Move_InvalidIndex_Throws()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => linked.Move(0, 3));
        }

        [Test]
        public void Consolidate_MergesPartialStacksAndFreesSlots()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), maxStackSize: 5);
            linked.TryAdd(Potion(), 12); // 5, 5, 2
            linked.TryRemoveAt(0, 2); // 3, 5, 2

            var freed = linked.Consolidate();

            Assert.AreEqual(1, freed);
            Assert.AreEqual(2, linked.SlotCount);
            Assert.AreEqual(5, linked[0].Quantity);
            Assert.AreEqual(5, linked[1].Quantity);
            Assert.AreEqual(10, linked.GetQuantity("potion"));
        }

        [Test]
        public void Consolidate_FixedLoadout_LeavesEmptySlotsInPlace()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), 4);
            linked.TryAddAt(0, Potion(), 4);
            linked.TryAddAt(2, Potion(), 4);

            Assert.AreEqual(1, linked.Consolidate());

            Assert.AreEqual(8, linked[0].Quantity);
            Assert.IsTrue(linked[2].IsEmpty);
            Assert.AreEqual(4, linked.SlotCount);
        }

        [Test]
        public void Consolidate_AllowDuplicatesFalse_DoesNothing()
        {
            var linked = new LinkedInventory<TestItem>(PotionsAndEthers(), allowDuplicates: false);
            linked.TryAdd(Potion(), 4);

            Assert.AreEqual(0, linked.Consolidate());
        }

        [Test]
        public void Consolidate_NeverMergesNonStackableItems()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source);
            var b = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(a);
            linked.TryAdd(b);

            Assert.AreEqual(0, linked.Consolidate());
            Assert.AreEqual(2, linked.SlotCount);
        }
    }
}
