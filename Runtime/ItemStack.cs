using System;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// One inventory slot: an item and how many units of it are held. An empty slot is a stack whose
    /// <see cref="Item"/> is null and whose <see cref="Quantity"/> is zero.
    /// </summary>
    /// <remarks>
    /// Constructed and mutated only by <see cref="Inventory{TItem}"/>. In a fixed-size inventory the
    /// same stack object stays at the same index for the inventory's lifetime, so UI may bind to a
    /// slot once and thereafter refresh on <see cref="Inventory{TItem}.OnSlotChanged"/>.
    /// <para>
    /// Not Unity-serializable; see the remarks on <see cref="Inventory{TItem}"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TItem">The item type this slot may hold.</typeparam>
    public class ItemStack<TItem> where TItem : Item
    {
        /// <summary>
        /// The item held, or null when the slot is empty.
        /// </summary>
        public TItem Item { get; private set; }

        /// <summary>
        /// Units held. Zero exactly when <see cref="Item"/> is null; otherwise 1..<see cref="Items.Item.MaxStackSize"/>.
        /// </summary>
        public int Quantity { get; private set; }

        /// <summary>
        /// True when the slot holds no item.
        /// </summary>
        public bool IsEmpty => Item == null;

        /// <summary>
        /// Units that could still be added to this stack. Zero when the slot is empty or already at
        /// or beyond its limit (an item's limit can shrink after it was stacked).
        /// </summary>
        public int RemainingSpace => IsEmpty ? 0 : Math.Max(0, Item.MaxStackSize - Quantity);

        /// <summary>
        /// True when the stack has reached its item's stack limit. False when the slot is empty.
        /// </summary>
        public bool IsFull => !IsEmpty && Quantity >= Item.MaxStackSize;

        internal ItemStack()
        {
        }

        /// <summary>
        /// Replaces the slot's contents wholesale. Does not clamp: callers guarantee the quantity
        /// fits the item's stack limit, or are relocating an existing stack as-is. Clamping here
        /// would silently destroy units of a stack that is over-full because its limit shrank.
        /// </summary>
        internal void Set(TItem item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                Clear();
                return;
            }

            Item = item;
            Quantity = quantity;
        }

        /// <summary>
        /// Adds up to <paramref name="amount"/> units, clamped to the stack limit.
        /// Returns the number of units actually added.
        /// </summary>
        internal int Add(int amount)
        {
            if (IsEmpty || amount <= 0) return 0;
            var added = Math.Min(amount, RemainingSpace);
            Quantity += added;
            return added;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> units, clearing the slot at zero. Returns units actually removed.
        /// </summary>
        internal int Remove(int amount)
        {
            if (IsEmpty || amount <= 0) return 0;
            var removed = Math.Min(amount, Quantity);
            Quantity -= removed;
            if (Quantity <= 0) Clear();
            return removed;
        }

        /// <summary>
        /// Empties the slot.
        /// </summary>
        internal void Clear()
        {
            Item = null;
            Quantity = 0;
        }

        /// <summary>
        /// Exchanges contents with <paramref name="other"/> without replacing either object.
        /// </summary>
        internal void SwapWith(ItemStack<TItem> other)
        {
            (Item, other.Item) = (other.Item, Item);
            (Quantity, other.Quantity) = (other.Quantity, Quantity);
        }

        /// <summary>
        /// Returns <c>&lt;empty&gt;</c> or a string of the form <c>disc_name x3</c>.
        /// </summary>
        public override string ToString() => IsEmpty ? "<empty>" : $"{Item.UniqueId} x{Quantity}";
    }
}
