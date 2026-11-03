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
            public bool IsStackable => true;
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
    }
}
