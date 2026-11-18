using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemEqualityTests
    {
        [Test]
        public void Equals_IsReferenceEquality_NotOverridden()
        {
            var definition = FakeItemDefinition.Create("disc", maxStackSize: 10);
            var a = new TestItem(definition);
            var b = new TestItem(definition);

            Assert.IsTrue(a.Equals(a));
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_IsReferenceBased_TwoIdenticalItemsDiffer()
        {
            var definition = FakeItemDefinition.Create("disc", maxStackSize: 10);
            var a = new TestItem(definition);
            var b = new TestItem(definition);

            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void TwoItemsFromSameAttributes_AreNotEqual_ButCanStackWith()
        {
            var definition = FakeItemDefinition.Create("disc", maxStackSize: 10);
            var a = new TestItem(definition);
            var b = new TestItem(definition);

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
