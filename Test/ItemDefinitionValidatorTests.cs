using System;
using System.Linq;
using GI.UnityToolkit.Items.Editor;
using NUnit.Framework;

namespace GI.UnityToolkit.Items.Test
{
    public class ItemDefinitionValidatorTests
    {
        private static FakeItemAttributes Def(string id) => new FakeItemAttributes(id);

        [Test]
        public void NoDuplicates_ReturnsEmpty()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(new[] { Def("a"), Def("b"), Def("c") });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void EmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, ItemDefinitionValidator.FindDuplicateUniqueIds(Array.Empty<FakeItemAttributes>()).Count);
        }

        [Test]
        public void OneDuplicatedPair_IsReportedWithBothDefinitions()
        {
            var first = Def("sword");
            var second = Def("sword");

            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(new[] { first, Def("axe"), second });

            Assert.AreEqual(1, result.Count);
            CollectionAssert.AreEqual(new[] { first, second }, result["sword"]);
        }

        [Test]
        public void ThreeWayDuplicate_ReportsAllThree()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(new[] { Def("x"), Def("x"), Def("x") });

            Assert.AreEqual(3, result["x"].Count);
        }

        [Test]
        public void SeveralDuplicatedIds_AreEachReported_OrderedById()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(
                new[] { Def("b"), Def("a"), Def("b"), Def("a"), Def("solo") });

            CollectionAssert.AreEqual(new[] { "a", "b" }, result.Keys.ToArray());
        }

        [Test]
        public void NullAndEmptyIds_AreIgnored()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(
                new[] { Def(null), Def(null), Def(string.Empty), Def(string.Empty) });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void NullEntries_AreIgnored()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(new[] { null, Def("a"), null, Def("a") });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(2, result["a"].Count);
        }

        [Test]
        public void Comparison_IsCaseSensitive()
        {
            var result = ItemDefinitionValidator.FindDuplicateUniqueIds(new[] { Def("Sword"), Def("sword") });

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void NullDefinitions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ItemDefinitionValidator.FindDuplicateUniqueIds<FakeItemAttributes>(null));
        }
    }
}
