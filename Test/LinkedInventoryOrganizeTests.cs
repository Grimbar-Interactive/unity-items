using System;
using NUnit.Framework;
using static GI.UnityToolkit.Items.Test.LinkedFixtures;

namespace GI.UnityToolkit.Items.Test
{
    public class LinkedInventoryOrganizeTests
    {
        private static int ById(TestItem a, TestItem b) => string.CompareOrdinal(a.UniqueId, b.UniqueId);

        [Test]
        public void Compact_Fixed_ShiftsOccupiedSlotsToTheFront()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source, "a");
            var b = AddSword(source, "b");
            var linked = new LinkedInventory<TestItem>(source, 4);
            linked.TryAddAt(1, a);
            linked.TryAddAt(3, b);

            Assert.IsTrue(linked.Compact());

            Assert.AreSame(a, linked[0].Item);
            Assert.AreSame(b, linked[1].Item);
            Assert.IsTrue(linked[2].IsEmpty);
            Assert.IsTrue(linked[3].IsEmpty);
        }

        [Test]
        public void Compact_AlreadyCompact_ReturnsFalse()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source, "a");
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAdd(a);

            Assert.IsFalse(linked.Compact());
        }

        [Test]
        public void Compact_Unbounded_IsAlwaysFalse()
        {
            var source = new Inventory<TestItem>();
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(AddSword(source));

            Assert.IsFalse(linked.Compact());
        }

        [Test]
        public void Sort_Fixed_OrdersOccupiedSlotsAndPushesEmptyOnesToTheEnd()
        {
            var source = new Inventory<TestItem>();
            var a = AddSword(source, "a");
            var b = AddSword(source, "b");
            var c = AddSword(source, "c");
            var linked = new LinkedInventory<TestItem>(source, 4);
            linked.TryAddAt(0, c);
            linked.TryAddAt(1, a);
            linked.TryAddAt(3, b);

            Assert.IsTrue(linked.Sort(ById));

            Assert.AreSame(a, linked[0].Item);
            Assert.AreSame(b, linked[1].Item);
            Assert.AreSame(c, linked[2].Item);
            Assert.IsTrue(linked[3].IsEmpty);
        }

        [Test]
        public void Sort_Unbounded_Reorders()
        {
            var source = new Inventory<TestItem>();
            var b = AddSword(source, "b");
            var a = AddSword(source, "a");
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(b);
            linked.TryAdd(a);

            linked.Sort(ById);

            Assert.AreSame(a, linked[0].Item);
            Assert.AreSame(b, linked[1].Item);
        }

        [Test]
        public void Sort_IsStable_ForEqualRanks()
        {
            var source = new Inventory<TestItem>();
            var first = AddSword(source, "same");
            var second = AddSword(source, "same");
            var linked = new LinkedInventory<TestItem>(source);
            linked.TryAdd(first);
            linked.TryAdd(second);

            linked.Sort(ById);

            Assert.AreSame(first, linked[0].Item);
            Assert.AreSame(second, linked[1].Item);
        }

        [Test]
        public void Sort_KeepsQuantities()
        {
            var source = SourceWithPotions(25);
            source.TryAdd(TestItems.Stackable("ether", 10), 10);
            var linked = new LinkedInventory<TestItem>(source, 3);
            linked.TryAddAt(0, Potion(), 6);
            linked.TryAddAt(1, TestItems.Stackable("ether", 10), 2);

            linked.Sort(ById);

            Assert.AreEqual("ether", linked[0].UniqueId);
            Assert.AreEqual(2, linked[0].Quantity);
            Assert.AreEqual("potion", linked[1].UniqueId);
            Assert.AreEqual(6, linked[1].Quantity);
        }

        [Test]
        public void Sort_NothingToSort_ReturnsTrue()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>(), 3);
            Assert.IsTrue(linked.Sort(ById));
        }

        [Test]
        public void Sort_NullComparison_Throws()
        {
            var linked = new LinkedInventory<TestItem>(new Inventory<TestItem>());
            Assert.Throws<ArgumentNullException>(() => linked.Sort((Comparison<TestItem>)null));
            Assert.Throws<ArgumentNullException>(() => linked.Sort((System.Collections.Generic.IComparer<TestItem>)null));
        }
    }
}
