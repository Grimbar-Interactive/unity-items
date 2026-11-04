using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// A slot-based container whose slots <i>reference</i> items held by another
    /// <see cref="Inventory{TItem}"/> (the source) instead of owning them. Assigning an item to a
    /// <see cref="LinkedInventory{TItem}"/> does not remove it from the source, which makes this the
    /// right container for loadouts, hotbars and quick-slots.
    /// </summary>
    /// <remarks>
    /// <para><b>Identity.</b> A non-stackable item is referenced by its <see cref="Item.InstanceId"/>,
    /// so two swords with the same <see cref="IItemAttributes.UniqueId"/> can occupy two slots when
    /// <see cref="AllowDuplicates"/> is true. A stackable item is referenced by
    /// <see cref="IItemAttributes.UniqueId"/>: slots claim a quantity of that kind from the source.
    /// The units claimed across all slots of one kind never exceed what the source holds, and one
    /// non-stackable instance fills at most one slot.</para>
    /// <para><b>Stack size.</b> A slot's limit is <see cref="GetMaxStackSize"/>: the item's own
    /// <see cref="Item.MaxStackSize"/> unless the constructor was given a
    /// <c>maxStackSize</c> override or a resolver. Non-stackable items always hold exactly one unit.
    /// </para>
    /// <para><b>Staying in sync.</b> The loadout follows the source through an internal
    /// notification raised after all of the source's public events (and even if one of those
    /// handlers throws), and calls <see cref="Reconcile"/> each time:
    /// slots whose instance left the source are cleared, and stackable claims above what the source
    /// still holds are trimmed, highest slot index first. Trimming raises the normal removal events.
    /// While a caller holds <see cref="Inventory{TItem}.BeginBatchUpdate"/> on the source, the source
    /// raises nothing, so the loadout catches up when that batch ends (or when you call
    /// <see cref="Reconcile"/>). The source holds a reference to the loadout through that
    /// subscription, so call <see cref="Dispose"/> when the loadout is no longer needed.</para>
    /// <para><b>Bounded and unbounded.</b> Exactly as for <see cref="Inventory{TItem}"/>: a null
    /// <see cref="MaxSlotCount"/> grows on demand and holds no empty slots; otherwise that many
    /// slots exist for the lifetime of the loadout and emptied slots keep their index.</para>
    /// <para><b>Atomicity.</b> <c>Try*</c> methods are all-or-nothing; <c>*UpTo</c> methods are
    /// best-effort and report how much they achieved.</para>
    /// <para><b>Known simplification.</b> Stackable items are matched by
    /// <see cref="IItemAttributes.UniqueId"/> alone, so an override of <see cref="Item.CanStackWith"/>
    /// that splits one UniqueId into incompatible stacks in the source is not reflected here.</para>
    /// <para><b>Unity serialization.</b> Not <see cref="SerializableAttribute"/>. Persist a loadout
    /// as a list of (slot index, <see cref="LinkedSlot{TItem}.UniqueId"/> or
    /// <see cref="LinkedSlot{TItem}.InstanceId"/>, quantity) records, and restore it with
    /// <see cref="TryAddAt(int,string,int)"/> after the source has been restored.</para>
    /// </remarks>
    /// <typeparam name="TItem">The item type this loadout references.</typeparam>
    public class LinkedInventory<TItem> : IReadOnlyInventory<TItem>, IDisposable, IInventoryEventSink<TItem>
        where TItem : Item
    {
        private readonly List<LinkedSlot<TItem>> _slots;
        private readonly InventoryEventQueue<TItem> _events;
        private readonly Func<TItem, int> _stackSizeResolver;
        private bool _disposed;

        /// <inheritdoc cref="Inventory{TItem}.OnItemAdded"/>
        public event Inventory<TItem>.ItemQuantityDelegate OnItemAdded;

        /// <inheritdoc cref="Inventory{TItem}.OnItemRemoved"/>
        /// <remarks>
        /// Also raised for units dropped by <see cref="Reconcile"/> when the source lost them: once per
        /// stackable kind with the total trimmed, and once per cleared non-stackable instance.
        /// </remarks>
        public event Inventory<TItem>.ItemQuantityDelegate OnItemRemoved;

        /// <inheritdoc cref="Inventory{TItem}.OnSlotChanged"/>
        public event Inventory<TItem>.SlotDelegate OnSlotChanged;

        /// <inheritdoc cref="Inventory{TItem}.OnInventoryUpdated"/>
        public event Action OnInventoryUpdated;

        /// <summary>
        /// Constructs a linked inventory over <paramref name="source"/>.
        /// </summary>
        /// <param name="source">The inventory whose items this one references.</param>
        /// <param name="maxSlotCount">Fixed slot count, or null for an unbounded, self-compacting loadout.</param>
        /// <param name="allowDuplicates">
        /// When false, at most one slot may reference any given <see cref="IItemAttributes.UniqueId"/>.
        /// When true, a kind may span several slots: stackable units overflow into further slots,
        /// and several instances of a non-stackable kind may each take a slot.
        /// </param>
        /// <param name="maxStackSize">
        /// When set, the stack limit of every stackable item in this loadout, replacing the item's own
        /// <see cref="Item.MaxStackSize"/> (it may be larger or smaller). Null uses each item's own limit.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maxSlotCount"/> or <paramref name="maxStackSize"/> is zero or negative.
        /// </exception>
        public LinkedInventory(Inventory<TItem> source, int? maxSlotCount = null, bool allowDuplicates = true,
            int? maxStackSize = null)
            : this(source, maxSlotCount, allowDuplicates, maxStackSize, null)
        {
        }

        /// <summary>
        /// Constructs a linked inventory over <paramref name="source"/> whose stackable items get a
        /// per-item stack limit from <paramref name="maxStackSizeResolver"/>.
        /// </summary>
        /// <param name="source">The inventory whose items this one references.</param>
        /// <param name="maxSlotCount">Fixed slot count, or null for an unbounded loadout.</param>
        /// <param name="allowDuplicates">See <see cref="AllowDuplicates"/>.</param>
        /// <param name="maxStackSizeResolver">
        /// Returns the stack limit for a stackable item; results below 1 are treated as 1. Must be
        /// deterministic, because existing slots are measured against it.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="maxStackSizeResolver"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSlotCount"/> is zero or negative.</exception>
        public LinkedInventory(Inventory<TItem> source, int? maxSlotCount, bool allowDuplicates,
            Func<TItem, int> maxStackSizeResolver)
            : this(source, maxSlotCount, allowDuplicates, null,
                maxStackSizeResolver ?? throw new ArgumentNullException(nameof(maxStackSizeResolver)))
        {
        }

        private LinkedInventory(Inventory<TItem> source, int? maxSlotCount, bool allowDuplicates, int? maxStackSize,
            Func<TItem, int> resolver)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maxSlotCount is <= 0) throw new ArgumentOutOfRangeException(nameof(maxSlotCount));
            if (maxStackSize is <= 0) throw new ArgumentOutOfRangeException(nameof(maxStackSize));

            Source = source;
            MaxSlotCount = maxSlotCount;
            AllowDuplicates = allowDuplicates;
            MaxStackSizeOverride = maxStackSize;
            _stackSizeResolver = resolver;

            _events = new InventoryEventQueue<TItem>(this);
            _slots = new List<LinkedSlot<TItem>>();
            Slots = _slots.AsReadOnly();

            if (maxSlotCount.HasValue)
            {
                for (var i = 0; i < maxSlotCount.Value; i++) _slots.Add(new LinkedSlot<TItem>(this));
            }

            source.Synchronize += Reconcile;
        }

        /// <summary>
        /// The inventory whose items this one references.
        /// </summary>
        public Inventory<TItem> Source { get; }

        /// <summary>
        /// Fixed slot count, or null when unbounded.
        /// </summary>
        public int? MaxSlotCount { get; }

        /// <summary>
        /// When false, at most one slot may reference any given <see cref="IItemAttributes.UniqueId"/>:
        /// one slot per kind, which a stackable item may fill to its limit but never overflow.
        /// </summary>
        public bool AllowDuplicates { get; }

        /// <summary>
        /// The constructor's <c>maxStackSize</c>, or null when none was given.
        /// </summary>
        public int? MaxStackSizeOverride { get; }

        /// <summary>
        /// True when <see cref="MaxSlotCount"/> is set.
        /// </summary>
        public bool IsFixedSize => MaxSlotCount.HasValue;

        /// <summary>
        /// True once <see cref="Dispose"/> has been called. A disposed loadout no longer follows its
        /// source, rejects every mutation with <see cref="ObjectDisposedException"/>, and still answers queries.
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Live read-only view of every slot in index order. In fixed-size mode this includes empty
        /// slots and always has <see cref="MaxSlotCount"/> entries; in unbounded mode it contains no
        /// empty slots.
        /// </summary>
        public IReadOnlyList<LinkedSlot<TItem>> Slots { get; }

        IReadOnlyList<IReadOnlyItemStack<TItem>> IReadOnlyInventory<TItem>.Slots => Slots;

        /// <summary>
        /// Gets the slot at <paramref name="index"/>. Never null; may be empty.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public LinkedSlot<TItem> this[int index]
        {
            get
            {
                EnsureValidIndex(index);
                return _slots[index];
            }
        }

        IReadOnlyItemStack<TItem> IReadOnlyInventory<TItem>.this[int index] => this[index];

        /// <inheritdoc cref="Inventory{TItem}.SlotCount"/>
        public int SlotCount => _slots.Count;

        /// <inheritdoc cref="Inventory{TItem}.FilledSlotCount"/>
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

        /// <inheritdoc cref="Inventory{TItem}.FreeSlotCount"/>
        public int? FreeSlotCount => IsFixedSize ? MaxSlotCount!.Value - FilledSlotCount : null;

        /// <inheritdoc cref="Inventory{TItem}.IsEmpty"/>
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
        /// True when no <i>slot</i> is free. Always false when unbounded. A full loadout may still
        /// accept units that top up an existing partial stack, so gate adds on <see cref="CanAdd"/>
        /// or <see cref="GetFreeSpaceFor"/>, never on this.
        /// </summary>
        public bool IsFull => IsFixedSize && FilledSlotCount >= MaxSlotCount!.Value;

        /// <summary>
        /// Total units claimed across all slots.
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

        /// <inheritdoc cref="Inventory{TItem}.IsValidIndex"/>
        public bool IsValidIndex(int index) => index >= 0 && index < _slots.Count;

        /// <summary>
        /// The stack limit this loadout applies to <paramref name="item"/>: 1 for a non-stackable
        /// item; otherwise the resolver's result, else <see cref="MaxStackSizeOverride"/>, else the
        /// item's own <see cref="Item.MaxStackSize"/>. Never below 1, and 0 for a null item.
        /// </summary>
        public int GetMaxStackSize(TItem item)
        {
            if (item == null) return 0;
            if (!item.IsStackable) return 1;

            var size = _stackSizeResolver != null
                ? _stackSizeResolver(item)
                : MaxStackSizeOverride ?? item.MaxStackSize;
            return Math.Max(1, size);
        }

        // ---------------------------------------------------------------------------------------
        // Availability
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Units of <paramref name="item"/> the source holds that no slot claims yet. For a
        /// non-stackable item this is 1 when that exact instance is in the source and unassigned,
        /// else 0. For a stackable item it is the source's quantity of that kind minus the quantity
        /// already assigned.
        /// </summary>
        public int GetUnassignedQuantity(TItem item)
        {
            if (item == null) return 0;

            if (!item.IsStackable)
            {
                var instanceId = item.InstanceId;
                if (!instanceId.HasValue) return 0;
                if (Source.IndexOfInstance(instanceId.Value) < 0) return 0;
                return IndexOfInstance(instanceId.Value) < 0 ? 1 : 0;
            }

            return GetUnassignedQuantity(item.UniqueId);
        }

        /// <summary>
        /// Units of this <see cref="IItemAttributes.UniqueId"/> the source holds that no slot claims
        /// yet. For a non-stackable kind this counts its unassigned instances.
        /// </summary>
        public int GetUnassignedQuantity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return 0;
            return Math.Max(0, Source.GetQuantity(uniqueId) - GetQuantity(uniqueId));
        }

        /// <summary>
        /// Units of <paramref name="item"/> that could be added right now, honoring what the source
        /// holds and no slot claims yet, slot capacity, stack limits, <see cref="AllowDuplicates"/>,
        /// <see cref="CanAcceptItem"/> and <see cref="CanAcceptItemAt"/>. 0 for a null or rejected item.
        /// </summary>
        public int GetFreeSpaceFor(TItem item)
        {
            if (item == null || !CanAcceptItem(item)) return 0;

            var unassigned = GetUnassignedQuantity(item);
            if (unassigned <= 0) return 0;

            return (int)Math.Min(GetSlotCapacityFor(item, unassigned), unassigned);
        }

        /// <summary>
        /// Whether <see cref="TryAdd(TItem,int)"/> would succeed with these arguments.
        /// </summary>
        public bool CanAdd(TItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            if (!item.IsStackable && quantity > 1) return false;
            return quantity <= GetFreeSpaceFor(item);
        }

        /// <summary>
        /// Units that fit in the loadout's slots alone, ignoring how many the source can supply.
        /// Counts only as many open slots as <paramref name="wanted"/> units would need, so the answer
        /// stays exact when <see cref="CanAcceptItemAt"/> refuses some indices of an unbounded loadout.
        /// </summary>
        private long GetSlotCapacityFor(TItem item, long wanted)
        {
            long stackSize = GetMaxStackSize(item);
            long existingSpace = 0;
            var hasExistingOfKind = false;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!IsKind(slot, item.UniqueId)) continue;

                hasExistingOfKind = true;
                if (item.IsStackable && slot.Item.IsStackable) existingSpace += slot.RemainingSpace;
            }

            if (!AllowDuplicates)
            {
                if (hasExistingOfKind) return existingSpace;
                return OpenSlotIndex(item, 0, create: false) >= 0 ? stackSize : 0;
            }

            var stillNeeded = Math.Max(0L, wanted - existingSpace);
            var slotsNeeded = (stillNeeded + stackSize - 1) / stackSize;
            return existingSpace + CountOpenSlots(item, slotsNeeded) * stackSize;
        }

        // ---------------------------------------------------------------------------------------
        // Adding
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Assigns exactly <paramref name="quantity"/> units of <paramref name="item"/>, or nothing.
        /// The units stay in the source.
        /// </summary>
        /// <returns>True when the full quantity was assigned.</returns>
        /// <remarks>
        /// Atomic. Fills partial slots of the same kind in ascending order first, then starts new
        /// slots (only when <see cref="AllowDuplicates"/> is true or the kind is not yet present).
        /// Fails when the source does not hold enough unassigned units - a stackable item is checked
        /// by <see cref="IItemAttributes.UniqueId"/>, a non-stackable one by
        /// <see cref="Item.InstanceId"/>. A non-stackable item is rejected when
        /// <paramref name="quantity"/> exceeds 1, or when that instance already has a slot.
        /// </remarks>
        public bool TryAdd(TItem item, int quantity = 1) => TryAdd(item, quantity, out _);

        /// <param name="item">The item to assign.</param>
        /// <param name="quantity">Number of units to assign; must be positive.</param>
        /// <param name="remainder">0 on success; the full requested quantity on failure.</param>
        /// <inheritdoc cref="TryAdd(TItem,int)"/>
        public bool TryAdd(TItem item, int quantity, out int remainder)
        {
            ThrowIfDisposed();
            remainder = quantity;
            if (!CanAdd(item, quantity)) return false;

            var resolved = ResolveSourceItem(item);
            if (resolved == null) return false;

            using (OpenBatch())
            {
                ApplyAdd(resolved, quantity);
            }

            remainder = 0;
            return true;
        }

        /// <summary>
        /// As <see cref="TryAdd(TItem,int)"/>, selecting a stackable kind by
        /// <see cref="IItemAttributes.UniqueId"/> straight from the source (for restoring saved
        /// loadouts). For a non-stackable kind the first unassigned instance is used.
        /// </summary>
        public bool TryAdd(string uniqueId, int quantity = 1)
        {
            ThrowIfDisposed();
            return TryFindSourceItem(uniqueId, out var item) && TryAdd(item, quantity);
        }

        /// <summary>
        /// Assigns the source's non-stackable item with this <see cref="Item.InstanceId"/>.
        /// </summary>
        public bool TryAdd(Guid instanceId)
        {
            ThrowIfDisposed();
            return Source.TryFindByInstanceId(instanceId, out var item) && TryAdd(item);
        }

        /// <summary>
        /// Assigns as many of <paramref name="quantity"/> units as fit. Assigns at most 1 unit of a
        /// non-stackable item.
        /// </summary>
        /// <returns>Units actually assigned, 0 to <paramref name="quantity"/>.</returns>
        public int AddUpTo(TItem item, int quantity)
        {
            ThrowIfDisposed();
            if (item == null || quantity <= 0) return 0;

            var effectiveQuantity = item.IsStackable ? quantity : Math.Min(quantity, 1);
            var toAdd = (int)Math.Min((long)effectiveQuantity, GetFreeSpaceFor(item));
            if (toAdd <= 0) return 0;

            var resolved = ResolveSourceItem(item);
            if (resolved == null) return 0;

            using (OpenBatch())
            {
                ApplyAdd(resolved, toAdd);
            }

            return toAdd;
        }

        /// <summary>
        /// Assigns <paramref name="quantity"/> units into the slot at exactly <paramref name="index"/>,
        /// or does nothing.
        /// </summary>
        /// <returns>
        /// True when assigned. False when the item is null or rejected, <paramref name="quantity"/>
        /// is not positive, the source lacks that many unassigned units, or:
        /// <list type="bullet">
        /// <item>the slot is empty and the quantity exceeds <see cref="GetMaxStackSize"/>, the kind is
        /// present while <see cref="AllowDuplicates"/> is false, or <see cref="CanAcceptItemAt"/>
        /// rejects the slot;</item>
        /// <item>the slot is occupied by a different kind, or lacks room for the full quantity.</item>
        /// </list>
        /// </returns>
        /// <remarks>An unbounded loadout holds no empty slots, so only topping up an existing slot can succeed there.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryAddAt(int index, TItem item, int quantity = 1)
        {
            ThrowIfDisposed();
            EnsureValidIndex(index);
            if (item == null || quantity <= 0 || !CanAcceptItem(item)) return false;
            if (!item.IsStackable && quantity > 1) return false;
            if (GetUnassignedQuantity(item) < quantity) return false;

            var resolved = ResolveSourceItem(item);
            if (resolved == null) return false;

            var slot = _slots[index];

            if (slot.IsEmpty)
            {
                if (quantity > GetMaxStackSize(item)) return false;
                if (!AllowDuplicates && IndexOf(item.UniqueId) >= 0) return false;
                if (!CanAcceptItemAt(index, item)) return false;

                using (OpenBatch())
                {
                    slot.Set(resolved, quantity);
                    MarkSlotChanged(index);
                    EnqueueItemAdded(resolved, quantity);
                }

                return true;
            }

            if (!item.IsStackable || !slot.Item.IsStackable) return false;
            if (!string.Equals(slot.Item.UniqueId, item.UniqueId, StringComparison.Ordinal)) return false;
            if (slot.RemainingSpace < quantity) return false;

            using (OpenBatch())
            {
                slot.Add(quantity);
                slot.RefreshItem(resolved);
                MarkSlotChanged(index);
                EnqueueItemAdded(resolved, quantity);
            }

            return true;
        }

        /// <summary>
        /// As <see cref="TryAddAt(int,TItem,int)"/>, selecting the item from the source by
        /// <see cref="IItemAttributes.UniqueId"/>. The restore-from-save primitive for stackable
        /// slots; for a non-stackable kind the first unassigned instance is used.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryAddAt(int index, string uniqueId, int quantity = 1)
        {
            ThrowIfDisposed();
            EnsureValidIndex(index);
            return TryFindSourceItem(uniqueId, out var item) && TryAddAt(index, item, quantity);
        }

        /// <summary>
        /// As <see cref="TryAddAt(int,TItem,int)"/>, selecting the source's non-stackable item by
        /// <see cref="Item.InstanceId"/>. The restore-from-save primitive for non-stackable slots.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryAddAt(int index, Guid instanceId)
        {
            ThrowIfDisposed();
            EnsureValidIndex(index);
            return Source.TryFindByInstanceId(instanceId, out var item) && TryAddAt(index, item);
        }

        // ---------------------------------------------------------------------------------------
        // Removing (references only; the source is never touched)
        // ---------------------------------------------------------------------------------------

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
        /// Unassigns exactly <paramref name="quantity"/> units matching <paramref name="item"/>'s
        /// <see cref="IItemAttributes.UniqueId"/>, or nothing, draining in ascending slot order. The
        /// units stay in the source. Use <see cref="TryRemoveInstance"/> to target one instance.
        /// </summary>
        public bool TryRemove(TItem item, int quantity = 1) => TryRemove(item, quantity, out _);

        /// <param name="item">An item of the kind to unassign; matched by <see cref="IItemAttributes.UniqueId"/>.</param>
        /// <param name="quantity">Number of units to unassign; must be positive.</param>
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

        /// <param name="uniqueId">The <see cref="IItemAttributes.UniqueId"/> of the kind to unassign.</param>
        /// <param name="quantity">Number of units to unassign; must be positive.</param>
        /// <param name="remainder">0 on success; the full requested quantity on failure.</param>
        /// <inheritdoc cref="TryRemove(TItem,int,out int)"/>
        public bool TryRemove(string uniqueId, int quantity, out int remainder)
        {
            ThrowIfDisposed();
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
        /// Unassigns as many of <paramref name="quantity"/> units as are assigned.
        /// </summary>
        /// <returns>Units actually unassigned.</returns>
        public int RemoveUpTo(TItem item, int quantity) => item == null ? 0 : RemoveUpTo(item.UniqueId, quantity);

        /// <inheritdoc cref="RemoveUpTo(TItem,int)"/>
        public int RemoveUpTo(string uniqueId, int quantity)
        {
            ThrowIfDisposed();
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
        /// Unassigns the slot referencing this <paramref name="item"/>'s <see cref="Item.InstanceId"/>.
        /// Always false for a stackable item, which has no instance identity.
        /// </summary>
        public bool TryRemoveInstance(TItem item)
        {
            ThrowIfDisposed();
            var index = IndexOfInstance(item);
            if (index < 0) return false;

            using (OpenBatch())
            {
                ClearSlotAt(index);
            }

            return true;
        }

        /// <summary>
        /// Unassigns the entire slot at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Index of the slot to empty.</param>
        /// <param name="item">The item unassigned, or null when the slot was empty.</param>
        /// <param name="quantity">Units unassigned, or 0 when the slot was empty.</param>
        /// <returns>False when the slot was already empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryRemoveAt(int index, out TItem item, out int quantity)
        {
            ThrowIfDisposed();
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
                ClearSlotAt(index);
            }

            return true;
        }

        /// <summary>
        /// Unassigns exactly <paramref name="quantity"/> units from the slot at <paramref name="index"/>,
        /// clearing it if that empties it. Atomic.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryRemoveAt(int index, int quantity)
        {
            ThrowIfDisposed();
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
        /// Empties every slot. Fixed-size loadouts keep their slot count; unbounded ones drop to zero
        /// slots. Raises <see cref="OnItemRemoved"/> once per cleared slot and
        /// <see cref="OnInventoryUpdated"/> once. The source is untouched.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
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

        // ---------------------------------------------------------------------------------------
        // Consume-through
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Uses up <paramref name="quantity"/> units: removes them from the <b>source</b> and from
        /// this loadout together, or does nothing. The "drink the potion on my hotbar" operation.
        /// </summary>
        /// <returns>True when the units were consumed.</returns>
        /// <remarks>
        /// A stackable item is consumed by <see cref="IItemAttributes.UniqueId"/>, draining this
        /// loadout's slots in ascending order. A non-stackable item is consumed by
        /// <see cref="Item.InstanceId"/> and only with a quantity of 1. Atomic: fails, changing
        /// nothing, unless both this loadout and the source hold the units. The loadout is updated
        /// before the source, so the source's change notification finds nothing left to trim.
        /// </remarks>
        public bool TryConsume(TItem item, int quantity = 1)
        {
            ThrowIfDisposed();
            if (item == null || quantity <= 0) return false;

            if (!item.IsStackable)
            {
                if (quantity != 1) return false;
                var index = IndexOfInstance(item);
                return index >= 0 && TryConsumeAt(index);
            }

            var uniqueId = item.UniqueId;
            if (GetQuantity(uniqueId) < quantity || !Source.CanRemove(uniqueId, quantity)) return false;

            using (OpenBatch())
            {
                ApplyRemoveByKind(uniqueId, quantity);
                if (!Source.TryRemove(uniqueId, quantity)) throw SourceRefusedRemoval();
            }

            return true;
        }

        /// <summary>
        /// As <see cref="TryConsume(TItem,int)"/>, consuming from the slot at <paramref name="index"/>
        /// specifically, so that exact slot is the one drained.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid slot index.</exception>
        public bool TryConsumeAt(int index, int quantity = 1)
        {
            ThrowIfDisposed();
            EnsureValidIndex(index);
            if (quantity <= 0) return false;

            var slot = _slots[index];
            if (slot.IsEmpty || slot.Quantity < quantity) return false;

            var item = slot.Item;

            if (!item.IsStackable)
            {
                if (!Source.ContainsInstance(item)) return false;

                using (OpenBatch())
                {
                    RemoveFromSlot(index, 1);
                    if (!Source.TryRemoveInstance(item)) throw SourceRefusedRemoval();
                }

                return true;
            }

            if (!Source.CanRemove(item.UniqueId, quantity)) return false;

            using (OpenBatch())
            {
                RemoveFromSlot(index, quantity);
                if (!Source.TryRemove(item.UniqueId, quantity)) throw SourceRefusedRemoval();
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------
        // Source synchronisation
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Brings the loadout in line with the source: clears slots whose non-stackable instance is
        /// no longer in the source, trims stackable claims above what the source holds (highest slot
        /// index first), and refreshes each slot's cached <see cref="LinkedSlot{TItem}.Item"/> when the
        /// source no longer holds that object (reported through <see cref="OnSlotChanged"/>). One
        /// batch; raises one <see cref="OnItemRemoved"/> per stackable kind trimmed and one per cleared
        /// non-stackable instance. Runs automatically after every change the source reports, once
        /// its public events have been raised, so calling it yourself is only needed when the source
        /// is held in a batch. A no-op once disposed.
        /// </summary>
        public void Reconcile()
        {
            if (_disposed) return;

            // Stackable trims are totalled per kind so one source loss is one removal event, however
            // many slots it spanned. Allocated only when something is actually trimmed.
            List<(TItem Item, int Quantity)> trimmed = null;

            using (OpenBatch())
            {
                // Descending, so a slot removed in unbounded mode only shifts slots already processed.
                for (var i = _slots.Count - 1; i >= 0; i--)
                {
                    var slot = _slots[i];
                    if (slot.IsEmpty) continue;

                    var item = slot.Item;

                    if (!item.IsStackable)
                    {
                        var instanceId = item.InstanceId;
                        if (instanceId.HasValue && Source.TryFindByInstanceId(instanceId.Value, out var current))
                        {
                            if (slot.RefreshItem(current)) MarkSlotChanged(i);
                        }
                        else
                        {
                            ClearSlotAt(i);
                        }

                        continue;
                    }

                    var uniqueId = item.UniqueId;
                    var excess = GetQuantity(uniqueId) - Source.GetQuantity(uniqueId);
                    if (excess > 0)
                    {
                        var removed = Math.Min(excess, slot.Quantity);
                        RemoveUnitsFromSlot(i, removed);
                        AddTrimmed(ref trimmed, item, removed);
                        if (slot.IsEmpty) continue;
                    }

                    // A stackable item is fungible, so the cached object only matters once the source
                    // no longer holds it. Re-pointing on every update would churn on source reorders.
                    if (!SourceHolds(slot.Item) && TryFindSourceItem(uniqueId, out var sourceItem) &&
                        slot.RefreshItem(sourceItem))
                    {
                        MarkSlotChanged(i);
                    }
                }

                if (trimmed != null)
                {
                    for (var i = 0; i < trimmed.Count; i++) EnqueueItemRemoved(trimmed[i].Item, trimmed[i].Quantity);
                }
            }
        }

        /// <summary>
        /// Whether the source's slots hold exactly this object.
        /// </summary>
        private bool SourceHolds(TItem item)
        {
            var sourceSlots = Source.Slots;
            for (var i = 0; i < sourceSlots.Count; i++)
            {
                if (ReferenceEquals(sourceSlots[i].Item, item)) return true;
            }

            return false;
        }

        private static void AddTrimmed(ref List<(TItem Item, int Quantity)> trimmed, TItem item, int quantity)
        {
            trimmed ??= new List<(TItem Item, int Quantity)>();

            for (var i = 0; i < trimmed.Count; i++)
            {
                if (!string.Equals(trimmed[i].Item.UniqueId, item.UniqueId, StringComparison.Ordinal)) continue;
                trimmed[i] = (trimmed[i].Item, trimmed[i].Quantity + quantity);
                return;
            }

            trimmed.Add((item, quantity));
        }

        /// <summary>
        /// Stops following the source and releases the subscription that keeps this loadout alive.
        /// Afterwards every mutating method throws <see cref="ObjectDisposedException"/>; queries
        /// keep working on the last known state. Safe to call repeatedly.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Source.Synchronize -= Reconcile;
        }

        // ---------------------------------------------------------------------------------------
        // Queries
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// The items referenced, in slot order, skipping empty slots. Equivalent to iterating the
        /// loadout directly with <c>foreach</c>.
        /// </summary>
        /// <remarks>
        /// Like <see cref="List{T}"/>, enumeration throws <see cref="InvalidOperationException"/> if
        /// the loadout is modified while it is in progress.
        /// </remarks>
        public IEnumerable<TItem> Items => EnumerateItems();

        private IEnumerable<TItem> EnumerateItems()
        {
            var version = _events.Version;
            var index = 0;

            while (true)
            {
                if (version != _events.Version)
                {
                    throw new InvalidOperationException("The inventory was modified during enumeration.");
                }

                if (index >= _slots.Count) yield break;

                var slot = _slots[index++];
                if (!slot.IsEmpty) yield return slot.Item;
            }
        }

        /// <summary>
        /// Enumerates the items referenced, in slot order, skipping empty slots.
        /// </summary>
        public IEnumerator<TItem> GetEnumerator() => Items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Whether a slot references the item with this item's <see cref="Item.InstanceId"/>. Always
        /// false for a stackable item; use <see cref="Contains(string)"/> for its kind.
        /// </summary>
        public bool ContainsInstance(TItem item) => IndexOfInstance(item) >= 0;

        /// <summary>
        /// Whether any slot references this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public bool Contains(string uniqueId) => !string.IsNullOrEmpty(uniqueId) && IndexOf(uniqueId) >= 0;

        /// <summary>
        /// Whether any referenced item satisfies <paramref name="predicate"/>.
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
        /// Total units assigned whose <see cref="IItemAttributes.UniqueId"/> matches <paramref name="item"/>'s.
        /// </summary>
        public int GetQuantity(TItem item) => item == null ? 0 : GetQuantity(item.UniqueId);

        /// <summary>
        /// Total units assigned with this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public int GetQuantity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return 0;

            long total = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (IsKind(_slots[i], uniqueId)) total += _slots[i].Quantity;
            }

            return (int)Math.Min(total, int.MaxValue);
        }

        /// <summary>
        /// Total units assigned in slots whose item satisfies <paramref name="predicate"/>.
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
        /// Number of occupied slots referencing this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public int GetSlotCount(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return 0;

            var count = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (IsKind(_slots[i], uniqueId)) count++;
            }

            return count;
        }

        /// <summary>
        /// Finds the first referenced item satisfying <paramref name="predicate"/>, in slot order.
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
        /// All referenced items satisfying <paramref name="predicate"/>, lazily, in slot order.
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
        /// Finds the referenced non-stackable item with this <see cref="Item.InstanceId"/>.
        /// </summary>
        public bool TryFindByInstanceId(Guid instanceId, out TItem item)
        {
            var index = IndexOfInstance(instanceId);
            if (index >= 0)
            {
                item = _slots[index].Item;
                return true;
            }

            item = null;
            return false;
        }

        /// <summary>
        /// Slot index referencing this item's <see cref="Item.InstanceId"/>, or -1. Always -1 for a
        /// null or stackable item.
        /// </summary>
        public int IndexOfInstance(TItem item)
        {
            if (item == null) return -1;
            var instanceId = item.InstanceId;
            return instanceId.HasValue ? IndexOfInstance(instanceId.Value) : -1;
        }

        /// <summary>
        /// Slot index referencing this <see cref="Item.InstanceId"/>, or -1.
        /// </summary>
        public int IndexOfInstance(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return -1;

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && slot.Item.InstanceId == instanceId) return i;
            }

            return -1;
        }

        /// <summary>
        /// Lowest slot index referencing this <see cref="IItemAttributes.UniqueId"/>, or -1.
        /// </summary>
        public int IndexOf(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return -1;

            for (var i = 0; i < _slots.Count; i++)
            {
                if (IsKind(_slots[i], uniqueId)) return i;
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

        // ---------------------------------------------------------------------------------------
        // Slot manipulation and organising
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Exchanges the contents of two slots. Returns false, changing nothing, for equal indices,
        /// when both slots are empty, or when <see cref="CanAcceptItemAt"/> refuses either move.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool Swap(int indexA, int indexB)
        {
            ThrowIfDisposed();
            EnsureValidIndex(indexA);
            EnsureValidIndex(indexB);
            if (indexA == indexB) return false;

            var slotA = _slots[indexA];
            var slotB = _slots[indexB];
            if (slotA.IsEmpty && slotB.IsEmpty) return false;
            if (!slotB.IsEmpty && !CanAcceptItemAt(indexA, slotB.Item)) return false;
            if (!slotA.IsEmpty && !CanAcceptItemAt(indexB, slotA.Item)) return false;

            using (OpenBatch())
            {
                slotA.SwapWith(slotB);
                MarkSlotChanged(indexA);
                MarkSlotChanged(indexB);
            }

            return true;
        }

        /// <summary>
        /// Moves the slot at <paramref name="fromIndex"/> to <paramref name="toIndex"/>: an empty
        /// destination receives it; a destination of the same stackable kind with room absorbs as
        /// much as fits, leaving any remainder in the source; any other occupied destination is
        /// swapped with.
        /// </summary>
        /// <returns>
        /// False for equal indices, an empty source, or when <see cref="CanAcceptItemAt"/> refuses
        /// the move; true otherwise.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Either index is not a valid slot index.</exception>
        public bool Move(int fromIndex, int toIndex)
        {
            ThrowIfDisposed();
            EnsureValidIndex(fromIndex);
            EnsureValidIndex(toIndex);
            if (fromIndex == toIndex) return false;

            var source = _slots[fromIndex];
            if (source.IsEmpty) return false;

            var destination = _slots[toIndex];
            var merge = !destination.IsEmpty && CanShareSlot(source.Item, destination.Item) && destination.RemainingSpace > 0;

            if (!merge)
            {
                if (!CanAcceptItemAt(toIndex, source.Item)) return false;
                if (!destination.IsEmpty && !CanAcceptItemAt(fromIndex, destination.Item)) return false;
            }

            using (OpenBatch())
            {
                var sourceRemoved = false;

                if (destination.IsEmpty)
                {
                    destination.Set(source.Item, source.Quantity);
                    source.Clear();
                }
                else if (merge)
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
        /// Merges every set of stackable like slots into as few slots as possible, lowest index
        /// first. One batch. No-op when <see cref="AllowDuplicates"/> is false.
        /// </summary>
        /// <returns>Number of slots freed.</returns>
        public int Consolidate()
        {
            ThrowIfDisposed();
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
                        if (candidate.IsEmpty || !CanShareSlot(candidate.Item, target.Item)) continue;

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
        /// Stable. See <see cref="Sort(Comparison{TItem})"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
        public bool Sort(IComparer<TItem> comparer)
        {
            ThrowIfDisposed();
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            return Sort(comparer.Compare);
        }

        /// <summary>
        /// Reorders occupied slots by <paramref name="comparison"/>, pushing empty slots to the end.
        /// Stable, so equally ranked items keep their relative order. Does not merge stacks. One batch.
        /// </summary>
        /// <returns>
        /// False, changing nothing, when <see cref="CanAcceptItemAt"/> refuses an item at the slot the
        /// sort would give it; true otherwise (including when there was nothing to sort).
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is null.</exception>
        public bool Sort(Comparison<TItem> comparison)
        {
            ThrowIfDisposed();
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            var occupied = new List<(TItem Item, int Quantity, int OriginalIndex)>();
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty) occupied.Add((slot.Item, slot.Quantity, i));
            }

            if (occupied.Count == 0) return true;

            occupied.Sort((a, b) =>
            {
                var cmp = comparison(a.Item, b.Item);
                return cmp != 0 ? cmp : a.OriginalIndex.CompareTo(b.OriginalIndex);
            });

            for (var i = 0; i < occupied.Count; i++)
            {
                if (occupied[i].OriginalIndex != i && !CanAcceptItemAt(i, occupied[i].Item)) return false;
            }

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

            return true;
        }

        /// <summary>
        /// Shifts occupied slots to the lowest indices, preserving their relative order. One batch.
        /// Always a no-op returning false in unbounded mode, which is compact by construction.
        /// </summary>
        /// <returns>
        /// True when at least one slot moved. False when nothing needed to move, or when
        /// <see cref="CanAcceptItemAt"/> refused a move (in which case nothing changed).
        /// </returns>
        public bool Compact()
        {
            ThrowIfDisposed();
            if (!IsFixedSize) return false;

            var writeIndex = 0;
            for (var readIndex = 0; readIndex < _slots.Count; readIndex++)
            {
                var slot = _slots[readIndex];
                if (slot.IsEmpty) continue;

                if (readIndex != writeIndex && !CanAcceptItemAt(writeIndex, slot.Item)) return false;
                writeIndex++;
            }

            writeIndex = 0;
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
        public IDisposable BeginBatchUpdate() => _events.BeginBatchScope();

        // ---------------------------------------------------------------------------------------
        // Filters
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Whether this loadout accepts <paramref name="item"/> at all, regardless of space. True by
        /// default for any non-null item; override for filtered loadouts (for example weapons only).
        /// Consulted by <see cref="CanAdd"/> and <see cref="GetFreeSpaceFor"/>.
        /// </summary>
        protected virtual bool CanAcceptItem(TItem item) => item != null;

        /// <summary>
        /// Whether the slot at <paramref name="index"/> accepts <paramref name="item"/> when that
        /// item is <i>placed into an empty slot</i> (by an add, <see cref="Move"/>, <see cref="Swap"/>,
        /// <see cref="Compact"/> or <see cref="Sort"/>). True by default. Override for per-slot rules
        /// such as "slot 0 takes weapons only".
        /// </summary>
        /// <remarks>
        /// Meaningful in fixed-size mode, where an index is a stable position. In unbounded mode slots
        /// shift when others are removed, so an index-based rule is rarely what you want there. Not
        /// consulted when units are added to a slot that already holds the same kind.
        /// </remarks>
        protected virtual bool CanAcceptItemAt(int index, TItem item) => true;

        // ---------------------------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------------------------

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LinkedInventory<TItem>));
        }

        private void EnsureValidIndex(int index)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static bool IsKind(LinkedSlot<TItem> slot, string uniqueId)
        {
            return !slot.IsEmpty && string.Equals(slot.Item.UniqueId, uniqueId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether two referenced items may be merged into one slot: both stackable, same kind.
        /// </summary>
        private static bool CanShareSlot(TItem a, TItem b)
        {
            return a.IsStackable && b.IsStackable && string.Equals(a.UniqueId, b.UniqueId, StringComparison.Ordinal);
        }

        /// <summary>
        /// The source's own object for the item that <paramref name="item"/> identifies: the instance
        /// for a non-stackable item, the first stack of that kind for a stackable one.
        /// </summary>
        private TItem ResolveSourceItem(TItem item)
        {
            if (!item.IsStackable)
            {
                var instanceId = item.InstanceId;
                return instanceId.HasValue && Source.TryFindByInstanceId(instanceId.Value, out var found) ? found : null;
            }

            return TryFindSourceItem(item.UniqueId, out var stackable) ? stackable : null;
        }

        /// <summary>
        /// Finds the source's item of this kind: any item for a stackable kind, the first instance
        /// not yet assigned here for a non-stackable one.
        /// </summary>
        private bool TryFindSourceItem(string uniqueId, out TItem item)
        {
            item = null;
            if (string.IsNullOrEmpty(uniqueId)) return false;

            var sourceSlots = Source.Slots;
            for (var i = 0; i < sourceSlots.Count; i++)
            {
                var candidate = sourceSlots[i].Item;
                if (candidate == null || !string.Equals(candidate.UniqueId, uniqueId, StringComparison.Ordinal)) continue;
                if (!candidate.IsStackable && IndexOfInstance(candidate) >= 0) continue;

                item = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Number of empty slots that could take <paramref name="item"/>. In unbounded mode these are
        /// the consecutive append positions that accept it, counted only up to <paramref name="needed"/>.
        /// </summary>
        private long CountOpenSlots(TItem item, long needed)
        {
            if (!IsFixedSize)
            {
                // Appended slots take consecutive indices, so a rule may accept one and refuse the next.
                long open = 0;
                while (open < needed && CanAcceptItemAt(_slots.Count + (int)open, item)) open++;
                return open;
            }

            long count = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty && CanAcceptItemAt(i, item)) count++;
            }

            return count;
        }

        /// <summary>
        /// Index of the first empty slot at or after <paramref name="from"/> that accepts
        /// <paramref name="item"/>; in unbounded mode the append position. With <paramref name="create"/>
        /// the unbounded slot is actually appended. -1 when there is none.
        /// </summary>
        private int OpenSlotIndex(TItem item, int from, bool create)
        {
            if (!IsFixedSize)
            {
                if (!CanAcceptItemAt(_slots.Count, item)) return -1;
                if (!create) return _slots.Count;

                _slots.Add(new LinkedSlot<TItem>(this));
                return _slots.Count - 1;
            }

            for (var i = from; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty && CanAcceptItemAt(i, item)) return i;
            }

            return -1;
        }

        private void ApplyAdd(TItem item, int quantity)
        {
            var remaining = quantity;

            if (item.IsStackable)
            {
                for (var i = 0; i < _slots.Count && remaining > 0; i++)
                {
                    var slot = _slots[i];
                    if (!IsKind(slot, item.UniqueId) || !slot.Item.IsStackable) continue;

                    var added = slot.Add(remaining);
                    if (added <= 0) continue;

                    remaining -= added;
                    slot.RefreshItem(item);
                    MarkSlotChanged(i);
                }
            }

            var stackSize = GetMaxStackSize(item);
            var blockedByExistingKind = !AllowDuplicates && IndexOf(item.UniqueId) >= 0;
            var searchFrom = 0;

            while (remaining > 0 && !blockedByExistingKind)
            {
                var index = OpenSlotIndex(item, searchFrom, create: true);
                if (index < 0) break;

                searchFrom = index + 1;
                var toAdd = Math.Min(remaining, stackSize);
                _slots[index].Set(item, toAdd);
                remaining -= toAdd;
                MarkSlotChanged(index);

                blockedByExistingKind = !AllowDuplicates;
            }

            System.Diagnostics.Debug.Assert(remaining == 0, "ApplyAdd placed fewer units than CanAdd promised.");

            // Report what was actually placed, never what was requested.
            if (quantity - remaining > 0) EnqueueItemAdded(item, quantity - remaining);
        }

        private void ApplyRemoveByKind(string uniqueId, int quantity)
        {
            var remaining = quantity;
            TItem removedItemSample = null;

            for (var i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (!IsKind(slot, uniqueId)) continue;

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

        /// <summary>
        /// Removes <paramref name="quantity"/> units from the occupied slot at <paramref name="index"/>,
        /// dropping the slot when it empties in unbounded mode. Caller guarantees the slot holds
        /// at least that many units and has opened a batch.
        /// </summary>
        private void RemoveFromSlot(int index, int quantity)
        {
            var item = _slots[index].Item;
            RemoveUnitsFromSlot(index, quantity);
            EnqueueItemRemoved(item, quantity);
        }

        /// <summary>
        /// As <see cref="RemoveFromSlot"/> but raises no removal event, for callers that report
        /// several slots as one removal.
        /// </summary>
        private void RemoveUnitsFromSlot(int index, int quantity)
        {
            var slot = _slots[index];
            slot.Remove(quantity);

            if (slot.IsEmpty && !IsFixedSize)
            {
                RemoveUnboundedSlot(index);
            }
            else
            {
                MarkSlotChanged(index);
            }
        }

        private static InvalidOperationException SourceRefusedRemoval()
        {
            return new InvalidOperationException(
                "The source refused a removal that it had just reported as possible.");
        }

        /// <summary>
        /// Empties the whole occupied slot at <paramref name="index"/>. Caller has opened a batch.
        /// </summary>
        private void ClearSlotAt(int index)
        {
            var slot = _slots[index];
            var item = slot.Item;
            var quantity = slot.Quantity;
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

        /// <summary>
        /// Removes the slot at <paramref name="index"/> (unbounded mode only) and marks every slot
        /// that shifted down into its place as changed.
        /// </summary>
        private void RemoveUnboundedSlot(int index)
        {
            _events.Version++;
            _slots.RemoveAt(index);
            for (var j = index; j < _slots.Count; j++) MarkSlotChanged(j);
        }

        private void EnqueueItemAdded(TItem item, int quantity) => _events.EnqueueItemAdded(item, quantity);

        private void EnqueueItemRemoved(TItem item, int quantity) => _events.EnqueueItemRemoved(item, quantity);

        private void MarkSlotChanged(int index) => _events.MarkSlotChanged(index);

        private InventoryEventQueue<TItem>.Batch OpenBatch() => _events.OpenBatch();

        int IInventoryEventSink<TItem>.SlotCount => _slots.Count;

        void IInventoryEventSink<TItem>.RaiseItemAdded(TItem item, int quantity) => OnItemAdded?.Invoke(item, quantity);

        void IInventoryEventSink<TItem>.RaiseItemRemoved(TItem item, int quantity) => OnItemRemoved?.Invoke(item, quantity);

        void IInventoryEventSink<TItem>.RaiseSlotChanged(int index) => OnSlotChanged?.Invoke(index);

        void IInventoryEventSink<TItem>.RaiseInventoryUpdated() => OnInventoryUpdated?.Invoke();

        // Nothing follows a loadout, so there is nothing to synchronise.
        void IInventoryEventSink<TItem>.RaiseSynchronize() { }
    }
}
