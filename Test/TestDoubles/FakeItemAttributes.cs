using UnityEngine;

namespace GI.UnityToolkit.Items.Test
{
    internal sealed class FakeItemAttributes : IItemAttributes
    {
        public FakeItemAttributes(string uniqueId, int maxStackSize = 1, string name = null, string description = null)
        {
            UniqueId = uniqueId;
            MaxStackSize = maxStackSize;
            Name = name;
            Description = description;
        }

        public string UniqueId { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon => null;
        public int MaxStackSize { get; }
    }
}
