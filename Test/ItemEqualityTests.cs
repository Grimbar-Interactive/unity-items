using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemEqualityTests
    {
        [Test]
        public void Equals_IsReferenceEquality_NotOverridden()
        {
            var attributes = new FakeItemAttributes("disc", maxStackSize: 10);
            var a = new TestItem(attributes);
            var b = new TestItem(attributes);

            Assert.IsTrue(a.Equals(a));
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_IsReferenceBased_TwoIdenticalItemsDiffer()
        {
            var attributes = new FakeItemAttributes("disc", maxStackSize: 10);
            var a = new TestItem(attributes);
            var b = new TestItem(attributes);

            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void TwoItemsFromSameAttributes_AreNotEqual_ButCanStackWith()
        {
            var attributes = new FakeItemAttributes("disc", maxStackSize: 10);
            var a = new TestItem(attributes);
            var b = new TestItem(attributes);

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a.CanStackWith(b));
        }

        [Test]
        public void Item_DoesNotImplementIEquatable()
        {
            var item = TestItems.NonStackable();
            Assert.IsNotAssignableFrom<System.IEquatable<Item>>(item);
        }
    }
}
