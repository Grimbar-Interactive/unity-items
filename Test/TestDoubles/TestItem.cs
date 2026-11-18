using System;

namespace GI.UnityToolkit.Items.Test
{
    internal class TestItem : Item
    {
        public TestItem(ItemDefinition definition) : base(definition)
        {
        }

        public TestItem(ItemDefinition definition, Guid instanceId) : base(definition, instanceId)
        {
        }
    }
}
