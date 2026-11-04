using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// A slot-based container of <typeparamref name="TItem"/>. Every slot is an
    /// <see cref="ItemStack{TItem}"/> holding an item and a quantity; the inventory, not the item,
    /// owns quantity.
    /// </summary>
    /// <remarks>
    /// <para><b>Two modes.</b> When <see cref="MaxSlotCount"/> is null the inventory is
    /// <i>unbounded</i>: it grows on demand, holds no empty slots, and removing a stack removes the
    /// slot (later indices shift down). When <see cref="MaxSlotCount"/> is set the inventory is
    /// <i>fixed size</i>: exactly that many slots exist for its lifetime, emptied slots persist and
    /// keep their index, and <see cref="Compact"/> is needed to close gaps.</para>
    /// <para><b>Atomicity.</b> Methods named <c>Try*</c> are all-or-nothing: on failure they mutate
    /// nothing and fire no events. Methods named <c>*UpTo</c> and <see cref="AddRange"/> are
    /// best-effort and report how much they achieved.</para>
    /// <para><b>Unity serialization.</b> This type is deliberately NOT marked
    /// <see cref="SerializableAttribute"/> and cannot round-trip through Unity's serializer. Persist
    /// an inventory through an explicit save DTO instead - the real save key is an item's
    /// <see cref="IItemAttributes.UniqueId"/>, not its object graph.</para>
    /// </remarks>
    /// <typeparam name="TItem">The item type this inventory holds.</typeparam>
    public class Inventory<TItem> : IEnumerable<TItem> where TItem : Item
    {
        /// <summary>
        /// Signature for events that report an item and a number of units.
        /// </summary>
        public delegate void ItemQuantityDelegate(TItem item, int quantity);

        /// <summary>
        /// Signature for events that report a slot index.
        /// </summary>
        public delegate void SlotDelegate(int index);

        private readonly List<ItemStack<TItem>> _slots;

        // Pending notifications are held in plain lists and flushed through swap-in spare buffers, so
        // a steady-state add/remove allocates nothing and a flush stays safe against re-entrancy.
        private List<PendingItemEvent> _pendingItemEvents = new();
        private List<PendingItemEvent> _spareItemEvents;
        private List<int> _pendingSlots = new();
        private List<int> _spareSlots;
        private bool _pendingInventoryUpdated;
        private int _batchDepth;
        private int _version;

        private readonly struct PendingItemEvent
        {
            public readonly bool Added;
            public readonly TItem Item;
            public readonly int Quantity;

            public PendingItemEvent(bool added, TItem item, int quantity)
            {
                Added = added;
                Item = item;
                Quantity = quantity;
            }
        }

        /// <summary>
        /// Raised once per logical add with the item and the total units added, even when those units
        /// span several slots. Never raised for a failed or zero-unit add.
        /// </summary>
        public event ItemQuantityDelegate OnItemAdded;

        /// <summary>
        /// Raised once per logical removal with the item and the total units removed, even when those
        /// units came from several slots. <see cref="Clear"/> raises it once per distinct stack cleared.
        /// </summary>
        public event ItemQuantityDelegate OnItemRemoved;

        /// <summary>
        /// Raised once for each slot index whose contents changed, in ascending index order.
        /// Coalesced within a batch, so a slot touched repeatedly reports once.
        /// </summary>
        /// <remarks>
        /// In unbounded mode a removed slot shifts later slots down. Indices that no longer exist
        /// when the event is raised are skipped, and a slot that simply vanished (including the
        /// last one, or everything after <see cref="Clear"/>) is reported only through
        /// <see cref="OnInventoryUpdated"/>. Handlers should treat <see cref="SlotCount"/> as the
        /// source of truth.
        /// </remarks>
        public event SlotDelegate OnSlotChanged;

        /// <summary>
        /// Raised exactly once at the end of any operation that changed the inventory. Bulk
        /// operations and a caller's <see cref="BeginBatchUpdate"/> scope raise it once, not once
        /// per slot.
        /// </summary>
        public event Action OnInventoryUpdated;

        /// <summary>
        /// Constructs an inventory.
        /// </summary>
        /// <param name="maxSlotCount">Fixed slot count, or null for an unbounded, self-compacting inventory.</param>
        /// <param name="allowDuplicates">
        /// When false, at most one slot may hold any given <see cref="IItemAttributes.UniqueId"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSlotCount"/> is zero or negative.</exception>
        public Inventory(int? maxSlotCount = null, bool allowDuplicates = true)
        {
            if (maxSlotCount is <= 0) throw new ArgumentOutOfRangeException(nameof(maxSlotCount));

            MaxSlotCount = maxSlotCount;
            AllowDuplicates = allowDuplicates;
            _slots = new List<ItemStack<TItem>>();
            Slots = _slots.AsReadOnly();

            if (maxSlotCount.HasValue)
            {
                for (var i = 0; i < maxSlotCount.Value; i++) _slots.Add(new ItemStack<TItem>());
            }
        }

        /// <summary>
        /// Fixed slot count, or null when unbounded.
        /// </summary>
        public int? MaxSlotCount { get; }

        /// <summary>
        /// When false, at most one slot may hold any given <see cref="IItemAttributes.UniqueId"/>.
        /// Because a slot and a stack are the same thing here, this means one <i>stack</i> per kind:
        /// a stackable item may still fill that single stack to its <see cref="Item.MaxStackSize"/>,
        /// but may never overflow into a second slot.
        /// </summary>
        public bool AllowDuplicates { get; }

        /// <summary>
        /// True when <see cref="MaxSlotCount"/> is set.
        /// </summary>
        public bool IsFixedSize => MaxSlotCount.HasValue;

        /// <summary>
        /// Live read-only view of every slot in index order. In fixed-size mode this includes empty
        /// slots and always has <see cref="MaxSlotCount"/> entries; in unbounded mode it contains no
        /// empty slots.
        /// </summary>
        public IReadOnlyList<ItemStack<TItem>> Slots { get; }

        /// <summary>
        /// Gets the slot at <paramref name="index"/>. Never null; may be empty.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public ItemStack<TItem> this[int index]
        {
            get
            {
                EnsureValidIndex(index);
                return _slots[index];
            }
        }

        /// <summary>
        /// Number of slots that currently exist. Equals <see cref="MaxSlotCount"/> when fixed size.
        /// </summary>
        public int SlotCount => _slots.Count;

        /// <summary>
        /// Number of slots holding an item.
        /// </summary>
        public int FilledSlotCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].IsEmpty) count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Number of empty slots, or null when unbounded.
        /// </summary>
        public int? FreeSlotCount => IsFixedSize ? MaxSlotCount!.Value - FilledSlotCount : null;

        /// <summary>
        /// True when no slot holds an item.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].IsEmpty) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// True when no <i>slot</i> is free. Always false when unbounded. A full inventory may still
        /// accept units of an item that stacks with an existing partial stack, so gate adds on
        /// <see cref="CanAdd"/> or <see cref="GetFreeSpaceFor"/>, never on this.
        /// </summary>
        public bool IsFull => IsFixedSize && FilledSlotCount >= MaxSlotCount!.Value;

        /// <summary>
        /// Total units held across all stacks.
        /// </summary>
        public int TotalQuantity
        {
            get
            {
                long total = 0;
                for (var i = 0; i < _slots.Count; i++) total += _slots[i].Quantity;
                return (int)Math.Min(total, int.MaxValue);
            }
        }

        /// <summary>
        /// Whether <paramref name="index"/> addresses an existing slot.
        /// </summary>
        public bool IsValidIndex(int index) => index >= 0 && index < _slots.Count;

        /// <summary>
        /// Units of <paramref name="item"/> that could be added right now, honoring stacking, slot
        /// capacity, <see cref="AllowDuplicates"/>, and <see cref="CanAcceptItem"/>. Returns
        /// <see cref="int.MaxValue"/> when unbounded and accepted, and 0 for a null or rejected item.
        /// </summary>
        /// <remarks><see cref="TryAdd(TItem,int)"/> succeeds if and only if its quantity is positive
        /// and at most this value (plus the non-stackable rule described there).</remarks>
        public int GetFreeSpaceFor(TItem item)
        {
            if (item == null || !CanAcceptItem(item)) return 0;

            // A non-stackable instance may occupy only one slot: a second slot holding the same
            // reference would share one InstanceId, and lookup/removal/save by instance would only
            // ever see the first.
            if (item.MaxStackSize <= 1 && IndexOf(item) >= 0) return 0;

            long existingSpace = 0;
            var hasExistingStackOfKind = false;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty) continue;
                if (!string.Equals(slot.Item.UniqueId, item.UniqueId, StringComparison.Ordinal)) continue;

                hasExistingStackOfKind = true;
                if (item.CanStackWith(slot.Item)) existingSpace += slot.RemainingSpace;
            }

            if (!AllowDuplicates)
            {
                if (hasExistingStackOfKind) return (int)Math.Min(existingSpace, int.MaxValue);
                if (IsFixedSize && FreeSlotCount <= 0) return 0;
                return item.MaxStackSize;
            }

            if (!IsFixedSize) return int.MaxValue;

            var newSlotsSpace = (long)FreeSlotCount.GetValueOrDefault() * item.MaxStackSize;
            return (int)Math.Min(existingSpace + newSlotsSpace, int.MaxValue);
        }

        /// <summary>
        /// Whether <see cref="TryAdd(TItem,int)"/> would succeed with these arguments.
        /// </summary>
        public bool CanAdd(TItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            if (item.MaxStackSize <= 1 && quantity > 1) return false;
            return quantity <= GetFreeSpaceFor(item);
        }

        /// <summary>
        /// Adds exactly <paramref name="quantity"/> units of <paramref name="item"/>, or nothing.
        /// </summary>
        /// <returns>True when the full quantity was added.</returns>
        /// <remarks>
        /// Fills partial stacks of matching items in ascending slot order first, then consumes empty
        /// slots. Atomic: if the full quantity does not fit, no slot is touched and no event fires.
        /// <para>
        /// A non-stackable item (<see cref="Item.MaxStackSize"/> of 1) is rejected when
        /// <paramref name="quantity"/> exceeds 1, because each such slot needs its own instance and
        /// this method has only one to give. Use <see cref="AddRange"/> with distinct instances.
        /// For the same reason, a non-stackable instance that is already held is rejected: one
        /// object reference may occupy at most one slot.
        /// </para>
        /// </remarks>
        public bool TryAdd(TItem item, int quantity = 1) => TryAdd(item, quantity, out _);

        /// <param name="remainder">0 on success; the full requested quantity on failure.</param>
        /// <inheritdoc cref="TryAdd(TItem,int)"/>
        public bool TryAdd(TItem item, int quantity, out int remainder)
        {
            remainder = quantity;
            if (!CanAdd(item, quantity)) return false;

            using (OpenBatch())
            {
                ApplyAdd(item, quantity);
            }

            remainder = 0;
            return true;
        }

        /// <summary>
        /// Adds as many of <paramref name="quantity"/> units as fit. Adds at most 1 unit of a
        /// non-stackable item, for the reason given on <see cref="TryAdd(TItem,int)"/>.
        /// </summary>
        /// <returns>Units actually added, 0 to <paramref name="quantity"/>.</returns>
        public int AddUpTo(TItem item, int quantity)
        {
            if (item == null || quantity <= 0 || !CanAcceptItem(item)) return 0;

            var effectiveQuantity = item.MaxStackSize <= 1 ? Math.Min(quantity, 1) : quantity;
            var available = GetFreeSpaceFor(item);
            var toAdd = (int)Math.Min((long)effectiveQuantity, (long)available);
            if (toAdd <= 0) return 0;

            using (OpenBatch())
            {
                ApplyAdd(item, toAdd);
            }

            return toAdd;
        }

        /// <summary>
        /// Adds one unit of each item in <paramref name="items"/>, best effort, in sequence order, as
        /// a single batch. Null entries are skipped and reported in <paramref name="notAdded"/>.
        /// </summary>
        /// <param name="notAdded">Items that did not fit. Empty when every item was added.</param>
        /// <returns>Number of items added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
        public int AddRange(IEnumerable<TItem> items, out IReadOnlyList<TItem> notAdded)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var failed = new List<TItem>();
            var addedCount = 0;

            using (OpenBatch())
            {
                foreach (var item in items)
                {
                    if (item != null && TryAdd(item))
                    {
                        addedCount++;
                    }
                    else
                    {
                        failed.Add(item);
                    }
                }
            }

            notAdded = failed;
            return addedCount;
        }

        /// <summary>
        /// Whether <see cref="TryRemove(TItem,int)"/> would succeed with these arguments.
        /// </summary>
        public bool CanRemove(TItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            return GetQuantity(item) >= quantity;
        }

        /// <summary>
        /// Whether <see cref="TryRemove(string,int)"/> would succeed with these arguments.
        /// </summary>
        public bool CanRemove(string uniqueId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(uniqueId) || quantity <= 0) return false;
            return GetQuantity(uniqueId) >= quantity;
        }

        /// <summary>
        /// Removes exactly <paramref name="quantity"/> units matching <paramref name="item"/>'s
        /// <see cref="IItemAttributes.UniqueId"/>, or nothing. Draining proceeds in ascending slot
        /// order. Matches by kind, not by reference - use <see cref="TryRemoveInstance"/> for that.
        /// </summary>
        /// <returns>True when the full quantity was removed.</returns>
        public bool TryRemove(TItem item, int quantity = 1) => TryRemove(item, quantity, out _);

        /// <param name="remainder">0 on success; the full requested quantity on failure.</param>
        /// <inheritdoc cref="TryRemove(TItem,int)"/>
        public bool TryRemove(TItem item, int quantity, out int remainder)
        {
            remainder = quantity;
            if (item == null) return false;
            return TryRemove(item.UniqueId, quantity, out remainder);
        }

        /// <summary>
        /// As <see cref="TryRemove(TItem,int)"/>, selecting the kind directly by
        /// <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public bool TryRemove(string uniqueId, int quantity = 1) => TryRemove(uniqueId, quantity, out _);

        /// <inheritdoc cref="TryRemove(TItem,int,out int)"/>
        public bool TryRemove(string uniqueId, int quantity, out int remainder)
        {
            remainder = quantity;
            if (string.IsNullOrEmpty(uniqueId) || quantity <= 0) return false;
            if (GetQuantity(uniqueId) < quantity) return false;

            using (OpenBatch())
            {
                ApplyRemoveByKind(uniqueId, quantity);
            }

            remainder = 0;
            return true;
        }

        /// <summary>
        /// Removes as many of <paramref name="quantity"/> units as are held.
        /// </summary>
        /// <returns>Units actually removed.</returns>
        public int RemoveUpTo(TItem item, int quantity) => item == null ? 0 : RemoveUpTo(item.UniqueId, quantity);

        /// <inheritdoc cref="RemoveUpTo(TItem,int)"/>
        public int RemoveUpTo(string uniqueId, int quantity)
        {
            if (string.IsNullOrEmpty(uniqueId) || quantity <= 0) return 0;

            var toRemove = Math.Min(quantity, GetQuantity(uniqueId));
            if (toRemove <= 0) return 0;

            using (OpenBatch())
            {
                ApplyRemoveByKind(uniqueId, toRemove);
            }

            return toRemove;
        }

        /// <summary>
        /// Removes the entire stack holding this exact object reference. Does nothing if the
        /// instance is not held.
        /// </summary>
        public bool TryRemoveInstance(TItem item)
        {
            if (item == null) return false;
            var index = IndexOf(item);
            if (index < 0) return false;

            using (OpenBatch())
            {
                var slot = _slots[index];
                var removedItem = slot.Item;
                var removedQuantity = slot.Quantity;
                slot.Clear();

                if (IsFixedSize)
                {
                    MarkSlotChanged(index);
                }
                else
                {
                    RemoveUnboundedSlot(index);
                }

                EnqueueItemRemoved(removedItem, removedQuantity);
            }

            return true;
        }

        /// <summary>
        /// Removes the entire stack at <paramref name="index"/>.
        /// </summary>
        /// <param name="item">The item removed, or null when the slot was empty.</param>
        /// <param name="quantity">Units removed, or 0 when the slot was empty.</param>
        /// <returns>False when the slot was already empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryRemoveAt(int index, out TItem item, out int quantity)
        {
            EnsureValidIndex(index);
            var slot = _slots[index];
            if (slot.IsEmpty)
            {
                item = null;
                quantity = 0;
                return false;
            }

            item = slot.Item;
            quantity = slot.Quantity;

            using (OpenBatch())
            {
                slot.Clear();

                if (IsFixedSize)
                {
                    MarkSlotChanged(index);
                }
                else
                {
                    RemoveUnboundedSlot(index);
                }

                EnqueueItemRemoved(item, quantity);
            }

            return true;
        }

        /// <summary>
        /// Removes exactly <paramref name="quantity"/> units from the stack at
        /// <paramref name="index"/>, clearing the slot if that empties it. Atomic: fails without
        /// mutating when the slot holds fewer units than requested.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryRemoveAt(int index, int quantity)
        {
            EnsureValidIndex(index);
            if (quantity <= 0) return false;

            var slot = _slots[index];
            if (slot.IsEmpty || slot.Quantity < quantity) return false;

            using (OpenBatch())
            {
                RemoveFromSlot(index, quantity);
            }

            return true;
        }

        /// <summary>
        /// Places <paramref name="quantity"/> units of <paramref name="item"/> into the slot at
        /// exactly <paramref name="index"/>, or does nothing. The restore-from-save primitive: it
        /// lets a fixed-size inventory be rebuilt slot by slot at the saved positions.
        /// </summary>
        /// <returns>
        /// True when the units were placed. False when the item is null or rejected by
        /// <see cref="CanAcceptItem"/>, <paramref name="quantity"/> is not positive, or:
        /// <list type="bullet">
        /// <item>the slot is empty and the quantity exceeds <see cref="Item.MaxStackSize"/>, the
        /// kind is already held while <see cref="AllowDuplicates"/> is false, or a non-stackable
        /// instance is already held;</item>
        /// <item>the slot is occupied and the item cannot stack with it, or the stack lacks room
        /// for the full quantity.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Atomic. Raises <see cref="OnItemAdded"/>, <see cref="OnSlotChanged"/> for
        /// <paramref name="index"/>, and <see cref="OnInventoryUpdated"/> on success. An unbounded
        /// inventory holds no empty slots, so only topping up an existing stack can succeed there;
        /// rebuild one by calling <see cref="TryAdd(TItem,int)"/> in saved order instead.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryAddAt(int index, TItem item, int quantity = 1)
        {
            EnsureValidIndex(index);
            if (item == null || quantity <= 0 || !CanAcceptItem(item)) return false;

            var slot = _slots[index];

            if (slot.IsEmpty)
            {
                if (quantity > item.MaxStackSize) return false;
                if (item.MaxStackSize <= 1 && IndexOf(item) >= 0) return false;
                if (!AllowDuplicates && IndexOf(item.UniqueId) >= 0) return false;

                using (OpenBatch())
                {
                    slot.Set(item, quantity);
                    MarkSlotChanged(index);
                    EnqueueItemAdded(item, quantity);
                }

                return true;
            }

            if (!item.CanStackWith(slot.Item)) return false;
            if (!string.Equals(slot.Item.UniqueId, item.UniqueId, StringComparison.Ordinal)) return false;
            if (slot.RemainingSpace < quantity) return false;

            using (OpenBatch())
            {
                slot.Add(quantity);
                MarkSlotChanged(index);
                EnqueueItemAdded(item, quantity);
            }

            return true;
        }

        /// <summary>
        /// Moves the entire stack at <paramref name="index"/> into <paramref name="target"/>, or
        /// moves nothing.
        /// </summary>
        /// <inheritdoc cref="TryTransferTo(Inventory{TItem},int,int)"/>
        public bool TryTransferTo(Inventory<TItem> target, int index)
        {
            ValidateTransfer(target, index);
            return TryTransferTo(target, index, _slots[index].Quantity);
        }

        /// <summary>
        /// Moves exactly <paramref name="quantity"/> units out of the stack at
        /// <paramref name="index"/> and into <paramref name="target"/>, or moves nothing.
        /// </summary>
        /// <returns>
        /// True when the full quantity moved. False when the slot is empty, holds fewer units than
        /// requested, <paramref name="quantity"/> is not positive, or the target would not accept
        /// the units (see <see cref="CanAdd"/>: stacking, capacity, <see cref="AllowDuplicates"/>
        /// and <see cref="CanAcceptItem"/> are all the target's own rules).
        /// </returns>
        /// <remarks>
        /// Atomic across both inventories: on failure neither changes and neither raises an event.
        /// On success the target raises <see cref="OnItemAdded"/> and the source raises
        /// <see cref="OnItemRemoved"/>, each followed by its own slot and update events. The target's
        /// events are raised first, but both inventories are already fully updated by then. A stackable item moved in part leaves the same object reference
        /// in both inventories, which is safe because stackable items are fungible.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="target"/> is this inventory; use <see cref="Move"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryTransferTo(Inventory<TItem> target, int index, int quantity)
        {
            ValidateTransfer(target, index);
            if (quantity <= 0) return false;

            var slot = _slots[index];
            if (slot.IsEmpty || slot.Quantity < quantity) return false;

            var item = slot.Item;
            if (!target.CanAdd(item, quantity)) return false;

            ApplyTransfer(target, index, item, quantity);
            return true;
        }

        /// <summary>
        /// Moves as many of <paramref name="quantity"/> units out of the stack at
        /// <paramref name="index"/> into <paramref name="target"/> as the target will take. Events
        /// and sharing rules are as for <see cref="TryTransferTo(Inventory{TItem},int,int)"/>.
        /// </summary>
        /// <returns>Units actually moved, 0 to <paramref name="quantity"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="target"/> is this inventory; use <see cref="Move"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public int TransferUpTo(Inventory<TItem> target, int index, int quantity)
        {
            ValidateTransfer(target, index);
            if (quantity <= 0) return 0;

            var slot = _slots[index];
            if (slot.IsEmpty) return 0;

            var item = slot.Item;
            var wanted = Math.Min(quantity, slot.Quantity);
            var toMove = (int)Math.Min((long)wanted, (long)target.GetFreeSpaceFor(item));
            if (toMove <= 0) return 0;

            ApplyTransfer(target, index, item, toMove);
            return toMove;
        }

        private void ValidateTransfer(Inventory<TItem> target, int index)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (ReferenceEquals(target, this))
            {
                throw new ArgumentException("Cannot transfer to the same inventory; use Move.", nameof(target));
            }

            EnsureValidIndex(index);
        }

        private void ApplyTransfer(Inventory<TItem> target, int index, TItem item, int quantity)
        {
            // Both inventories are fully updated before either raises an event, so no handler can
            // observe the units in both places or mutate the source mid-transfer. The target is
            // the inner scope, so its events still flush before the source's.
            using (OpenBatch())
            using (target.OpenBatch())
            {
                target.ApplyAdd(item, quantity);
                RemoveFromSlot(index, quantity);
            }
        }

        /// <summary>
        /// Removes <paramref name="quantity"/> units from the occupied slot at <paramref name="index"/>,
        /// dropping the slot when it empties in unbounded mode. Caller guarantees the slot holds
        /// at least that many units and has opened a batch.
        /// </summary>
        private void RemoveFromSlot(int index, int quantity)
        {
            var slot = _slots[index];
            var item = slot.Item;
            slot.Remove(quantity);

            if (slot.IsEmpty && !IsFixedSize)
            {
                RemoveUnboundedSlot(index);
            }
            else
            {
                MarkSlotChanged(index);
            }

            EnqueueItemRemoved(item, quantity);
        }

        /// <summary>
        /// Empties every slot. Fixed-size inventories keep their slot count; unbounded inventories
        /// drop to zero slots. Raises <see cref="OnItemRemoved"/> once per cleared stack and
        /// <see cref="OnInventoryUpdated"/> once. Does nothing and fires nothing when already empty.
        /// </summary>
        public void Clear()
        {
            if (IsEmpty) return;

            using (OpenBatch())
            {
                for (var i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    if (slot.IsEmpty) continue;

                    var item = slot.Item;
                    var quantity = slot.Quantity;
                    slot.Clear();
                    EnqueueItemRemoved(item, quantity);

                    if (IsFixedSize) MarkSlotChanged(i);
                }

                if (!IsFixedSize) _slots.Clear();
            }
        }

        /// <summary>
        /// The items held, in slot order, skipping empty slots. Equivalent to iterating the
        /// inventory directly with <c>foreach</c>.
        /// </summary>
        /// <remarks>
        /// Like <see cref="List{T}"/>, enumeration throws <see cref="InvalidOperationException"/> if
        /// the inventory is modified while it is in progress. Collect what you want to change first
        /// (for example <c>inventory.FindAll(...).ToList()</c>), then mutate.
        /// </remarks>
        public IEnumerable<TItem> Items => EnumerateItems();

        private IEnumerable<TItem> EnumerateItems()
        {
            var version = _version;
            var index = 0;

            while (true)
            {
                if (version != _version)
                {
                    throw new InvalidOperationException("The inventory was modified during enumeration.");
                }

                if (index >= _slots.Count) yield break;

                var slot = _slots[index++];
                if (!slot.IsEmpty) yield return slot.Item;
            }
        }

        /// <summary>
        /// Whether this exact object reference occupies a slot.
        /// </summary>
        public bool Contains(TItem item) => item != null && IndexOf(item) >= 0;

        /// <summary>
        /// Whether any slot holds an item with this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public bool Contains(string uniqueId) => !string.IsNullOrEmpty(uniqueId) && IndexOf(uniqueId) >= 0;

        /// <summary>
        /// Whether any held item satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool Contains(Func<TItem, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            foreach (var item in Items)
            {
                if (predicate(item)) return true;
            }

            return false;
        }

        /// <summary>
        /// Total units held whose <see cref="IItemAttributes.UniqueId"/> matches <paramref name="item"/>'s.
        /// </summary>
        public int GetQuantity(TItem item) => item == null ? 0 : GetQuantity(item.UniqueId);

        /// <summary>
        /// Total units held with this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public int GetQuantity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return 0;

            long total = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && string.Equals(slot.Item.UniqueId, uniqueId, StringComparison.Ordinal))
                {
                    total += slot.Quantity;
                }
            }

            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>
        /// Total units held in stacks whose item satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public int GetQuantity(Func<TItem, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            long total = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && predicate(slot.Item)) total += slot.Quantity;
            }

            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>
        /// Number of occupied slots holding this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public int GetSlotCount(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return 0;

            var count = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && string.Equals(slot.Item.UniqueId, uniqueId, StringComparison.Ordinal)) count++;
            }

            return count;
        }

        /// <summary>
        /// Finds the first held item satisfying <paramref name="predicate"/>, in slot order.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool TryFind(Func<TItem, bool> predicate, out TItem item)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            foreach (var candidate in Items)
            {
                if (!predicate(candidate)) continue;
                item = candidate;
                return true;
            }

            item = null;
            return false;
        }

        /// <summary>
        /// All held items satisfying <paramref name="predicate"/>, lazily, in slot order.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public IEnumerable<TItem> FindAll(Func<TItem, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return FindAllIterator(predicate);
        }

        private IEnumerable<TItem> FindAllIterator(Func<TItem, bool> predicate)
        {
            foreach (var item in Items)
            {
                if (predicate(item)) yield return item;
            }
        }

        /// <summary>
        /// Finds the held item with this <see cref="Item.InstanceId"/>.
        /// </summary>
        public bool TryFindByInstanceId(Guid instanceId, out TItem item)
        {
            foreach (var candidate in Items)
            {
                if (candidate.InstanceId != instanceId) continue;
                item = candidate;
                return true;
            }

            item = null;
            return false;
        }

        /// <summary>
        /// Slot index of this exact object reference, or -1.
        /// </summary>
        public int IndexOf(TItem item)
        {
            if (item == null) return -1;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && ReferenceEquals(slot.Item, item)) return i;
            }

            return -1;
        }

        /// <summary>
        /// Lowest slot index holding this <see cref="IItemAttributes.UniqueId"/>, or -1.
        /// </summary>
        public int IndexOf(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return -1;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && string.Equals(slot.Item.UniqueId, uniqueId, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        /// <summary>
        /// Slot index of the item with this <see cref="Item.InstanceId"/>, or -1.
        /// </summary>
        public int IndexOfInstance(Guid instanceId)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && slot.Item.InstanceId == instanceId) return i;
            }

            return -1;
        }

        /// <summary>
        /// Lowest slot index whose item satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool TryGetSlotIndex(Func<TItem, bool> predicate, out int index)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && predicate(slot.Item))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Enumerates the items held, in slot order, skipping empty slots.
        /// </summary>
        public IEnumerator<TItem> GetEnumerator() => Items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Exchanges the contents of two slots. Returns false, changing nothing, for equal indices
        /// or when both slots are empty. Slot objects are not replaced, so UI bindings survive.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool Swap(int indexA, int indexB)
        {
            EnsureValidIndex(indexA);
            EnsureValidIndex(indexB);
            if (indexA == indexB) return false;

            var slotA = _slots[indexA];
            var slotB = _slots[indexB];
            if (slotA.IsEmpty && slotB.IsEmpty) return false;

            using (OpenBatch())
            {
                slotA.SwapWith(slotB);
                MarkSlotChanged(indexA);
                MarkSlotChanged(indexB);
            }

            return true;
        }

        /// <summary>
        /// Moves the stack at <paramref name="fromIndex"/> to <paramref name="toIndex"/>. The
        /// drag-and-drop primitive. Three cases: an empty destination receives the stack; a
        /// destination holding a stackable match with room absorbs as much as fits, leaving any
        /// remainder in the source; any other occupied destination is swapped with.
        /// </summary>
        /// <returns>False for equal indices or an empty source; true for every other call.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool Move(int fromIndex, int toIndex)
        {
            EnsureValidIndex(fromIndex);
            EnsureValidIndex(toIndex);
            if (fromIndex == toIndex) return false;

            var source = _slots[fromIndex];
            if (source.IsEmpty) return false;

            var destination = _slots[toIndex];

            using (OpenBatch())
            {
                var sourceRemoved = false;

                if (destination.IsEmpty)
                {
                    destination.Set(source.Item, source.Quantity);
                    source.Clear();
                }
                else if (source.Item.CanStackWith(destination.Item) && destination.RemainingSpace > 0)
                {
                    var moved = destination.Add(source.Quantity);
                    source.Remove(moved);
                    sourceRemoved = source.IsEmpty && !IsFixedSize;
                }
                else
                {
                    source.SwapWith(destination);
                }

                if (sourceRemoved)
                {
                    RemoveUnboundedSlot(fromIndex);
                    MarkSlotChanged(toIndex > fromIndex ? toIndex - 1 : toIndex);
                }
                else
                {
                    MarkSlotChanged(fromIndex);
                    MarkSlotChanged(toIndex);
                }
            }

            return true;
        }

        /// <summary>
        /// Moves <paramref name="quantity"/> units out of the stack at <paramref name="index"/> into
        /// the first empty slot (fixed size) or a newly appended slot (unbounded).
        /// </summary>
        /// <returns>
        /// False when the quantity is non-positive, not strictly less than the stack's quantity
        /// (at least one unit must remain), or above the item's <see cref="Item.MaxStackSize"/>, the
        /// slot is empty, no slot is available, or
        /// <see cref="AllowDuplicates"/> is false - splitting would create a second stack of one kind.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool SplitStack(int index, int quantity)
        {
            EnsureValidIndex(index);
            if (!AllowDuplicates || quantity <= 0) return false;

            var slot = _slots[index];
            if (slot.IsEmpty || slot.Item.MaxStackSize <= 1 || quantity >= slot.Quantity) return false;
            if (quantity > slot.Item.MaxStackSize) return false;

            var destinationIndex = FindOrCreateEmptySlotIndex();
            return destinationIndex >= 0 && SplitStackInternal(index, quantity, destinationIndex);
        }

        /// <summary>
        /// As <see cref="SplitStack(int,int)"/>, but into a specific slot, which must be empty.
        /// Unbounded inventories hold no empty slots, so this always returns false there; use
        /// <see cref="SplitStack(int,int)"/> instead.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool SplitStack(int index, int quantity, int destinationIndex)
        {
            EnsureValidIndex(index);
            EnsureValidIndex(destinationIndex);
            if (!AllowDuplicates || quantity <= 0 || index == destinationIndex) return false;

            var slot = _slots[index];
            if (slot.IsEmpty || slot.Item.MaxStackSize <= 1 || quantity >= slot.Quantity) return false;
            if (quantity > slot.Item.MaxStackSize) return false;
            if (!_slots[destinationIndex].IsEmpty) return false;

            return SplitStackInternal(index, quantity, destinationIndex);
        }

        private bool SplitStackInternal(int index, int quantity, int destinationIndex)
        {
            using (OpenBatch())
            {
                var slot = _slots[index];
                var item = slot.Item;
                slot.Remove(quantity);
                _slots[destinationIndex].Set(item, quantity);
                MarkSlotChanged(index);
                MarkSlotChanged(destinationIndex);
            }

            return true;
        }

        /// <summary>
        /// Merges the stack at <paramref name="sourceIndex"/> into the one at
        /// <paramref name="destinationIndex"/>, filling the destination to its stack limit and
        /// leaving any remainder in the source.
        /// </summary>
        /// <returns>
        /// False when the indices are equal, either slot is empty, the items do not
        /// <see cref="Item.CanStackWith"/> each other, or the destination is already full.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool MergeStacks(int sourceIndex, int destinationIndex)
        {
            EnsureValidIndex(sourceIndex);
            EnsureValidIndex(destinationIndex);
            if (sourceIndex == destinationIndex) return false;

            var source = _slots[sourceIndex];
            var destination = _slots[destinationIndex];
            if (source.IsEmpty || destination.IsEmpty) return false;
            if (!source.Item.CanStackWith(destination.Item) || destination.IsFull) return false;

            using (OpenBatch())
            {
                var moved = destination.Add(source.Quantity);
                source.Remove(moved);

                var sourceRemoved = source.IsEmpty && !IsFixedSize;
                var effectiveDestinationIndex = destinationIndex;

                if (sourceRemoved)
                {
                    RemoveUnboundedSlot(sourceIndex);
                    if (destinationIndex > sourceIndex) effectiveDestinationIndex--;
                }
                else
                {
                    MarkSlotChanged(sourceIndex);
                }

                MarkSlotChanged(effectiveDestinationIndex);
            }

            return true;
        }

        /// <summary>
        /// Merges every set of stackable like stacks into as few slots as possible, lowest index
        /// first. One batch. No-op when <see cref="AllowDuplicates"/> is false.
        /// </summary>
        /// <returns>Number of slots freed.</returns>
        public int Consolidate()
        {
            if (!AllowDuplicates) return 0;

            var freedSlots = 0;

            using (OpenBatch())
            {
                for (var i = 0; i < _slots.Count; i++)
                {
                    var target = _slots[i];
                    if (target.IsEmpty || target.IsFull) continue;

                    for (var j = i + 1; j < _slots.Count && target.RemainingSpace > 0; j++)
                    {
                        var candidate = _slots[j];
                        if (candidate.IsEmpty || !candidate.Item.CanStackWith(target.Item)) continue;

                        var moved = target.Add(candidate.Quantity);
                        if (moved <= 0) continue;

                        candidate.Remove(moved);
                        MarkSlotChanged(i);

                        if (!candidate.IsEmpty)
                        {
                            MarkSlotChanged(j);
                            continue;
                        }

                        freedSlots++;

                        if (IsFixedSize)
                        {
                            MarkSlotChanged(j);
                        }
                        else
                        {
                            RemoveUnboundedSlot(j);
                            j--;
                        }
                    }
                }
            }

            return freedSlots;
        }

        /// <summary>
        /// Reorders occupied slots by <paramref name="comparer"/>, pushing empty slots to the end.
        /// Stable, so equally ranked items keep their relative order. Does not merge stacks - call
        /// <see cref="Consolidate"/> first if you want that. One batch.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
        public void Sort(IComparer<TItem> comparer)
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            Sort((a, b) => comparer.Compare(a, b));
        }

        /// <inheritdoc cref="Sort(IComparer{TItem})"/>
        public void Sort(Comparison<TItem> comparison)
        {
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            var occupied = new List<(TItem Item, int Quantity, int OriginalIndex)>();
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty) occupied.Add((slot.Item, slot.Quantity, i));
            }

            if (occupied.Count == 0) return;

            occupied.Sort((a, b) =>
            {
                var cmp = comparison(a.Item, b.Item);
                return cmp != 0 ? cmp : a.OriginalIndex.CompareTo(b.OriginalIndex);
            });

            using (OpenBatch())
            {
                for (var i = 0; i < occupied.Count; i++)
                {
                    var (item, quantity, originalIndex) = occupied[i];
                    if (originalIndex == i) continue;

                    MarkSlotChanged(i);
                    _slots[i].Set(item, quantity);
                }

                for (var i = occupied.Count; i < _slots.Count; i++)
                {
                    if (_slots[i].IsEmpty) continue;
                    _slots[i].Clear();
                    MarkSlotChanged(i);
                }
            }
        }

        /// <summary>
        /// Shifts occupied slots to the lowest indices, preserving their relative order, so empty
        /// slots are all at the end. One batch. Always a no-op returning false in unbounded mode,
        /// which is compact by construction.
        /// </summary>
        /// <returns>True when at least one stack moved.</returns>
        public bool Compact()
        {
            if (!IsFixedSize) return false;

            var writeIndex = 0;
            var changed = false;

            using (OpenBatch())
            {
                for (var readIndex = 0; readIndex < _slots.Count; readIndex++)
                {
                    var slot = _slots[readIndex];
                    if (slot.IsEmpty) continue;

                    if (readIndex != writeIndex)
                    {
                        _slots[writeIndex].Set(slot.Item, slot.Quantity);
                        slot.Clear();
                        MarkSlotChanged(writeIndex);
                        MarkSlotChanged(readIndex);
                        changed = true;
                    }

                    writeIndex++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Suppresses and coalesces events until the returned handle is disposed, so a run of
        /// operations reports as one change. Reentrant: only the outermost scope flushes.
        /// </summary>
        public IDisposable BeginBatchUpdate() => new BatchScope(this);

        /// <summary>
        /// Whether this inventory accepts <paramref name="item"/> at all, regardless of space.
        /// Returns true by default; override for filtered, item-specific containers.
        /// Consulted by <see cref="CanAdd"/> and <see cref="GetFreeSpaceFor"/>.
        /// </summary>
        protected virtual bool CanAcceptItem(TItem item) => item != null;

        private void EnsureValidIndex(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Removes the slot at <paramref name="index"/> (unbounded mode only) and marks every slot
        /// that shifted down into its place as changed.
        /// </summary>
        private void RemoveUnboundedSlot(int index)
        {
            _version++;
            _slots.RemoveAt(index);
            for (var j = index; j < _slots.Count; j++) MarkSlotChanged(j);
        }

        private int FindOrCreateEmptySlotIndex()
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty) return i;
            }

            if (IsFixedSize) return -1;

            _slots.Add(new ItemStack<TItem>());
            return _slots.Count - 1;
        }

        private void ApplyAdd(TItem item, int quantity)
        {
            var remaining = quantity;

            for (var i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || !item.CanStackWith(slot.Item)) continue;
                if (!string.Equals(slot.Item.UniqueId, item.UniqueId, StringComparison.Ordinal)) continue;

                var added = slot.Add(remaining);
                if (added <= 0) continue;

                remaining -= added;
                MarkSlotChanged(i);
            }

            var blockedByExistingKind = !AllowDuplicates && IndexOf(item.UniqueId) >= 0;

            while (remaining > 0 && !blockedByExistingKind)
            {
                var index = FindOrCreateEmptySlotIndex();
                if (index < 0) break;

                var toAdd = Math.Min(remaining, item.MaxStackSize);
                _slots[index].Set(item, toAdd);
                remaining -= toAdd;
                MarkSlotChanged(index);

                blockedByExistingKind = !AllowDuplicates;
            }

            EnqueueItemAdded(item, quantity);
        }

        private void ApplyRemoveByKind(string uniqueId, int quantity)
        {
            var remaining = quantity;
            TItem removedItemSample = null;

            for (var i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || !string.Equals(slot.Item.UniqueId, uniqueId, StringComparison.Ordinal)) continue;

                removedItemSample ??= slot.Item;
                var removed = slot.Remove(remaining);
                remaining -= removed;

                if (slot.IsEmpty && !IsFixedSize)
                {
                    RemoveUnboundedSlot(i);
                    i--;
                }
                else
                {
                    MarkSlotChanged(i);
                }
            }

            if (removedItemSample != null) EnqueueItemRemoved(removedItemSample, quantity);
        }

        private void EnqueueItemAdded(TItem item, int quantity)
        {
            _version++;
            _pendingItemEvents.Add(new PendingItemEvent(true, item, quantity));
            _pendingInventoryUpdated = true;
        }

        private void EnqueueItemRemoved(TItem item, int quantity)
        {
            _version++;
            _pendingItemEvents.Add(new PendingItemEvent(false, item, quantity));
            _pendingInventoryUpdated = true;
        }

        private void MarkSlotChanged(int index)
        {
            _version++;

            // Duplicates are collapsed at flush time; skipping an immediate repeat keeps hot loops small.
            var pending = _pendingSlots;
            if (pending.Count == 0 || pending[pending.Count - 1] != index) pending.Add(index);
            _pendingInventoryUpdated = true;
        }

        private void FlushPending()
        {
            var events = _pendingItemEvents;
            var slots = _pendingSlots;
            var updated = _pendingInventoryUpdated;
            _pendingInventoryUpdated = false;

            if (events.Count == 0 && slots.Count == 0 && !updated) return;

            // Hand fresh (or recycled) buffers to the inventory before raising anything, so a
            // handler that mutates the inventory (re-entrancy) or throws cannot corrupt or leak
            // this flush. The buffers being raised from are recycled once the flush ends.
            _pendingItemEvents = _spareItemEvents ?? new List<PendingItemEvent>();
            _spareItemEvents = null;
            _pendingSlots = _spareSlots ?? new List<int>();
            _spareSlots = null;

            try
            {
                for (var i = 0; i < events.Count; i++)
                {
                    var pending = events[i];
                    if (pending.Added) OnItemAdded?.Invoke(pending.Item, pending.Quantity);
                    else OnItemRemoved?.Invoke(pending.Item, pending.Quantity);
                }

                if (slots.Count > 0)
                {
                    slots.Sort();
                    var previous = -1;
                    for (var i = 0; i < slots.Count; i++)
                    {
                        var index = slots[i];
                        // Unbounded removals shift slots down, and handlers may remove more, so an
                        // index recorded earlier may no longer exist. Check the live count.
                        if (index == previous || index >= _slots.Count) continue;
                        previous = index;
                        OnSlotChanged?.Invoke(index);
                    }
                }

                if (updated) OnInventoryUpdated?.Invoke();
            }
            finally
            {
                events.Clear();
                slots.Clear();
                _spareItemEvents = events;
                _spareSlots = slots;
            }
        }

        private Batch OpenBatch() => new Batch(this);

        /// <summary>
        /// Allocation-free batch scope for internal operations. Not idempotent - dispose exactly once,
        /// which <c>using</c> guarantees. The public <see cref="BeginBatchUpdate"/> hands out the
        /// idempotent <see cref="BatchScope"/> class instead.
        /// </summary>
        private readonly struct Batch : IDisposable
        {
            private readonly Inventory<TItem> _inventory;

            public Batch(Inventory<TItem> inventory)
            {
                _inventory = inventory;
                inventory._batchDepth++;
            }

            public void Dispose()
            {
                _inventory._batchDepth--;
                if (_inventory._batchDepth == 0) _inventory.FlushPending();
            }
        }

        private sealed class BatchScope : IDisposable
        {
            private readonly Inventory<TItem> _inventory;
            private bool _disposed;

            public BatchScope(Inventory<TItem> inventory)
            {
                _inventory = inventory;
                _inventory._batchDepth++;
            }

            public void Dispose()
            {
                // Idempotent: a second Dispose must not unbalance the depth counter.
                if (_disposed) return;
                _disposed = true;

                _inventory._batchDepth--;
                if (_inventory._batchDepth == 0) _inventory.FlushPending();
            }
        }
    }
}
