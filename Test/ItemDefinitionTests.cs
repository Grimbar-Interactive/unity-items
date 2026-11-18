using NUnit.Framework;
using UnityEngine;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemDefinitionTests
    {
        private PlainTestDefinition _plain;
        private StackableTestDefinition _stackable;

        [SetUp]
        public void SetUp()
        {
            _plain = ScriptableObject.CreateInstance<PlainTestDefinition>();
            _stackable = ScriptableObject.CreateInstance<StackableTestDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_plain);
            Object.DestroyImmediate(_stackable);
        }

        [Test]
        public void PlainDefinition_IsNotStackable()
        {
            Assert.AreEqual(1, _plain.MaxStackSize);
            Assert.IsFalse(_plain.IsStackable);
        }

        [Test]
        public void StackableDefinition_DefaultsTo64AndIsStackable()
        {
            Assert.AreEqual(64, _stackable.MaxStackSize);
            Assert.IsTrue(_stackable.IsStackable);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void StackableDefinition_OnValidate_RaisesTooSmallLimitToOne(int value)
        {
            _stackable.SetMaxStackSize(value);

            _stackable.RunOnValidate();

            Assert.AreEqual(1, _stackable.MaxStackSize);
            Assert.IsFalse(_stackable.IsStackable);
        }

        [Test]
        public void StackableDefinition_OnValidate_AllowsLimitOfOneAndIsNotStackable()
        {
            _stackable.SetMaxStackSize(1);

            _stackable.RunOnValidate();

            Assert.AreEqual(1, _stackable.MaxStackSize);
            Assert.IsFalse(_stackable.IsStackable);
        }

        [Test]
        public void ItemFromStackableDefinitionWithLimitOne_IsNonStackableWithInstanceId()
        {
            _stackable.SetMaxStackSize(1);

            var item = new DefinitionTestItem(_stackable);

            Assert.IsFalse(item.IsStackable);
            Assert.IsNotNull(item.InstanceId);
            Assert.AreEqual(1, item.MaxStackSize);
        }

        [Test]
        public void StackableDefinition_OnValidate_KeepsValidLimit()
        {
            _stackable.SetMaxStackSize(10);

            _stackable.RunOnValidate();

            Assert.AreEqual(10, _stackable.MaxStackSize);
        }

        [Test]
        public void ItemFromPlainDefinition_IsNonStackableWithInstanceId()
        {
            var item = new DefinitionTestItem(_plain);

            Assert.IsFalse(item.IsStackable);
            Assert.IsNotNull(item.InstanceId);
            Assert.AreEqual(1, item.MaxStackSize);
        }

        [Test]
        public void PlainDefinition_CreateItem_ReturnsTypedItemBuiltFromIt()
        {
            DefinitionTestItem item = _plain.CreateItem();

            Assert.AreSame(_plain, item.Definition);
        }

        [Test]
        public void PlainDefinition_UntypedCreateItem_ReturnsSameKindOfItem()
        {
            Item item = ((ItemDefinition)_plain).CreateItem();

            Assert.IsInstanceOf<DefinitionTestItem>(item);
            Assert.AreSame(_plain, item.Definition);
        }

        [Test]
        public void StackableDefinition_CreateItem_ReturnsStackableItem()
        {
            var item = _stackable.CreateItem();

            Assert.AreSame(_stackable, item.Definition);
            Assert.IsTrue(item.IsStackable);
            Assert.IsNull(item.InstanceId);
        }

        [Test]
        public void CreateItem_EachCallReturnsNewInstance()
        {
            Assert.AreNotSame(_plain.CreateItem(), _plain.CreateItem());
            Assert.AreNotEqual(_plain.CreateItem().InstanceId, _plain.CreateItem().InstanceId);
        }

        [Test]
        public void ItemFromStackableDefinition_IsStackableWithoutInstanceId()
        {
            var item = new DefinitionTestItem(_stackable);

            Assert.IsTrue(item.IsStackable);
            Assert.IsNull(item.InstanceId);
            Assert.AreEqual(64, item.MaxStackSize);
        }
    }
}
