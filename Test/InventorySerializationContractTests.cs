using System;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventorySerializationContractTests
    {
        [Test]
        public void Inventory_IsNotMarkedSerializable()
        {
            Assert.IsFalse(typeof(Inventory<TestItem>).IsSerializable);
        }

        [Test]
        public void ItemStack_IsNotMarkedSerializable()
        {
            Assert.IsFalse(typeof(ItemStack<TestItem>).IsSerializable);
        }
    }
}
