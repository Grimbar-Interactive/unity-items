using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryCollectionRegistrationTests
    {
        [Test]
        public void NewCollection_IsEmpty()
        {
            var collection = new InventoryCollection();

            Assert.AreEqual(0, collection.Count);
            Assert.AreEqual(0, collection.RegisteredItemTypes.Count);
            Assert.IsTrue(collection.IsEmpty);
            Assert.IsFalse(collection.AllowFallback);
        }

        [Test]
        public void AllowFallback_ReflectsConstructorArgument()
        {
            Assert.IsTrue(new InventoryCollection(allowFallback: true).AllowFallback);
        }

        [Test]
        public void Register_ThenGetInventory_ReturnsSameInstance()
        {
            var collection = new InventoryCollection();
            var use = new Inventory<UseTestItem>();

            collection.Register(use);

            Assert.AreSame(use, collection.GetInventory<UseTestItem>());
            Assert.IsTrue(collection.TryGetInventory<UseTestItem>(out var found));
            Assert.AreSame(use, found);
            Assert.IsTrue(collection.IsRegistered<UseTestItem>());
            Assert.IsTrue(collection.IsRegistered(typeof(UseTestItem)));
            Assert.AreEqual(1, collection.Count);
        }

        [Test]
        public void Register_SecondInventoryForSameType_Throws()
        {
            var collection = new InventoryCollection();
            var first = new Inventory<UseTestItem>();
            collection.Register(first);

            Assert.Throws<ArgumentException>(() => collection.Register(new Inventory<UseTestItem>()));
            Assert.AreSame(first, collection.GetInventory<UseTestItem>());
        }

        [Test]
        public void TryRegister_SecondInventoryForSameType_ReturnsFalseAndKeepsFirst()
        {
            var collection = new InventoryCollection();
            var first = new Inventory<UseTestItem>();
            collection.Register(first);

            Assert.IsFalse(collection.TryRegister(new Inventory<UseTestItem>()));
            Assert.AreSame(first, collection.GetInventory<UseTestItem>());
            Assert.AreEqual(1, collection.Count);
        }

        [Test]
        public void Register_BaseAndDerivedTypes_Coexist()
        {
            var collection = new InventoryCollection();

            collection.Register(new Inventory<BasicTestItem>());
            collection.Register(new Inventory<UseTestItem>());

            Assert.AreEqual(2, collection.Count);
            Assert.IsTrue(collection.IsRegistered<BasicTestItem>());
            Assert.IsTrue(collection.IsRegistered<UseTestItem>());
            Assert.IsFalse(collection.IsRegistered<SpecialUseTestItem>());
        }

        [Test]
        public void Register_InventorySubclass_IsKeyedByItemType()
        {
            var collection = new InventoryCollection();
            var rejecting = new RejectingUseInventory();

            collection.Register(rejecting);

            Assert.AreSame(rejecting, collection.GetInventory<UseTestItem>());
        }

        [Test]
        public void RegisteredItemTypes_AreInRegistrationOrder()
        {
            var collection = new InventoryCollection();

            collection.Register(new Inventory<UseTestItem>());
            collection.Register(new Inventory<BasicTestItem>());
            collection.Register(new Inventory<OtherTestItem>());

            CollectionAssert.AreEqual(
                new[] { typeof(UseTestItem), typeof(BasicTestItem), typeof(OtherTestItem) },
                collection.RegisteredItemTypes);
        }

        [Test]
        public void Unregister_RemovesInventoryButLeavesItsContents()
        {
            var collection = new InventoryCollection();
            var use = new Inventory<UseTestItem>();
            collection.Register(use);
            collection.TryAdd(HierarchyItems.Use());

            Assert.IsTrue(collection.Unregister<UseTestItem>());

            Assert.IsFalse(collection.IsRegistered<UseTestItem>());
            Assert.AreEqual(0, collection.Count);
            Assert.AreEqual(1, use.TotalQuantity);
            Assert.IsTrue(collection.IsEmpty);
        }

        [Test]
        public void Unregister_WhenNotRegistered_ReturnsFalse()
        {
            Assert.IsFalse(new InventoryCollection().Unregister<UseTestItem>());
        }

        [Test]
        public void Unregister_ThenRegisterAgain_Works()
        {
            var collection = new InventoryCollection();
            collection.Register(new Inventory<UseTestItem>());
            collection.Unregister<UseTestItem>();

            var replacement = new Inventory<UseTestItem>();
            collection.Register(replacement);

            Assert.AreSame(replacement, collection.GetInventory<UseTestItem>());
        }

        [Test]
        public void GetInventory_WhenNotRegistered_Throws()
        {
            var collection = new InventoryCollection();

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => collection.GetInventory<UseTestItem>());
            Assert.IsFalse(collection.TryGetInventory<UseTestItem>(out var found));
            Assert.IsNull(found);
        }

        [Test]
        public void IsRegistered_NullType_ReturnsFalse()
        {
            Assert.IsFalse(new InventoryCollection().IsRegistered(null));
        }

        [Test]
        public void Register_NullInventory_Throws()
        {
            var collection = new InventoryCollection();

            Assert.Throws<ArgumentNullException>(() => collection.Register<UseTestItem>(null));
            Assert.Throws<ArgumentNullException>(() => collection.TryRegister<UseTestItem>(null));
        }
    }
}
