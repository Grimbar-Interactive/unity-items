using System;
using System.Collections.Generic;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// The read side shared by <see cref="Inventory{TItem}"/> and <see cref="LinkedInventory{TItem}"/>:
    /// slots, counts, queries and change events. Bind UI and read-only game logic to this and either
    /// container will do.
    /// </summary>
    /// <typeparam name="TItem">The item type the inventory holds.</typeparam>
    public interface IReadOnlyInventory<TItem> : IEnumerable<TItem> where TItem : Item
    {
        /// <inheritdoc cref="Inventory{TItem}.OnItemAdded"/>
        event Inventory<TItem>.ItemQuantityDelegate OnItemAdded;

        /// <inheritdoc cref="Inventory{TItem}.OnItemRemoved"/>
        event Inventory<TItem>.ItemQuantityDelegate OnItemRemoved;

        /// <inheritdoc cref="Inventory{TItem}.OnSlotChanged"/>
        event Inventory<TItem>.SlotDelegate OnSlotChanged;

        /// <inheritdoc cref="Inventory{TItem}.OnInventoryUpdated"/>
        event Action OnInventoryUpdated;

        /// <inheritdoc cref="Inventory{TItem}.MaxSlotCount"/>
        int? MaxSlotCount { get; }

        /// <inheritdoc cref="Inventory{TItem}.AllowDuplicates"/>
        bool AllowDuplicates { get; }

        /// <inheritdoc cref="Inventory{TItem}.IsFixedSize"/>
        bool IsFixedSize { get; }

        /// <summary>
        /// Live read-only view of every slot in index order.
        /// </summary>
        IReadOnlyList<IReadOnlyItemStack<TItem>> Slots { get; }

        /// <summary>
        /// Gets the slot at <paramref name="index"/>. Never null; may be empty.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        IReadOnlyItemStack<TItem> this[int index] { get; }

        /// <inheritdoc cref="Inventory{TItem}.SlotCount"/>
        int SlotCount { get; }

        /// <inheritdoc cref="Inventory{TItem}.FilledSlotCount"/>
        int FilledSlotCount { get; }

        /// <inheritdoc cref="Inventory{TItem}.FreeSlotCount"/>
        int? FreeSlotCount { get; }

        /// <inheritdoc cref="Inventory{TItem}.IsEmpty"/>
        bool IsEmpty { get; }

        /// <inheritdoc cref="Inventory{TItem}.IsFull"/>
        bool IsFull { get; }

        /// <inheritdoc cref="Inventory{TItem}.TotalQuantity"/>
        int TotalQuantity { get; }

        /// <inheritdoc cref="Inventory{TItem}.Items"/>
        IEnumerable<TItem> Items { get; }

        /// <inheritdoc cref="Inventory{TItem}.IsValidIndex"/>
        bool IsValidIndex(int index);

        /// <inheritdoc cref="Inventory{TItem}.GetFreeSpaceFor"/>
        int GetFreeSpaceFor(TItem item);

        /// <inheritdoc cref="Inventory{TItem}.CanAdd"/>
        bool CanAdd(TItem item, int quantity = 1);

        /// <inheritdoc cref="Inventory{TItem}.ContainsInstance"/>
        bool ContainsInstance(TItem item);

        /// <inheritdoc cref="Inventory{TItem}.Contains(string)"/>
        bool Contains(string uniqueId);

        /// <inheritdoc cref="Inventory{TItem}.Contains(Func{TItem,bool})"/>
        bool Contains(Func<TItem, bool> predicate);

        /// <inheritdoc cref="Inventory{TItem}.GetQuantity(TItem)"/>
        int GetQuantity(TItem item);

        /// <inheritdoc cref="Inventory{TItem}.GetQuantity(string)"/>
        int GetQuantity(string uniqueId);

        /// <inheritdoc cref="Inventory{TItem}.GetQuantity(Func{TItem,bool})"/>
        int GetQuantity(Func<TItem, bool> predicate);

        /// <inheritdoc cref="Inventory{TItem}.GetSlotCount"/>
        int GetSlotCount(string uniqueId);

        /// <inheritdoc cref="Inventory{TItem}.TryFind"/>
        bool TryFind(Func<TItem, bool> predicate, out TItem item);

        /// <inheritdoc cref="Inventory{TItem}.FindAll"/>
        IEnumerable<TItem> FindAll(Func<TItem, bool> predicate);

        /// <inheritdoc cref="Inventory{TItem}.TryFindByInstanceId"/>
        bool TryFindByInstanceId(Guid instanceId, out TItem item);

        /// <inheritdoc cref="Inventory{TItem}.IndexOfInstance(TItem)"/>
        int IndexOfInstance(TItem item);

        /// <inheritdoc cref="Inventory{TItem}.IndexOfInstance(Guid)"/>
        int IndexOfInstance(Guid instanceId);

        /// <inheritdoc cref="Inventory{TItem}.IndexOf"/>
        int IndexOf(string uniqueId);

        /// <inheritdoc cref="Inventory{TItem}.TryGetSlotIndex"/>
        bool TryGetSlotIndex(Func<TItem, bool> predicate, out int index);

        /// <inheritdoc cref="Inventory{TItem}.BeginBatchUpdate"/>
        IDisposable BeginBatchUpdate();
    }
}
