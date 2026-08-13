using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAllowDuplicatesTests
    {
        [Test]
        public void AllowDuplicatesFalse_SecondNonStackableOfSameKind_IsRejected()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsFalse(inventory.TryAdd(TestItems.NonStackable("disc"), 1));
        }

        [Test]
        public void AllowDuplicatesFalse_DifferentUniqueIds_AreAccepted()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.IsTrue(inventory.TryAdd(TestItems.NonStackable("patch"), 1));
        }

        [Test]
        public void AllowDuplicatesFalse_StackableItem_FillsOneStackToStackLimit()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            Assert.IsTrue(inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10));
            Assert.AreEqual(1, inventory.SlotCount);
        }

        [Test]
        public void AllowDuplicatesFalse_StackableItem_OverflowBeyondStackLimit_IsRejected()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            Assert.IsFalse(inventory.TryAdd(TestItems.Stackable("disc", max: 10), 11));
        }

        [Test]
        public void AllowDuplicatesFalse_GetFreeSpaceFor_IsRemainingSpaceInExistingStack()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 4);

            Assert.AreEqual(6, inventory.GetFreeSpaceFor(TestItems.Stackable("disc", max: 10)));
        }

        [Test]
        public void AllowDuplicatesFalse_GetFreeSpaceFor_NoExistingStack_IsStackLimit()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            Assert.AreEqual(10, inventory.GetFreeSpaceFor(TestItems.Stackable("disc", max: 10)));
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public void AllowDuplicatesFalse_GetSlotCount_NeverExceedsOne(int quantity)
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), quantity);

            Assert.AreEqual(1, inventory.GetSlotCount("disc"));
        }

        [Test]
        public void AllowDuplicatesFalse_SplitStack_IsRejected()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 10);

            Assert.IsFalse(inventory.SplitStack(0, 4));
        }

        [Test]
        public void AllowDuplicatesFalse_Consolidate_IsNoOp()
        {
            var inventory = new Inventory<TestItem>(allowDuplicates: false);
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual(0, inventory.Consolidate());
        }

        [Test]
        public void AllowDuplicatesFalse_Swap_PreservesOneStackPerKind()
        {
            var inventory = new Inventory<TestItem>(2, allowDuplicates: false);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("patch"), 1);

            inventory.Swap(0, 1);

            Assert.AreEqual(1, inventory.GetSlotCount("disc"));
            Assert.AreEqual(1, inventory.GetSlotCount("patch"));
        }

        [Test]
        public void AllowDuplicatesTrue_SameKindNonStackable_ProducesMultipleSlots()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);
            inventory.TryAdd(TestItems.NonStackable("disc"), 1);

            Assert.AreEqual(2, inventory.GetSlotCount("disc"));
        }
    }
}
