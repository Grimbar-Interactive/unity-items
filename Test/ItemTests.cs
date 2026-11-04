using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemTests
    {
        [Test]
        public void Constructor_NullAttributes_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TestItem(null));
        }

        [Test]
        public void Constructor_AssignsInstanceId()
        {
            var item = TestItems.NonStackable();
            Assert.IsTrue(item.InstanceId.HasValue);
            Assert.AreNotEqual(Guid.Empty, item.InstanceId.Value);
        }

        [Test]
        public void InstanceId_Stackable_IsNull()
        {
            var item = TestItems.Stackable("potion", max: 10);
            Assert.IsNull(item.InstanceId);
        }

        [Test]
        public void Constructor_WithInstanceId_Stackable_InstanceIdIsNull()
        {
            var item = new TestItem(new FakeItemAttributes("potion", maxStackSize: 10), Guid.NewGuid());
            Assert.IsNull(item.InstanceId);
        }

        [Test]
        public void InstanceId_DiffersBetweenItemsOfSameKind()
        {
            var a = TestItems.NonStackable("disc");
            var b = TestItems.NonStackable("disc");
            Assert.AreNotEqual(a.InstanceId, b.InstanceId);
        }

        [Test]
        public void Constructor_WithInstanceId_PreservesIt()
        {
            var id = Guid.NewGuid();
            var item = new TestItem(new FakeItemAttributes("disc"), id);
            Assert.AreEqual(id, item.InstanceId);
        }

        [Test]
        public void Constructor_EmptyInstanceId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TestItem(new FakeItemAttributes("disc"), Guid.Empty));
        }

        [Test]
        public void Constructor_WithInstanceId_NullAttributes_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TestItem(null, Guid.NewGuid()));
        }

        [Test]
        public void Attributes_AreForwardedFromSource()
        {
            var attributes = new FakeItemAttributes("disc", maxStackSize: 5, name: "Disc", description: "A disc");
            var item = new TestItem(attributes);

            Assert.AreEqual("disc", item.UniqueId);
            Assert.AreEqual("Disc", item.Name);
            Assert.AreEqual("A disc", item.Description);
            Assert.IsNull(item.Icon);
            Assert.IsTrue(item.IsStackable);
            Assert.AreEqual(5, item.MaxStackSize);
        }

        [Test]
        public void MaxStackSize_NonStackable_IsOne()
        {
            var item = TestItems.NonStackable();
            Assert.AreEqual(1, item.MaxStackSize);
        }

        [Test]
        public void IsStackable_MaxStackSizeOne_IsFalse()
        {
            var item = new TestItem(new FakeItemAttributes("disc", maxStackSize: 1));
            Assert.IsFalse(item.IsStackable);
        }

        [Test]
        public void IsStackable_MaxStackSizeAboveOne_IsTrue()
        {
            var item = new TestItem(new FakeItemAttributes("disc", maxStackSize: 2));
            Assert.IsTrue(item.IsStackable);
        }

        [Test]
        public void MaxStackSize_IntMaxValue_IsPreserved()
        {
            var item = new TestItem(new FakeItemAttributes("disc", maxStackSize: int.MaxValue));
            Assert.AreEqual(int.MaxValue, item.MaxStackSize);
            Assert.IsTrue(item.IsStackable);
        }

        [Test]
        public void MaxStackSize_Stackable_IsConfiguredValue()
        {
            var item = TestItems.Stackable(max: 7);
            Assert.AreEqual(7, item.MaxStackSize);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void MaxStackSize_InvalidConfiguredSize_ClampsToOneAndIsNotStackable(int maxStackSize)
        {
            var item = new TestItem(new FakeItemAttributes("disc", maxStackSize: maxStackSize));
            Assert.AreEqual(1, item.MaxStackSize);
            Assert.IsFalse(item.IsStackable);
        }

        [Test]
        public void CanStackWith_SameUniqueIdBothStackable_IsTrue()
        {
            var a = TestItems.Stackable("disc");
            var b = TestItems.Stackable("disc");
            Assert.IsTrue(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_DifferentUniqueId_IsFalse()
        {
            var a = TestItems.Stackable("disc");
            var b = TestItems.Stackable("patch");
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_ReceiverNotStackable_IsFalse()
        {
            var a = TestItems.NonStackable("disc");
            var b = TestItems.Stackable("disc");
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_OtherNotStackable_IsFalse()
        {
            var a = TestItems.Stackable("disc");
            var b = TestItems.NonStackable("disc");
            Assert.IsFalse(a.CanStackWith(b));
        }

        [Test]
        public void CanStackWith_Null_IsFalse()
        {
            var a = TestItems.Stackable("disc");
            Assert.IsFalse(a.CanStackWith(null));
        }

        [Test]
        public void CanStackWith_IsSymmetric()
        {
            var a = TestItems.Stackable("disc");
            var b = TestItems.Stackable("disc");
            Assert.AreEqual(a.CanStackWith(b), b.CanStackWith(a));
        }

        [Test]
        public void CanStackWith_Override_RequiresMatchingState()
        {
            var attributes = new FakeItemAttributes("disc", maxStackSize: 10);
            var worn = new DurabilityTestItem(attributes, durability: 40);
            var pristine = new DurabilityTestItem(attributes, durability: 100);

            Assert.IsFalse(worn.CanStackWith(pristine));
        }

        [Test]
        public void ToString_IncludesTypeAndUniqueId()
        {
            var item = TestItems.NonStackable("disc_destroyer");
            Assert.AreEqual("TestItem(disc_destroyer)", item.ToString());
        }

        [Test]
        public void ItemOfT_Definition_IsTypedAndNotNull()
        {
            var attributes = new FakeItemAttributes("disc");
            var item = new TypedTestItem(attributes);
            Assert.AreSame(attributes, item.Definition);
        }

        [Test]
        public void ItemOfT_AcceptsPlainIItemAttributes_WithoutScriptableObject()
        {
            var item = new TypedTestItem(new FakeItemAttributes("disc"));
            Assert.IsNotNull(item);
        }
    }
}
