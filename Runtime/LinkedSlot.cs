using System;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// One <see cref="LinkedInventory{TItem}"/> slot: a reference to items held by the linked source
    /// inventory, and how many units of them this slot claims. An empty slot has a null
    /// <see cref="Item"/> and a <see cref="Quantity"/> of zero.
    /// </summary>
    /// <remarks>
    /// A slot never owns anything. Its identity is a key into the source: <see cref="InstanceId"/>
    /// for a non-stackable item, <see cref="UniqueId"/> for a stackable one. <see cref="Item"/> is only
    /// a cached reference to the source's item, refreshed whenever the linked inventory reconciles.
    /// In a fixed-size linked inventory the same slot object stays at the same index for its
    /// lifetime, so UI may bind once and refresh on
    /// <see cref="LinkedInventory{TItem}.OnSlotChanged"/>.
    /// <para>
    /// Constructed and mutated only by <see cref="LinkedInventory{TItem}"/>. Not Unity-serializable.
    /// </para>
    /// </remarks>
    /// <typeparam name="TItem">The item type this slot may reference.</typeparam>
    public class LinkedSlot<TItem> : IReadOnlyItemStack<TItem> where TItem : Item
    {
        private readonly LinkedInventory<TItem> _owner;

        /// <summary>
        /// The referenced item as the source inventory holds it, or null when the slot is empty.
        /// </summary>
        public TItem Item { get; private set; }

        /// <summary>
        /// Units this slot claims from the source. Zero exactly when <see cref="Item"/> is null.
        /// </summary>
        public int Quantity { get; private set; }

        /// <summary>
        /// True when the slot references nothing.
        /// </summary>
        public bool IsEmpty => Item == null;

        /// <summary>
        /// The <see cref="IItemAttributes.UniqueId"/> this slot references, or null when empty. The
        /// save key for a stackable item.
        /// </summary>
        public string UniqueId => IsEmpty ? null : Item.UniqueId;

        /// <summary>
        /// The <see cref="Items.Item.InstanceId"/> this slot references: the save key for a
        /// non-stackable item. Null when empty or when the item is stackable.
        /// </summary>
        public Guid? InstanceId => IsEmpty ? null : Item.InstanceId;

        /// <summary>
        /// This slot's stack limit: the linked inventory's effective limit for the item (see
        /// <see cref="LinkedInventory{TItem}.GetMaxStackSize"/>). Zero when empty.
        /// </summary>
        public int MaxStackSize => IsEmpty ? 0 : _owner.GetMaxStackSize(Item);

        /// <summary>
        /// Units that could still be added to this slot. Zero when empty or at or beyond its limit
        /// (a limit can shrink after units were assigned).
        /// </summary>
        public int RemainingSpace => IsEmpty ? 0 : Math.Max(0, MaxStackSize - Quantity);

        /// <summary>
        /// True when the slot has reached its stack limit. False when empty.
        /// </summary>
        public bool IsFull => !IsEmpty && Quantity >= MaxStackSize;

        internal LinkedSlot(LinkedInventory<TItem> owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Replaces the slot's contents wholesale. Does not clamp, mirroring
        /// <see cref="ItemStack{TItem}"/>: callers guarantee the quantity fits, or are relocating a
        /// slot as-is.
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
        /// Points the slot at the source's current object for the same key, keeping the quantity.
        /// Returns true when the reference actually changed.
        /// </summary>
        internal bool RefreshItem(TItem item)
        {
            if (IsEmpty || item == null || ReferenceEquals(Item, item)) return false;
            Item = item;
            return true;
        }

        /// <summary>
        /// Adds up to <paramref name="amount"/> units, clamped to the slot's limit.
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

        internal void Clear()
        {
            Item = null;
            Quantity = 0;
        }

        internal void SwapWith(LinkedSlot<TItem> other)
        {
            (Item, other.Item) = (other.Item, Item);
            (Quantity, other.Quantity) = (other.Quantity, Quantity);
        }

        /// <summary>
        /// Returns <c>&lt;empty&gt;</c> or a string of the form <c>potion x3</c>.
        /// </summary>
        public override string ToString() => IsEmpty ? "<empty>" : $"{Item.UniqueId} x{Quantity}";
    }
}
