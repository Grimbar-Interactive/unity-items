using System;
using System.Linq;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryCollectionQueryTests
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

        [Test]
        public void Items_EnumeratesEveryInventoryInRegistrationOrder()
        {
            var basic = HierarchyItems.Basic("b");
            var use = HierarchyItems.Use("u");
            _collection.TryAdd(use);
            _collection.TryAdd(basic);

            CollectionAssert.AreEqual(new Item[] { basic, use }, _collection.Items.ToList());
        }

        [Test]
        public void Items_OfEmptyCollection_IsEmpty()
        {
            Assert.IsEmpty(new InventoryCollection().Items.ToList());
            Assert.IsEmpty(_collection.Items.ToList());
        }

        [Test]
        public void IsEmpty_AndTotalQuantity_CoverEveryInventory()
        {
            Assert.IsTrue(_collection.IsEmpty);

            _collection.TryAdd(HierarchyItems.Use("potion", 10), 4);
            _collection.TryAdd(HierarchyItems.Basic("rock", 10), 2);

            Assert.IsFalse(_collection.IsEmpty);
            Assert.AreEqual(6, _collection.TotalQuantity);
        }

        [Test]
        public void Contains_ByUniqueId_SearchesEveryInventory()
        {
            _collection.TryAdd(HierarchyItems.Use("sword"));
            _collection.TryAdd(HierarchyItems.Basic("rock"));

            Assert.IsTrue(_collection.Contains("sword"));
            Assert.IsTrue(_collection.Contains("rock"));
            Assert.IsFalse(_collection.Contains("missing"));
            Assert.IsFalse(_collection.Contains((string)null));
            Assert.IsFalse(_collection.Contains(""));
        }

        [Test]
        public void Contains_ByPredicate_SearchesEveryInventory()
        {
            _collection.TryAdd(HierarchyItems.Use("sword"));
            _collection.TryAdd(HierarchyItems.Basic("rock"));

            Assert.IsTrue(_collection.Contains(i => i.UniqueId == "rock"));
            Assert.IsFalse(_collection.Contains(i => i.UniqueId == "missing"));
            Assert.Throws<ArgumentNullException>(() => _collection.Contains((Func<Item, bool>)null));
        }

        [Test]
        public void ContainsInstance_FindsItemInItsChain()
        {
            var sword = HierarchyItems.Use("sword");
            _collection.TryAdd(sword);

            Assert.IsTrue(_collection.ContainsInstance(sword));
            Assert.IsFalse(_collection.ContainsInstance(HierarchyItems.Use("sword")));
            Assert.IsFalse(_collection.ContainsInstance(null));
        }

        [Test]
        public void ContainsInstance_FindsItemHeldByLessSpecificInventory()
        {
            var sword = HierarchyItems.Use("sword");
            _basic.TryAdd(sword);

            Assert.IsTrue(_collection.ContainsInstance(sword));
        }

        [Test]
        public void GetQuantity_ByUniqueId_SumsEveryInventory()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _use.TryAdd(potion, 3);
            _basic.TryAdd(potion, 4);

            Assert.AreEqual(7, _collection.GetQuantity("potion"));
            Assert.AreEqual(0, _collection.GetQuantity("missing"));
            Assert.AreEqual(0, _collection.GetQuantity((string)null));
        }

        [Test]
        public void GetQuantity_ByItem_SumsTheItemsChain()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _use.TryAdd(potion, 3);
            _basic.TryAdd(potion, 4);

            Assert.AreEqual(7, _collection.GetQuantity(potion));
            Assert.AreEqual(0, _collection.GetQuantity((Item)null));
        }

        [Test]
        public void GetQuantity_ByPredicate_SumsMatchingStacks()
        {
            _collection.TryAdd(HierarchyItems.Use("potion", 10), 3);
            _collection.TryAdd(HierarchyItems.Basic("rock", 10), 4);

            Assert.AreEqual(3, _collection.GetQuantity(i => i is UseTestItem));
            Assert.AreEqual(7, _collection.GetQuantity(i => true));
            Assert.Throws<ArgumentNullException>(() => _collection.GetQuantity((Func<Item, bool>)null));
        }

        [Test]
        public void TryFind_ByPredicate_ReturnsFirstMatch()
        {
            var rock = HierarchyItems.Basic("rock");
            _collection.TryAdd(rock);
            _collection.TryAdd(HierarchyItems.Use("sword"));

            Assert.IsTrue(_collection.TryFind(i => i.UniqueId == "rock", out Item found));
            Assert.AreSame(rock, found);
            Assert.IsFalse(_collection.TryFind(i => i.UniqueId == "missing", out Item missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void TryFind_Typed_OnlyConsidersItemsOfThatType()
        {
            var rock = HierarchyItems.Basic("rock");
            var sword = HierarchyItems.Use("sword");
            _collection.TryAdd(rock);
            _collection.TryAdd(sword);

            Assert.IsTrue(_collection.TryFind<UseTestItem>(i => true, out var found));
            Assert.AreSame(sword, found);
            Assert.IsFalse(_collection.TryFind<OtherTestItem>(i => true, out var none));
            Assert.IsNull(none);
        }

        [Test]
        public void FindAll_Typed_FiltersByTypeAndPredicate()
        {
            var rock = HierarchyItems.Basic("rock");
            var sword = HierarchyItems.Use("sword");
            var axe = HierarchyItems.Use("axe");
            _collection.TryAdd(rock);
            _collection.TryAdd(sword);
            _collection.TryAdd(axe);

            CollectionAssert.AreEquivalent(new[] { sword, axe }, _collection.FindAll<UseTestItem>(i => true).ToList());
            CollectionAssert.AreEqual(new[] { axe }, _collection.FindAll<UseTestItem>(i => i.UniqueId == "axe").ToList());
        }

        [Test]
        public void FindAll_Untyped_FiltersByPredicate()
        {
            _collection.TryAdd(HierarchyItems.Basic("rock"));
            _collection.TryAdd(HierarchyItems.Use("sword"));

            var found = _collection.FindAll(i => i.UniqueId == "sword").ToList();

            Assert.AreEqual(1, found.Count);
            Assert.Throws<ArgumentNullException>(() => _collection.FindAll((Func<Item, bool>)null));
            Assert.Throws<ArgumentNullException>(() => _collection.FindAll<UseTestItem>(null));
        }

        [Test]
        public void TryFindByInstanceId_SearchesEveryInventory()
        {
            var sword = HierarchyItems.Use("sword");
            var rock = HierarchyItems.Basic("rock");
            _collection.TryAdd(sword);
            _collection.TryAdd(rock);

            Assert.IsTrue(_collection.TryFindByInstanceId(sword.InstanceId.Value, out var foundSword));
            Assert.AreSame(sword, foundSword);
            Assert.IsTrue(_collection.TryFindByInstanceId(rock.InstanceId.Value, out var foundRock));
            Assert.AreSame(rock, foundRock);
            Assert.IsFalse(_collection.TryFindByInstanceId(Guid.NewGuid(), out var missing));
            Assert.IsNull(missing);
        }

        [Test]
        public void Items_ThrowsWhenRegistrationsChangeDuringEnumeration()
        {
            _collection.TryAdd(HierarchyItems.Use("sword"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in _collection.Items)
                {
                    _collection.Unregister<BasicTestItem>();
                }
            });
        }

        [Test]
        public void Items_ThrowsWhenAnInventoryChangesDuringEnumeration()
        {
            _collection.TryAdd(HierarchyItems.Use("sword"));

            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var _ in _collection.Items)
                {
                    _collection.TryAdd(HierarchyItems.Use("axe"));
                }
            });
        }

        [Test]
        public void QueriesOnCollectionWithNoInventories_ReturnEmptyResults()
        {
            var collection = new InventoryCollection();

            Assert.IsFalse(collection.Contains("x"));
            Assert.AreEqual(0, collection.GetQuantity("x"));
            Assert.AreEqual(0, collection.TotalQuantity);
            Assert.IsFalse(collection.TryFind(i => true, out Item _));
            Assert.IsFalse(collection.TryFindByInstanceId(Guid.NewGuid(), out _));
        }
    }
}
