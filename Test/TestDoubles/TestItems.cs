using System.Collections.Generic;

namespace GI.UnityToolkit.Items.Test
{
    internal static class TestItems
    {
        public static TestItem Stackable(string id = "stackable", int max = 10)
        {
            return new TestItem(FakeItemDefinition.Create(id, maxStackSize: max));
        }

        public static TestItem NonStackable(string id = "unique")
        {
            return new TestItem(FakeItemDefinition.Create(id));
        }

        public static List<TestItem> Many(int count, string id = "unique")
        {
            var items = new List<TestItem>(count);
            for (var i = 0; i < count; i++) items.Add(NonStackable(id));
            return items;
        }
    }
}
