using System;
using System.Collections.Generic;

namespace GI.UnityToolkit.Items.Test
{
    internal sealed class InventoryEventRecorder<TItem> : IDisposable where TItem : Item
    {
        private readonly IReadOnlyInventory<TItem> _inventory;

        public List<(TItem Item, int Quantity)> Added { get; } = new();
        public List<(TItem Item, int Quantity)> Removed { get; } = new();
        public List<int> ChangedSlots { get; } = new();
        public List<string> Order { get; } = new();

        public int AddedCount => Added.Count;
        public int RemovedCount => Removed.Count;
        public int SlotChangedCount => ChangedSlots.Count;
        public int UpdatedCount { get; private set; }

        public InventoryEventRecorder(IReadOnlyInventory<TItem> inventory)
        {
            _inventory = inventory;
            _inventory.OnItemAdded += HandleItemAdded;
            _inventory.OnItemRemoved += HandleItemRemoved;
            _inventory.OnSlotChanged += HandleSlotChanged;
            _inventory.OnInventoryUpdated += HandleInventoryUpdated;
        }

        private void HandleItemAdded(TItem item, int quantity)
        {
            Added.Add((item, quantity));
            Order.Add("Added");
        }

        private void HandleItemRemoved(TItem item, int quantity)
        {
            Removed.Add((item, quantity));
            Order.Add("Removed");
        }

        private void HandleSlotChanged(int index)
        {
            ChangedSlots.Add(index);
            Order.Add("SlotChanged");
        }

        private void HandleInventoryUpdated()
        {
            UpdatedCount++;
            Order.Add("Updated");
        }

        public void Dispose()
        {
            _inventory.OnItemAdded -= HandleItemAdded;
            _inventory.OnItemRemoved -= HandleItemRemoved;
            _inventory.OnSlotChanged -= HandleSlotChanged;
            _inventory.OnInventoryUpdated -= HandleInventoryUpdated;
        }
    }
}
