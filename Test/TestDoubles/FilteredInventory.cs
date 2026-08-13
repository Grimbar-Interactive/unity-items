namespace GI.UnityToolkit.Items.Test
{
    internal sealed class FilteredInventory : Inventory<TestItem>
    {
        public FilteredInventory(int? maxSlotCount = null, bool allowDuplicates = true) : base(maxSlotCount, allowDuplicates)
        {
        }

        protected override bool CanAcceptItem(TestItem item)
        {
            return item != null && item.UniqueId == "allowed";
        }
    }
}
