using System;

namespace GI.UnityToolkit.Items.Test
{
    // A small item hierarchy for exercising type-based routing:
    //   Item -> BasicTestItem -> UseTestItem -> SpecialUseTestItem
    //   Item -> OtherTestItem (unrelated branch)
    internal class BasicTestItem : Item
    {
        public BasicTestItem(ItemDefinition definition) : base(definition)
        {
        }

        public BasicTestItem(ItemDefinition definition, Guid instanceId) : base(definition, instanceId)
        {
        }
    }

    internal class UseTestItem : BasicTestItem
    {
        public UseTestItem(ItemDefinition definition) : base(definition)
        {
        }
    }

    internal class SpecialUseTestItem : UseTestItem
    {
        public SpecialUseTestItem(ItemDefinition definition) : base(definition)
        {
        }
    }

    internal class OtherTestItem : Item
    {
        public OtherTestItem(ItemDefinition definition) : base(definition)
        {
        }
    }

    /// <summary>
    /// An inventory that refuses every item, regardless of space.
    /// </summary>
    internal sealed class RejectingUseInventory : Inventory<UseTestItem>
    {
        protected override bool CanAcceptItem(UseTestItem item) => false;
    }

    internal static class HierarchyItems
    {
        public static BasicTestItem Basic(string id = "basic", int max = 1) => new(FakeItemDefinition.Create(id, max));

        public static UseTestItem Use(string id = "use", int max = 1) => new(FakeItemDefinition.Create(id, max));

        public static SpecialUseTestItem Special(string id = "special", int max = 1) => new(FakeItemDefinition.Create(id, max));

        public static OtherTestItem Other(string id = "other", int max = 1) => new(FakeItemDefinition.Create(id, max));
    }
}
