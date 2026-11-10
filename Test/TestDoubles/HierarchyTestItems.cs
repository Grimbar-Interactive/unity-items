using System;

namespace GI.UnityToolkit.Items.Test
{
    // A small item hierarchy for exercising type-based routing:
    //   Item -> BasicTestItem -> UseTestItem -> SpecialUseTestItem
    //   Item -> OtherTestItem (unrelated branch)
    internal class BasicTestItem : Item
    {
        public BasicTestItem(IItemAttributes attributes) : base(attributes)
        {
        }

        public BasicTestItem(IItemAttributes attributes, Guid instanceId) : base(attributes, instanceId)
        {
        }
    }

    internal class UseTestItem : BasicTestItem
    {
        public UseTestItem(IItemAttributes attributes) : base(attributes)
        {
        }
    }

    internal class SpecialUseTestItem : UseTestItem
    {
        public SpecialUseTestItem(IItemAttributes attributes) : base(attributes)
        {
        }
    }

    internal class OtherTestItem : Item
    {
        public OtherTestItem(IItemAttributes attributes) : base(attributes)
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
        public static BasicTestItem Basic(string id = "basic", int max = 1) => new(new FakeItemAttributes(id, max));

        public static UseTestItem Use(string id = "use", int max = 1) => new(new FakeItemAttributes(id, max));

        public static SpecialUseTestItem Special(string id = "special", int max = 1) => new(new FakeItemAttributes(id, max));

        public static OtherTestItem Other(string id = "other", int max = 1) => new(new FakeItemAttributes(id, max));
    }
}
