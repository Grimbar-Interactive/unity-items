using System;

namespace GI.UnityToolkit.Items.Test
{
    internal class TestItem : Item
    {
        public TestItem(IItemAttributes attributes) : base(attributes)
        {
        }

        public TestItem(IItemAttributes attributes, Guid instanceId) : base(attributes, instanceId)
        {
        }
    }
}
