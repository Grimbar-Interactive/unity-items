using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryCollectionEventTests
    {
        private Inventory<BasicTestItem> _basic;
        private Inventory<UseTestItem> _use;
        private InventoryCollection _collection;
        private List<(Item Item, int Quantity)> _added;
        private List<(Item Item, int Quantity)> _removed;
        private int _updated;

        [SetUp]
        public void SetUp()
        {
            _basic = new Inventory<BasicTestItem>();
            _use = new Inventory<UseTestItem>();
            _collection = new InventoryCollection();
            _collection.Register(_basic);
            _collection.Register(_use);
            Subscribe(_collection);
        }

        private void Subscribe(InventoryCollection collection)
        {
            _added = new List<(Item, int)>();
            _removed = new List<(Item, int)>();
            _updated = 0;
            collection.OnItemAdded += (item, quantity) => _added.Add((item, quantity));
            collection.OnItemRemoved += (item, quantity) => _removed.Add((item, quantity));
            collection.OnCollectionUpdated += () => _updated++;
        }

        [Test]
        public void TryAdd_ForwardsItemAddedAndRaisesOneUpdate()
        {
            var potion = HierarchyItems.Use("potion", 10);

            _collection.TryAdd(potion, 4);

            Assert.AreEqual(1, _added.Count);
            Assert.AreSame(potion, _added[0].Item);
            Assert.AreEqual(4, _added[0].Quantity);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void TryRemove_ForwardsItemRemovedAndRaisesOneUpdate()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _collection.TryAdd(potion, 4);
            _added.Clear();
            _updated = 0;

            _collection.TryRemove(potion, 3);

            Assert.AreEqual(1, _removed.Count);
            Assert.AreEqual(3, _removed[0].Quantity);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void ChangesMadeDirectlyOnARegisteredInventory_AreForwarded()
        {
            var sword = HierarchyItems.Basic("sword");

            _basic.TryAdd(sword);
            _basic.TryRemoveInstance(sword);

            Assert.AreEqual(1, _added.Count);
            Assert.AreEqual(1, _removed.Count);
            Assert.AreEqual(2, _updated);
        }

        [Test]
        public void FailedAdd_RaisesNothing()
        {
            _collection.TryAdd(HierarchyItems.Other());
            _collection.TryAdd(HierarchyItems.Use("potion", 10), 0);

            Assert.AreEqual(0, _added.Count);
            Assert.AreEqual(0, _updated);
        }

        [Test]
        public void FallbackSpill_AcrossInventories_RaisesASingleUpdate()
        {
            var use = new Inventory<UseTestItem>(1);
            var basic = new Inventory<BasicTestItem>(1);
            var collection = new InventoryCollection(allowFallback: true);
            collection.Register(basic);
            collection.Register(use);
            Subscribe(collection);

            collection.TryAdd(HierarchyItems.Use("potion", 10), 15);

            Assert.AreEqual(2, _added.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void RemovalAcrossInventories_RaisesASingleUpdate()
        {
            var potion = HierarchyItems.Use("potion", 10);
            _use.TryAdd(potion, 3);
            _basic.TryAdd(potion, 4);
            _updated = 0;

            _collection.TryRemove(potion, 5);

            Assert.AreEqual(2, _removed.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void BeginBatchUpdate_CoalescesUpdatesUntilDisposed()
        {
            using (_collection.BeginBatchUpdate())
            {
                _collection.TryAdd(HierarchyItems.Use("a"));
                _collection.TryAdd(HierarchyItems.Basic("b"));

                Assert.AreEqual(0, _updated);
                Assert.AreEqual(0, _added.Count);
            }

            Assert.AreEqual(2, _added.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void BeginBatchUpdate_Nests_AndOnlyTheOutermostFlushes()
        {
            var outer = _collection.BeginBatchUpdate();
            var inner = _collection.BeginBatchUpdate();
            _collection.TryAdd(HierarchyItems.Use("a"));

            inner.Dispose();
            Assert.AreEqual(0, _updated);

            outer.Dispose();
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void BeginBatchUpdate_WithNoChanges_RaisesNothing()
        {
            using (_collection.BeginBatchUpdate())
            {
            }

            Assert.AreEqual(0, _updated);
        }

        [Test]
        public void BeginBatchUpdate_DisposedTwice_DoesNotSuppressLaterEvents()
        {
            var batch = _collection.BeginBatchUpdate();
            batch.Dispose();
            batch.Dispose();

            _collection.TryAdd(HierarchyItems.Use("a"));

            Assert.AreEqual(1, _added.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void AddRange_RaisesASingleUpdate()
        {
            _collection.AddRange(new Item[] { HierarchyItems.Use("a"), HierarchyItems.Basic("b") }, out _);

            Assert.AreEqual(2, _added.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void Unregister_StopsForwardingThatInventorysEvents()
        {
            _collection.Unregister<UseTestItem>();
            _updated = 0;

            _use.TryAdd(HierarchyItems.Use("a"));

            Assert.AreEqual(0, _added.Count);
            Assert.AreEqual(0, _updated);
        }

        [Test]
        public void Unregister_KeepsForwardingTheOtherInventories()
        {
            _collection.Unregister<UseTestItem>();
            _updated = 0;

            _basic.TryAdd(HierarchyItems.Basic("a"));

            Assert.AreEqual(1, _added.Count);
            Assert.AreEqual(1, _updated);
        }

        [Test]
        public void RegisterAndUnregister_RaiseAnUpdate()
        {
            var special = new Inventory<SpecialUseTestItem>();
            _updated = 0;

            _collection.Register(special);
            Assert.AreEqual(1, _updated);

            _collection.Unregister<SpecialUseTestItem>();
            Assert.AreEqual(2, _updated);
        }

        [Test]
        public void FailedRegister_RaisesNothing()
        {
            _updated = 0;

            _collection.TryRegister(new Inventory<UseTestItem>());

            Assert.AreEqual(0, _updated);
        }

        [Test]
        public void InventoryRegisteredWithTwoCollections_ForwardsToBoth()
        {
            var other = new InventoryCollection();
            other.Register(_use);
            var otherUpdates = 0;
            other.OnCollectionUpdated += () => otherUpdates++;

            _collection.TryAdd(HierarchyItems.Use("a"));

            Assert.AreEqual(1, _updated);
            Assert.AreEqual(1, otherUpdates);
        }

        [Test]
        public void ThrowingHandler_DoesNotLeaveBatchSuppressingEvents()
        {
            var shouldThrow = true;
            _collection.OnCollectionUpdated += () =>
            {
                if (shouldThrow) throw new InvalidOperationException("boom");
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (_collection.BeginBatchUpdate())
                {
                    _collection.TryAdd(HierarchyItems.Use("a"));
                }
            });

            shouldThrow = false;
            _updated = 0;
            _collection.TryAdd(HierarchyItems.Use("b"));

            Assert.AreEqual(1, _updated);
        }
    }
}
