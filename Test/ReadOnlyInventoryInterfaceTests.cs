using System.Linq;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class ReadOnlyInventoryInterfaceTests
    {
        private static (int Slots, int Filled, int Total, int Potions, int FirstQuantity, int Items) Summarize(
            IReadOnlyInventory<TestItem> inventory)
        {
            return (inventory.SlotCount, inventory.FilledSlotCount, inventory.TotalQuantity,
                inventory.GetQuantity("potion"), inventory[0].Quantity, inventory.Items.Count());
        }

        [Test]
        public void InventoryAndLinkedInventory_AnswerTheSameQuestionsTheSameWay()
        {
            var plain = new Inventory<TestItem>();
            plain.TryAdd(Potion(), 12);
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));
            linked.TryAdd(Potion(), 12);

            Assert.AreEqual(Summarize(plain), Summarize(linked));
        }

        [Test]
        public void Slots_AreExposedAsReadOnlyStacks_OnBothContainers()
        {
            var plain = new Inventory<TestItem>(2);
            plain.TryAdd(Potion(), 12);
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            linked.TryAdd(Potion(), 12);

            foreach (IReadOnlyInventory<TestItem> inventory in new IReadOnlyInventory<TestItem>[] { plain, linked })
            {
                Assert.AreEqual(2, inventory.Slots.Count);
                Assert.AreEqual(10, inventory.Slots[0].Quantity);
                Assert.IsTrue(inventory.Slots[0].IsFull);
                Assert.AreEqual(8, inventory.Slots[1].RemainingSpace);
                Assert.AreSame(inventory.Slots[0], inventory[0]);
            }
        }

        [Test]
        public void Queries_WorkThroughTheInterface()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 3);
            linked.TryAdd(sword);
            IReadOnlyInventory<TestItem> view = linked;

            Assert.IsTrue(view.Contains("potion"));
            Assert.IsTrue(view.ContainsInstance(sword));
            Assert.AreEqual(1, view.IndexOf("sword") - view.IndexOf("potion"));
            Assert.IsTrue(view.TryFindByInstanceId(sword.InstanceId.Value, out var found));
            Assert.AreSame(sword, found);
            Assert.AreEqual(4, view.GetQuantity(_ => true));
            Assert.AreEqual(1, view.FindAll(i => i.UniqueId == "sword").Count());
            Assert.IsTrue(view.CanAdd(Potion(), 5));
        }

        [Test]
        public void PredicateAndInstanceQueries_WorkThroughTheInterface()
        {
            var source = SourceWithPotions(25);
            var sword = AddSword(source);
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(Potion(), 3);
            linked.TryAdd(sword);
            IReadOnlyInventory<TestItem> view = linked;

            Assert.IsTrue(view.TryFind(i => i.UniqueId == "sword", out var found));
            Assert.AreSame(sword, found);
            Assert.IsFalse(view.TryFind(i => i.UniqueId == "missing", out var none));
            Assert.IsNull(none);

            Assert.IsTrue(view.TryGetSlotIndex(i => i.UniqueId == "sword", out var index));
            Assert.AreEqual(1, index);
            Assert.IsFalse(view.TryGetSlotIndex(i => false, out index));
            Assert.AreEqual(-1, index);

            Assert.AreEqual(1, view.IndexOfInstance(sword));
            Assert.AreEqual(1, view.IndexOfInstance(sword.InstanceId.Value));
            Assert.AreEqual(-1, view.IndexOfInstance(Potion()));
        }

        [Test]
        public void LinkedSlot_ToString_NamesTheKindAndQuantity()
        {
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 2);
            linked.TryAdd(Potion(), 3);

            Assert.AreEqual("potion x3", linked[0].ToString());
            Assert.AreEqual("<empty>", linked[1].ToString());
        }

        [Test]
        public void Events_AreRaisedThroughTheInterface_OnBothContainers()
        {
            var plain = new Inventory<TestItem>();
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25));

            foreach (IReadOnlyInventory<TestItem> inventory in new IReadOnlyInventory<TestItem>[] { plain, linked })
            {
                var updates = 0;
                inventory.OnInventoryUpdated += () => updates++;

                if (inventory is Inventory<TestItem> p) p.TryAdd(Potion());
                else ((LinkedInventory<TestItem>)inventory).TryAdd(Potion());

                Assert.AreEqual(1, updates);
            }
        }

        [Test]
        public void ItemStack_And_LinkedSlot_BothImplementTheReadOnlyStackInterface()
        {
            var plain = new Inventory<TestItem>(1);
            var linked = new LinkedInventory<TestItem>(SourceWithPotions(25), 1);

            Assert.IsInstanceOf<IReadOnlyItemStack<TestItem>>(plain[0]);
            Assert.IsInstanceOf<IReadOnlyItemStack<TestItem>>(linked[0]);
            Assert.IsTrue(((IReadOnlyItemStack<TestItem>)linked[0]).IsEmpty);
        }
    }
}
