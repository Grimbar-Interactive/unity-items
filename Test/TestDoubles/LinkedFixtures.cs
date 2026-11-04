namespace GI.UnityToolkit.Items.Test
{
    internal static class LinkedFixtures
    {
        /// <summary>
        /// A stackable "potion" with the given per-stack limit.
        /// </summary>
        public static TestItem Potion(int max = 10) => TestItems.Stackable("potion", max);

        /// <summary>
        /// An unbounded source holding <paramref name="count"/> potions, split into stacks of <paramref name="max"/>.
        /// </summary>
        public static Inventory<TestItem> SourceWithPotions(int count, int max = 10)
        {
            var source = new Inventory<TestItem>();
            source.TryAdd(Potion(max), count);
            return source;
        }

        /// <summary>
        /// Adds a new non-stackable item to <paramref name="source"/> and returns it.
        /// </summary>
        public static TestItem AddSword(Inventory<TestItem> source, string id = "sword")
        {
            var sword = TestItems.NonStackable(id);
            source.TryAdd(sword);
            return sword;
        }
    }
}
