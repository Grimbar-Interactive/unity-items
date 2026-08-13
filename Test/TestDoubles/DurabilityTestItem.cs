namespace GI.UnityToolkit.Items.Test
{
    internal sealed class DurabilityTestItem : Item
    {
        public DurabilityTestItem(IItemAttributes attributes, int durability) : base(attributes)
        {
            Durability = durability;
        }

        public int Durability { get; }

        public override bool CanStackWith(Item other)
        {
            return base.CanStackWith(other) && other is DurabilityTestItem d && d.Durability == Durability;
        }
    }
}
