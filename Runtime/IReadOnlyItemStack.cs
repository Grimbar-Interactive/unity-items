namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// Read-only view of one inventory slot: an item and how many units of it the slot holds. Lets
    /// UI and other consumers bind to slots without caring whether the slot belongs to an
    /// <see cref="Inventory{TItem}"/> or a <see cref="LinkedInventory{TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">The item type the slot may hold.</typeparam>
    public interface IReadOnlyItemStack<out TItem> where TItem : Item
    {
        /// <summary>
        /// The item held, or null when the slot is empty.
        /// </summary>
        TItem Item { get; }

        /// <summary>
        /// Units held. Zero exactly when <see cref="Item"/> is null.
        /// </summary>
        int Quantity { get; }

        /// <summary>
        /// True when the slot holds no item.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Units that could still be added to this slot. Zero when the slot is empty or at its limit.
        /// </summary>
        int RemainingSpace { get; }

        /// <summary>
        /// True when the slot has reached its stack limit. False when the slot is empty.
        /// </summary>
        bool IsFull { get; }
    }
}
