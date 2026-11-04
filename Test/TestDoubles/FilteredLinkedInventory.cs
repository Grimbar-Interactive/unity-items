namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// A <see cref="LinkedInventory{TItem}"/> with both filters switched on: one banned UniqueId, and
    /// optionally one slot index that only accepts one UniqueId.
    /// </summary>
    internal sealed class FilteredLinkedInventory : LinkedInventory<TestItem>
    {
        public FilteredLinkedInventory(Inventory<TestItem> source, int? maxSlotCount = null, bool allowDuplicates = true)
            : base(source, maxSlotCount, allowDuplicates)
        {
        }

        public string BannedId { get; set; } = "banned";

        public int? RestrictedIndex { get; set; }

        public string RestrictedToId { get; set; }

        protected override bool CanAcceptItem(TestItem item)
        {
            return item != null && item.UniqueId != BannedId;
        }

        protected override bool CanAcceptItemAt(int index, TestItem item)
        {
            return index != RestrictedIndex || item.UniqueId == RestrictedToId;
        }
    }
}
