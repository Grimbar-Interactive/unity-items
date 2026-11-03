using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryRemoveByIdTests
    {
        [Test]
        public void TryRemove_ById_RemovesRequestedUnits()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsTrue(inventory.TryRemove("disc", 2));
            Assert.AreEqual(3, inventory.GetQuantity("disc"));
        }

        [Test]
        public void TryRemove_ById_DefaultQuantityIsOne()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.IsTrue(inventory.TryRemove("disc"));
            Assert.AreEqual(4, inventory.GetQuantity("disc"));
        }

        [Test]
        public void TryRemove_ById_Insufficient_ReturnsFalseAndChangesNothing()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);
            using var recorder = new InventoryEventRecorder<TestItem>(inventory);

            Assert.IsFalse(inventory.TryRemove("disc", 6));

            Assert.AreEqual(5, inventory.GetQuantity("disc"));
            Assert.AreEqual(0, recorder.UpdatedCount);
        }

        [Test]
        public void TryRemove_ById_NullOrEmpty_ReturnsFalse()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"));

            Assert.IsFalse(inventory.TryRemove((string)null));
            Assert.IsFalse(inventory.TryRemove(string.Empty));
        }

        [TestCase(1, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(0, false)]
        [TestCase(-1, false)]
        public void CanRemove_ById_MatchesHeldQuantity(int quantity, bool expected)
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.Stackable("disc", max: 10), 5);

            Assert.AreEqual(expected, inventory.CanRemove("disc", quantity));
        }

        [Test]
        public void CanRemove_ById_UnknownOrNullId_IsFalse()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"));

            Assert.IsFalse(inventory.CanRemove("missing"));
            Assert.IsFalse(inventory.CanRemove((string)null));
        }

        [Test]
        public void CanRemove_ById_DefaultQuantityIsOne()
        {
            var inventory = new Inventory<TestItem>();
            inventory.TryAdd(TestItems.NonStackable("disc"));

            Assert.IsTrue(inventory.CanRemove("disc"));
        }
    }
}
