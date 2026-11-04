using NUnit.Framework;
using UnityEngine;

namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// A definition's stack limit can shrink after units were stacked against the old limit (for
    /// example a ScriptableObject edited in play mode). Over-full stacks must read as "no room",
    /// never as negative room that corrupts capacity maths elsewhere.
    /// </summary>
    public class InventoryShrunkStackLimitTests
    {
        private sealed class MutableAttributes : IItemAttributes
        {
            public string UniqueId { get; set; }
            public string Name => null;
            public string Description => null;
            public Sprite Icon => null;
            public int MaxStackSize { get; set; }
        }

        [Test]
        public void RemainingSpace_WhenLimitShrinksBelowQuantity_IsZeroNotNegative()
        {
            var attributes = new MutableAttributes { UniqueId = "potion", MaxStackSize = 10 };
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(new TestItem(attributes), 8);

            attributes.MaxStackSize = 5;

            Assert.AreEqual(0, inventory[0].RemainingSpace);
            Assert.IsTrue(inventory[0].IsFull);
        }

        [Test]
        public void GetFreeSpaceFor_WithOverfullStack_CountsOnlyGenuineFreeSpace()
        {
            var attributes = new MutableAttributes { UniqueId = "potion", MaxStackSize = 10 };
            var inventory = new Inventory<TestItem>(2);
            inventory.TryAdd(new TestItem(attributes), 8);

            attributes.MaxStackSize = 5;

            // The overfull stack contributes nothing; the single free slot holds 5.
            Assert.AreEqual(5, inventory.GetFreeSpaceFor(new TestItem(attributes)));
        }

        private static Inventory<TestItem> OverfullInventory()
        {
            var attributes = new MutableAttributes { UniqueId = "potion", MaxStackSize = 10 };
            var inventory = new Inventory<TestItem>(3);
            inventory.TryAddAt(1, new TestItem(attributes), 8);
            attributes.MaxStackSize = 5;
            return inventory;
        }

        [Test]
        public void Move_OverfullStackToEmptySlot_KeepsEveryUnit()
        {
            var inventory = OverfullInventory();

            inventory.Move(1, 2);

            Assert.AreEqual(8, inventory.TotalQuantity);
        }

        [Test]
        public void Compact_OverfullStack_KeepsEveryUnit()
        {
            var inventory = OverfullInventory();

            inventory.Compact();

            Assert.AreEqual(8, inventory.TotalQuantity);
            Assert.AreEqual(8, inventory[0].Quantity);
        }

        [Test]
        public void Sort_OverfullStack_KeepsEveryUnit()
        {
            var inventory = OverfullInventory();

            inventory.Sort((a, b) => 0);

            Assert.AreEqual(8, inventory.TotalQuantity);
        }

        [Test]
        public void SplitStack_AboveStackLimit_FailsWithoutChangingAnything()
        {
            var inventory = OverfullInventory();

            Assert.IsFalse(inventory.SplitStack(1, 6));
            Assert.IsFalse(inventory.SplitStack(1, 6, 2));

            Assert.AreEqual(8, inventory[1].Quantity);
            Assert.IsTrue(inventory[2].IsEmpty);
        }

        [Test]
        public void SplitStack_WithinStackLimit_StillSucceeds()
        {
            var inventory = OverfullInventory();

            Assert.IsTrue(inventory.SplitStack(1, 3));

            Assert.AreEqual(5, inventory[1].Quantity);
            Assert.AreEqual(3, inventory.TotalQuantity - inventory[1].Quantity);
        }
    }
}
