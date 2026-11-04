using System;
using System.Linq;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class InventoryAssemblyContractTests
    {
        [Test]
        public void CoreTypes_LiveInDedicatedAssembly()
        {
            Assert.AreEqual("GrimbarInteractive.UnityToolkit.Items", typeof(Item).Assembly.GetName().Name);
        }

        [TestCase(typeof(Item))]
        [TestCase(typeof(IItemAttributes))]
        [TestCase(typeof(ItemStack<>))]
        [TestCase(typeof(Inventory<>))]
        [TestCase(typeof(LinkedInventory<>))]
        [TestCase(typeof(LinkedSlot<>))]
        [TestCase(typeof(IReadOnlyInventory<>))]
        [TestCase(typeof(IReadOnlyItemStack<>))]
        public void CoreTypes_LiveInToolkitNamespace(Type type)
        {
            Assert.AreEqual("GI.UnityToolkit.Items", type.Namespace);
        }

        [Test]
        public void CoreAssembly_ReferencesNoOdinAssembly()
        {
            var references = typeof(Item).Assembly.GetReferencedAssemblies();
            Assert.IsFalse(references.Any(r => r.Name.StartsWith("Sirenix", StringComparison.Ordinal)));
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceAssemblyCSharp()
        {
            var references = typeof(Item).Assembly.GetReferencedAssemblies();
            Assert.IsFalse(references.Any(r => r.Name == "Assembly-CSharp"));
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceJetBrainsAnnotations()
        {
            var references = typeof(Item).Assembly.GetReferencedAssemblies();
            Assert.IsFalse(references.Any(r => r.Name.Contains("JetBrains")));
        }
    }
}
