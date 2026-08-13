using UnityEngine;

namespace GI.UnityToolkit.Items.Test
{
    internal sealed class FakeItemAttributes : IItemAttributes
    {
        public FakeItemAttributes(string uniqueId, bool isStackable = false, int maxStackSize = 1, string name = null, string description = null)
        {
            UniqueId = uniqueId;
            IsStackable = isStackable;
            MaxStackSize = maxStackSize;
            Name = name;
            Description = description;
        }

        public string UniqueId { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon => null;
        public bool IsStackable { get; }
        public int MaxStackSize { get; }
    }
}
