using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// Enumeration follows <see cref="System.Collections.Generic.List{T}"/>: mutating mid-iteration
    /// throws rather than silently skipping or repeating items.
    /// </summary>
    public class InventoryEnumerationSafetyTests
    {
        private static Inventory<TestItem> UnboundedWithThree()
        {
            var inventory = new Inventory<TestItem>();
            inventory.AddRange(TestItems.Many(3, "disc"), out _);
            return inventory;
        }

        [Test]
        public void Foreach_RemovingTheCurrentItem_Throws()
        {
            var inventory = UnboundedWithThree();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var item in inventory) inventory.TryRemoveInstance(item);
            });
        }

        [Test]
        public void Foreach_AddingAnItem_Throws()
        {
            var inventory = UnboundedWithThree();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in inventory) inventory.TryAdd(TestItems.NonStackable("extra"));
            });
        }

        [Test]
        public void Foreach_SwappingSlots_Throws()
        {
            var inventory = new Inventory<TestItem>(5);
            inventory.AddRange(TestItems.Many(2, "disc"), out _);

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in inventory) inventory.Swap(0, 1);
            });
        }

        [Test]
        public void Foreach_ClearingTheInventory_Throws()
        {
            var inventory = UnboundedWithThree();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in inventory) inventory.Clear();
            });
        }

        [Test]
        public void FindAll_MutatedWhileEnumerating_Throws()
        {
            var inventory = UnboundedWithThree();

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var item in inventory.FindAll(i => true)) inventory.TryRemoveInstance(item);
            });
        }

        [Test]
        public void Foreach_FailedMutation_DoesNotThrow()
        {
            var inventory = UnboundedWithThree();

            Assert.DoesNotThrow(() =>
            {
                foreach (var _ in inventory) inventory.TryRemove("missing", 1);
            });
        }

        [Test]
        public void Foreach_ReadOnlyQueries_DoNotThrow()
        {
            var inventory = UnboundedWithThree();

            Assert.DoesNotThrow(() =>
            {
                foreach (var item in inventory)
                {
                    inventory.ContainsInstance(item);
                    inventory.GetQuantity(item);
                    inventory.IndexOfInstance(item);
                }
            });
        }

        [Test]
        public void Mutating_AfterEnumerationHasFinished_IsFine()
        {
            var inventory = UnboundedWithThree();
            foreach (var _ in inventory) { }

            Assert.DoesNotThrow(() => inventory.TryAdd(TestItems.NonStackable("extra")));
        }

        [Test]
        public void CollectingFirst_ThenRemoving_RemovesEverything()
        {
            var inventory = UnboundedWithThree();

            foreach (var item in new System.Collections.Generic.List<TestItem>(inventory)) inventory.TryRemoveInstance(item);

            Assert.IsTrue(inventory.IsEmpty);
        }
    }
}
