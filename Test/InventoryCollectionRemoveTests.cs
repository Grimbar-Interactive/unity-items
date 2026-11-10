using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryCollectionRemoveTests
    {
        private Inventory<BasicTestItem> _basic;
        private Inventory<UseTestItem> _use;
        private InventoryCollection _collection;

        [SetUp]
        public void SetUp()
        {
            _basic = new Inventory<BasicTestItem>();
            _use = new Inventory<UseTestItem>();
            _collection = new InventoryCollection();
            _collection.Register(_basic);
            _collection.Register(_use);
        }

        // Puts 3 potions in the specific inventory and 4 in the base inventory (added directly, the
        // way an earlier, less specific registration might have left them).
        private UseTestItem SplitPotions()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _use.TryAdd(potion, 3);
            _basic.TryAdd(potion, 4);
            return potion;
        }

        [Test]
        public void TryRemove_ByItem_DrainsMostSpecificInventoryFirst()
        {
            var potion = SplitPotions();

            Assert.IsTrue(_collection.TryRemove(potion, 5));

            Assert.AreEqual(0, _use.GetQuantity("potion"));
            Assert.AreEqual(2, _basic.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_ByItem_WithinMostSpecific_LeavesOthersAlone()
        {
            var potion = SplitPotions();

            Assert.IsTrue(_collection.TryRemove(potion, 2));

            Assert.AreEqual(1, _use.GetQuantity("potion"));
            Assert.AreEqual(4, _basic.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_ByItem_IsAtomicWhenNotEnoughIsHeld()
        {
            var potion = SplitPotions();

            Assert.IsFalse(_collection.TryRemove(potion, 8));

            Assert.AreEqual(3, _use.GetQuantity("potion"));
            Assert.AreEqual(4, _basic.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_ByItem_SearchesWholeChainEvenWithoutFallback()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _basic.TryAdd(potion, 4);

            Assert.IsFalse(_collection.AllowFallback);
            Assert.IsTrue(_collection.TryRemove(potion, 4));
            Assert.AreEqual(0, _basic.TotalQuantity);
        }

        [Test]
        public void TryRemove_ByItem_DoesNotReachInventoriesOutsideTheItemsChain()
        {
            // A Basic item's chain is just Inventory<Basic>; units of the same id held as Use items
            // in the Use inventory are not part of it.
            _use.TryAdd(HierarchyItems.Use("potion", 10), 3);

            Assert.IsFalse(_collection.TryRemove(HierarchyItems.Basic("potion", 10), 1));
            Assert.AreEqual(3, _use.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_ByUniqueId_DrawsFromEveryInventoryInRegistrationOrder()
        {
            SplitPotions();

            Assert.IsTrue(_collection.TryRemove("potion", 5));

            // _basic was registered first, so it is drained first.
            Assert.AreEqual(0, _basic.GetQuantity("potion"));
            Assert.AreEqual(2, _use.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_ByUniqueId_IsAtomicWhenNotEnoughIsHeld()
        {
            SplitPotions();

            Assert.IsFalse(_collection.TryRemove("potion", 8));

            Assert.AreEqual(3, _use.GetQuantity("potion"));
            Assert.AreEqual(4, _basic.GetQuantity("potion"));
        }

        [Test]
        public void TryRemove_InvalidArguments_ReturnFalse()
        {
            var potion = SplitPotions();

            Assert.IsFalse(_collection.TryRemove((Item)null, 1));
            Assert.IsFalse(_collection.TryRemove(potion, 0));
            Assert.IsFalse(_collection.TryRemove((string)null, 1));
            Assert.IsFalse(_collection.TryRemove("", 1));
            Assert.IsFalse(_collection.TryRemove("potion", -1));
            Assert.AreEqual(7, _collection.GetQuantity("potion"));
        }

        [Test]
        public void CanRemove_ReflectsHeldQuantity()
        {
            var potion = SplitPotions();

            Assert.IsTrue(_collection.CanRemove(potion, 7));
            Assert.IsFalse(_collection.CanRemove(potion, 8));
            Assert.IsTrue(_collection.CanRemove("potion", 7));
            Assert.IsFalse(_collection.CanRemove("potion", 8));
            Assert.IsFalse(_collection.CanRemove("missing"));
            Assert.IsFalse(_collection.CanRemove((Item)null));
        }

        [Test]
        public void RemoveUpTo_ByItem_RemovesAsMuchAsHeld()
        {
            var potion = SplitPotions();

            Assert.AreEqual(7, _collection.RemoveUpTo(potion, 20));

            Assert.IsTrue(_collection.IsEmpty);
        }

        [Test]
        public void RemoveUpTo_ByUniqueId_RemovesAsMuchAsHeld()
        {
            SplitPotions();

            Assert.AreEqual(6, _collection.RemoveUpTo("potion", 6));

            Assert.AreEqual(1, _collection.GetQuantity("potion"));
        }

        [Test]
        public void RemoveUpTo_NothingHeld_ReturnsZero()
        {
            Assert.AreEqual(0, _collection.RemoveUpTo("missing", 3));
            Assert.AreEqual(0, _collection.RemoveUpTo(HierarchyItems.Use("missing"), 3));
            Assert.AreEqual(0, _collection.RemoveUpTo((Item)null, 3));
        }

        [Test]
        public void TryRemoveInstance_FindsTheInstanceInTheInventoryHoldingIt()
        {
            var inUse = HierarchyItems.Use("sword");
            var inBasic = HierarchyItems.Use("axe");
            _use.TryAdd(inUse);
            _basic.TryAdd(inBasic);

            Assert.IsTrue(_collection.TryRemoveInstance(inBasic));
            Assert.AreEqual(0, _basic.TotalQuantity);
            Assert.AreEqual(1, _use.TotalQuantity);

            Assert.IsTrue(_collection.TryRemoveInstance(inUse));
            Assert.IsTrue(_collection.IsEmpty);
        }

        [Test]
        public void TryRemoveInstance_NotHeldStackableOrNull_ReturnsFalse()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _use.TryAdd(potion);

            Assert.IsFalse(_collection.TryRemoveInstance(HierarchyItems.Use("sword")));
            Assert.IsFalse(_collection.TryRemoveInstance(potion));
            Assert.IsFalse(_collection.TryRemoveInstance(null));
            Assert.AreEqual(1, _use.TotalQuantity);
        }

        [Test]
        public void TryRemove_FromUnboundedInventory_DropsEmptiedSlots()
        {
            var potion = SplitPotions();

            _collection.TryRemove(potion, 3);

            Assert.AreEqual(0, _use.SlotCount);
        }
    }
}
