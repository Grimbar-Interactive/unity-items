using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// A set of typed <see cref="Inventory{TItem}"/> instances behind one front door. Items added to
    /// the collection are routed to the most specific registered inventory that can hold them.
    /// </summary>
    /// <remarks>
    /// <para><b>One inventory per item type.</b> At most one <c>Inventory&lt;T&gt;</c> may be registered
    /// for any given <c>T</c>, but a type and its ancestors may each have one, for example
    /// <c>Inventory&lt;UseItem&gt;</c> and <c>Inventory&lt;BasicItem&gt;</c> when <c>UseItem</c> derives
    /// from <c>BasicItem</c>. Types are matched exactly when registering.</para>
    /// <para><b>Routing.</b> An item's <i>chain</i> is every registered inventory whose item type is the
    /// item's runtime type or one of its base types, most specific first. With
    /// <see cref="AllowFallback"/> false (the default) an add uses only the first inventory of the chain:
    /// if that inventory cannot take the item the add fails. With it true, units the first inventory
    /// cannot take spill down to the next inventory of the chain, and so on.</para>
    /// <para><b>Removal and queries</b> always look at the whole chain, whatever
    /// <see cref="AllowFallback"/> says, because items may have been put into a less specific inventory
    /// directly.</para>
    /// <para><b>Atomicity.</b> Methods named <c>Try*</c> are all-or-nothing across every inventory
    /// involved. Methods named <c>*UpTo</c> and <see cref="AddRange"/> are best-effort.</para>
    /// <para><b>Events.</b> Item events and update notifications of every registered inventory are
    /// forwarded; see <see cref="OnItemAdded"/>, <see cref="OnItemRemoved"/> and
    /// <see cref="OnCollectionUpdated"/>.</para>
    /// <para>Like <see cref="Inventory{TItem}"/>, this type is deliberately not
    /// <see cref="SerializableAttribute"/>. Persist the inventories it holds.</para>
    /// </remarks>
    public class InventoryCollection
    {
        /// <summary>
        /// Signature for collection events that report an item and a number of units.
        /// </summary>
        public delegate void ItemQuantityDelegate(Item item, int quantity);

        private static readonly Entry[] NoEntries = Array.Empty<Entry>();
        private static readonly Route EmptyRoute = new Route(NoEntries, NoEntries);

        private readonly Dictionary<Type, Entry> _byType = new();
        private readonly Dictionary<Type, Route> _routes = new();
        private readonly List<Entry> _ordered = new();

        // Copy-on-write snapshots, rebuilt whenever registrations change. Loops iterate a local copy
        // of the reference, so a handler that registers or unregisters mid-operation cannot disturb them.
        private Entry[] _entries = NoEntries;
        private Type[] _types = Type.EmptyTypes;

        private int _version;
        private int _batchDepth;
        private bool _updatePending;

        /// <summary>
        /// Raised once per logical add with the item and the total units added to one inventory.
        /// Forwarded from the registered inventories as they flush their own events.
        /// </summary>
        public event ItemQuantityDelegate OnItemAdded;

        /// <summary>
        /// Raised once per logical removal with the item and the total units removed from one
        /// inventory. Forwarded from the registered inventories as they flush their own events.
        /// </summary>
        public event ItemQuantityDelegate OnItemRemoved;

        /// <summary>
        /// Raised once at the end of any operation that changed a registered inventory or the set of
        /// registered inventories. Operations that touch several inventories, and a caller's
        /// <see cref="BeginBatchUpdate"/> scope, raise it once rather than once per inventory.
        /// </summary>
        public event Action OnCollectionUpdated;

        /// <summary>
        /// Constructs an empty collection.
        /// </summary>
        /// <param name="allowFallback">
        /// When false, an item is only ever added to the most specific registered inventory for its
        /// type. When true, units that inventory cannot take spill into less specific inventories.
        /// </param>
        public InventoryCollection(bool allowFallback = false)
        {
            AllowFallback = allowFallback;
        }

        /// <summary>
        /// Whether an add may spill into less specific inventories when the most specific one cannot
        /// take the item (full, or rejected by its <c>CanAcceptItem</c> filter).
        /// </summary>
        public bool AllowFallback { get; }

        /// <summary>
        /// Number of registered inventories.
        /// </summary>
        public int Count => _entries.Length;

        /// <summary>
        /// The item types that have an inventory registered, in registration order.
        /// </summary>
        public IReadOnlyList<Type> RegisteredItemTypes => _types;

        #region Registration

        /// <summary>
        /// Registers <paramref name="inventory"/> as the inventory for <typeparamref name="TItem"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is null.</exception>
        /// <exception cref="ArgumentException">An inventory is already registered for <typeparamref name="TItem"/>.</exception>
        public void Register<TItem>(Inventory<TItem> inventory) where TItem : Item
        {
            if (!TryRegister(inventory))
            {
                throw new ArgumentException(
                    $"An inventory for {typeof(TItem).Name} is already registered.", nameof(inventory));
            }
        }

        /// <summary>
        /// Same as <see cref="Register{TItem}"/>, but returns false instead of throwing when an inventory
        /// is already registered for <typeparamref name="TItem"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="inventory"/> is null.</exception>
        public bool TryRegister<TItem>(Inventory<TItem> inventory) where TItem : Item
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (_byType.ContainsKey(typeof(TItem))) return false;

            var entry = new Entry<TItem>(this, inventory);
            _byType.Add(typeof(TItem), entry);
            _ordered.Add(entry);
            RegistrationsChanged();
            return true;
        }

        /// <summary>
        /// Removes the inventory registered for <typeparamref name="TItem"/> and stops forwarding its
        /// events. The inventory and its contents are untouched.
        /// </summary>
        /// <returns>False when nothing was registered for <typeparamref name="TItem"/>.</returns>
        public bool Unregister<TItem>() where TItem : Item
        {
            if (!_byType.TryGetValue(typeof(TItem), out var entry)) return false;

            _byType.Remove(typeof(TItem));
            _ordered.Remove(entry);
            entry.Unsubscribe();
            RegistrationsChanged();
            return true;
        }

        /// <summary>
        /// Whether an inventory is registered for exactly <typeparamref name="TItem"/>.
        /// </summary>
        public bool IsRegistered<TItem>() where TItem : Item => _byType.ContainsKey(typeof(TItem));

        /// <summary>
        /// Whether an inventory is registered for exactly <paramref name="itemType"/>.
        /// </summary>
        public bool IsRegistered(Type itemType) => itemType != null && _byType.ContainsKey(itemType);

        /// <summary>
        /// Gets the inventory registered for exactly <typeparamref name="TItem"/>.
        /// </summary>
        public bool TryGetInventory<TItem>(out Inventory<TItem> inventory) where TItem : Item
        {
            if (_byType.TryGetValue(typeof(TItem), out var entry))
            {
                // The dictionary key is the exact type, so the entry is always an Entry<TItem>.
                inventory = ((Entry<TItem>)entry).Inventory;
                return true;
            }

            inventory = null;
            return false;
        }

        /// <summary>
        /// Gets the inventory registered for exactly <typeparamref name="TItem"/>.
        /// </summary>
        /// <exception cref="KeyNotFoundException">No inventory is registered for <typeparamref name="TItem"/>.</exception>
        public Inventory<TItem> GetInventory<TItem>() where TItem : Item
        {
            if (TryGetInventory<TItem>(out var inventory)) return inventory;
            throw new KeyNotFoundException($"No inventory is registered for {typeof(TItem).Name}.");
        }

        private void RegistrationsChanged()
        {
            _version++;
            _routes.Clear();

            var entries = _ordered.ToArray();
            var types = new Type[entries.Length];
            for (var i = 0; i < entries.Length; i++) types[i] = entries[i].ItemType;

            _entries = entries;
            _types = types;
            NotifyUpdated();
        }

        #endregion

        #region Routing

        private sealed class Route
        {
            // Every registered inventory that can hold the item, most specific first.
            public readonly Entry[] Chain;

            // The inventories an add may use: just the first of the chain, or the whole chain.
            public readonly Entry[] Eligible;

            public Route(Entry[] chain, Entry[] eligible)
            {
                Chain = chain;
                Eligible = eligible;
            }
        }

        private Route GetRoute(Item item)
        {
            if (item == null || _entries.Length == 0) return EmptyRoute;

            var type = item.GetType();
            if (_routes.TryGetValue(type, out var cached)) return cached;

            List<Entry> chain = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                if (!_byType.TryGetValue(t, out var entry)) continue;
                (chain ??= new List<Entry>()).Add(entry);
            }

            Route route;
            if (chain == null)
            {
                route = EmptyRoute;
            }
            else
            {
                var all = chain.ToArray();
                route = new Route(all, AllowFallback || all.Length == 1 ? all : new[] { all[0] });
            }

            _routes[type] = route;
            return route;
        }

        private static bool IsInstanceHeld(Item item, Entry[] chain)
        {
            if (!item.InstanceId.HasValue) return false;

            for (var i = 0; i < chain.Length; i++)
            {
                if (chain[i].ContainsInstance(item)) return true;
            }

            return false;
        }

        private static int Clamp(long value) => (int)Math.Min(value, int.MaxValue);

        #endregion

        #region Adding

        /// <summary>
        /// Whether an inventory is registered that an add of <paramref name="item"/> would use,
        /// ignoring space and inventory filters. Use <see cref="CanAdd"/> to ask whether the item
        /// would actually fit.
        /// </summary>
        public bool CanAccept(Item item) => GetRoute(item).Eligible.Length > 0;

        /// <summary>
        /// Units of <paramref name="item"/> that could be added right now: the free space of the most
        /// specific inventory, plus that of each less specific one when <see cref="AllowFallback"/> is
        /// true. Returns 0 for a null or unroutable item, and for a non-stackable item whose
        /// <see cref="Item.InstanceId"/> is already held by an inventory it could be added to.
        /// </summary>
        public int GetFreeSpaceFor(Item item)
        {
            var route = GetRoute(item);
            if (route.Eligible.Length == 0) return 0;
            if (IsInstanceHeld(item, route.Chain)) return 0;

            long total = 0;
            for (var i = 0; i < route.Eligible.Length; i++) total += route.Eligible[i].GetFreeSpaceFor(item);
            return Clamp(total);
        }

        /// <summary>
        /// Whether <see cref="TryAdd"/> would succeed with these arguments.
        /// </summary>
        public bool CanAdd(Item item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;
            if (!item.IsStackable && quantity > 1) return false;
            return quantity <= GetFreeSpaceFor(item);
        }

        /// <summary>
        /// Adds exactly <paramref name="quantity"/> units of <paramref name="item"/>, or nothing.
        /// </summary>
        /// <returns>True when the full quantity was added.</returns>
        /// <remarks>
        /// The item goes to the most specific registered inventory for its type; with
        /// <see cref="AllowFallback"/>, units that inventory cannot take spill into the next one, and
        /// so on. Atomic: if the eligible inventories cannot take the full quantity, none is touched
        /// and no event fires. A non-stackable item whose <see cref="Item.InstanceId"/> is already
        /// held by any inventory it could be added to is rejected, so one instance never lives in two
        /// inventories.
        /// </remarks>
        public bool TryAdd(Item item, int quantity = 1)
        {
            if (!CanAdd(item, quantity)) return false;

            AddCore(GetRoute(item).Eligible, item, quantity);
            return true;
        }

        /// <summary>
        /// Adds as many of <paramref name="quantity"/> units as fit. Adds at most 1 unit of a
        /// non-stackable item.
        /// </summary>
        /// <returns>Units actually added, 0 to <paramref name="quantity"/>.</returns>
        public int AddUpTo(Item item, int quantity)
        {
            if (item == null || quantity <= 0) return 0;

            var wanted = item.IsStackable ? quantity : Math.Min(quantity, 1);
            var toAdd = Math.Min(wanted, GetFreeSpaceFor(item));
            if (toAdd <= 0) return 0;

            return AddCore(GetRoute(item).Eligible, item, toAdd);
        }

        /// <summary>
        /// Adds one unit of each item in <paramref name="items"/>, best effort, in sequence order, as
        /// a single batch. Null entries are skipped and reported in <paramref name="notAdded"/>.
        /// </summary>
        /// <param name="items">The items to add, one unit each.</param>
        /// <param name="notAdded">Items that did not fit. Empty when every item was added.</param>
        /// <returns>Number of items added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
        public int AddRange(IEnumerable<Item> items, out IReadOnlyList<Item> notAdded)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var failed = new List<Item>();
            var addedCount = 0;

            using (BeginBatchUpdate())
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

        // Caller has validated the quantity against GetFreeSpaceFor.
        private int AddCore(Entry[] eligible, Item item, int quantity)
        {
            var remaining = quantity;

            using (OpenBatch(eligible))
            {
                for (var i = 0; i < eligible.Length && remaining > 0; i++)
                {
                    var take = Math.Min(remaining, eligible[i].GetFreeSpaceFor(item));
                    if (take <= 0) continue;
                    remaining -= eligible[i].AddUpTo(item, take);
                }
            }

            return quantity - remaining;
        }

        #endregion

        #region Removing

        /// <summary>
        /// Whether <see cref="TryRemove(Item,int)"/> would succeed with these arguments.
        /// </summary>
        public bool CanRemove(Item item, int quantity = 1)
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
        /// <see cref="IItemAttributes.UniqueId"/>, or nothing. Draws from the most specific inventory
        /// first, then down the item's chain; <see cref="AllowFallback"/> does not apply. Matches by
        /// kind, not by instance - use <see cref="TryRemoveInstance"/> for that.
        /// </summary>
        /// <returns>True when the full quantity was removed.</returns>
        public bool TryRemove(Item item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;

            var chain = GetRoute(item).Chain;
            if (QuantityOf(chain, item.UniqueId) < quantity) return false;

            RemoveCore(chain, item.UniqueId, quantity);
            return true;
        }

        /// <summary>
        /// Removes exactly <paramref name="quantity"/> units with this
        /// <see cref="IItemAttributes.UniqueId"/>, or nothing, drawing from the registered inventories
        /// in registration order.
        /// </summary>
        /// <returns>True when the full quantity was removed.</returns>
        public bool TryRemove(string uniqueId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(uniqueId) || quantity <= 0) return false;

            var entries = _entries;
            if (QuantityOf(entries, uniqueId) < quantity) return false;

            RemoveCore(entries, uniqueId, quantity);
            return true;
        }

        /// <summary>
        /// Removes as many of <paramref name="quantity"/> units as are held, as for
        /// <see cref="TryRemove(Item,int)"/>.
        /// </summary>
        /// <returns>Units actually removed.</returns>
        public int RemoveUpTo(Item item, int quantity)
        {
            if (item == null || quantity <= 0) return 0;

            var chain = GetRoute(item).Chain;
            var toRemove = Math.Min(quantity, QuantityOf(chain, item.UniqueId));
            return toRemove <= 0 ? 0 : RemoveCore(chain, item.UniqueId, toRemove);
        }

        /// <summary>
        /// Removes as many of <paramref name="quantity"/> units as are held, as for
        /// <see cref="TryRemove(string,int)"/>.
        /// </summary>
        /// <returns>Units actually removed.</returns>
        public int RemoveUpTo(string uniqueId, int quantity)
        {
            if (string.IsNullOrEmpty(uniqueId) || quantity <= 0) return 0;

            var entries = _entries;
            var toRemove = Math.Min(quantity, QuantityOf(entries, uniqueId));
            return toRemove <= 0 ? 0 : RemoveCore(entries, uniqueId, toRemove);
        }

        /// <summary>
        /// Removes the stack holding the item with <paramref name="item"/>'s
        /// <see cref="Item.InstanceId"/> from whichever inventory holds it. Always false for a
        /// stackable item, which has no instance identity.
        /// </summary>
        public bool TryRemoveInstance(Item item)
        {
            if (item == null || !item.InstanceId.HasValue) return false;

            var chain = GetRoute(item).Chain;
            for (var i = 0; i < chain.Length; i++)
            {
                if (chain[i].TryRemoveInstance(item)) return true;
            }

            return false;
        }

        private static int QuantityOf(Entry[] entries, string uniqueId)
        {
            long total = 0;
            for (var i = 0; i < entries.Length; i++) total += entries[i].GetQuantity(uniqueId);
            return Clamp(total);
        }

        // Caller has checked that at least `quantity` units are held.
        private int RemoveCore(Entry[] entries, string uniqueId, int quantity)
        {
            var remaining = quantity;

            using (OpenBatch(entries))
            {
                for (var i = 0; i < entries.Length && remaining > 0; i++)
                {
                    var take = Math.Min(remaining, entries[i].GetQuantity(uniqueId));
                    if (take <= 0) continue;
                    remaining -= entries[i].RemoveUpTo(uniqueId, take);
                }
            }

            return quantity - remaining;
        }

        #endregion

        #region Querying

        /// <summary>
        /// True when no registered inventory holds an item (including when none is registered).
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                var entries = _entries;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (!entries[i].IsEmpty) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Total units held across every registered inventory.
        /// </summary>
        public int TotalQuantity
        {
            get
            {
                long total = 0;
                var entries = _entries;
                for (var i = 0; i < entries.Length; i++) total += entries[i].TotalQuantity;
                return Clamp(total);
            }
        }

        /// <summary>
        /// The items held, inventory by inventory in registration order, then in slot order within
        /// each, skipping empty slots.
        /// </summary>
        /// <remarks>
        /// Like <see cref="Inventory{TItem}.Items"/>, enumeration throws
        /// <see cref="InvalidOperationException"/> if an inventory is modified, or one is registered
        /// or unregistered, while it is in progress. Collect what you want to change first.
        /// </remarks>
        public IEnumerable<Item> Items => EnumerateItems();

        private IEnumerable<Item> EnumerateItems()
        {
            var version = _version;
            var entries = _entries;

            for (var i = 0; i < entries.Length; i++)
            {
                if (version != _version) throw ModifiedDuringEnumeration();

                foreach (var item in entries[i].Items)
                {
                    yield return item;
                    if (version != _version) throw ModifiedDuringEnumeration();
                }
            }
        }

        private static InvalidOperationException ModifiedDuringEnumeration() =>
            new("The inventory collection was modified during enumeration.");

        /// <summary>
        /// Whether any inventory holds an item with this <see cref="IItemAttributes.UniqueId"/>.
        /// </summary>
        public bool Contains(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return false;

            var entries = _entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].GetQuantity(uniqueId) > 0) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether any held item satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool Contains(Func<Item, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var entries = _entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Contains(predicate)) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether an inventory holds an item with <paramref name="item"/>'s
        /// <see cref="Item.InstanceId"/>. Always false for a stackable item.
        /// </summary>
        public bool ContainsInstance(Item item)
        {
            if (item == null || !item.InstanceId.HasValue) return false;
            return IsInstanceHeld(item, GetRoute(item).Chain);
        }

        /// <summary>
        /// Total units held whose <see cref="IItemAttributes.UniqueId"/> matches
        /// <paramref name="item"/>'s, across the inventories that can hold that item.
        /// </summary>
        public int GetQuantity(Item item) => item == null ? 0 : QuantityOf(GetRoute(item).Chain, item.UniqueId);

        /// <summary>
        /// Total units held with this <see cref="IItemAttributes.UniqueId"/>, across every inventory.
        /// </summary>
        public int GetQuantity(string uniqueId) =>
            string.IsNullOrEmpty(uniqueId) ? 0 : QuantityOf(_entries, uniqueId);

        /// <summary>
        /// Total units held in stacks whose item satisfies <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public int GetQuantity(Func<Item, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            long total = 0;
            var entries = _entries;
            for (var i = 0; i < entries.Length; i++) total += entries[i].GetQuantity(predicate);
            return Clamp(total);
        }

        /// <summary>
        /// Finds the first held item satisfying <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool TryFind(Func<Item, bool> predicate, out Item item)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var entries = _entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].TryFind(predicate, out item)) return true;
            }

            item = null;
            return false;
        }

        /// <summary>
        /// Finds the first held item that is a <typeparamref name="TItem"/> and satisfies
        /// <paramref name="predicate"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public bool TryFind<TItem>(Func<TItem, bool> predicate, out TItem item) where TItem : Item
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            foreach (var candidate in Items)
            {
                if (candidate is not TItem typed || !predicate(typed)) continue;
                item = typed;
                return true;
            }

            item = null;
            return false;
        }

        /// <summary>
        /// All held items satisfying <paramref name="predicate"/>, lazily.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public IEnumerable<Item> FindAll(Func<Item, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return FindAllIterator(predicate);
        }

        private IEnumerable<Item> FindAllIterator(Func<Item, bool> predicate)
        {
            foreach (var item in Items)
            {
                if (predicate(item)) yield return item;
            }
        }

        /// <summary>
        /// All held items that are a <typeparamref name="TItem"/> and satisfy
        /// <paramref name="predicate"/>, lazily.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
        public IEnumerable<TItem> FindAll<TItem>(Func<TItem, bool> predicate) where TItem : Item
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return FindAllIterator(predicate);
        }

        private IEnumerable<TItem> FindAllIterator<TItem>(Func<TItem, bool> predicate) where TItem : Item
        {
            foreach (var item in Items)
            {
                if (item is TItem typed && predicate(typed)) yield return typed;
            }
        }

        /// <summary>
        /// Finds the held non-stackable item with this <see cref="Item.InstanceId"/>. Stackable items
        /// have no instance id and are never found.
        /// </summary>
        public bool TryFindByInstanceId(Guid instanceId, out Item item)
        {
            var entries = _entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].TryFindByInstanceId(instanceId, out item)) return true;
            }

            item = null;
            return false;
        }

        #endregion

        #region Batching and events

        /// <summary>
        /// Suppresses and coalesces events until the returned handle is disposed, by opening a batch
        /// on every registered inventory, so a run of operations reports as one change. Reentrant:
        /// only the outermost scope flushes. Disposing the handle twice is harmless.
        /// </summary>
        /// <remarks>
        /// An inventory registered while a batch is open is not part of it.
        /// </remarks>
        public IDisposable BeginBatchUpdate() => new BatchScope(this, _entries);

        // Internal operations only need a scope when more than one inventory can change; a single
        // inventory already reports as one change on its own.
        private IDisposable OpenBatch(Entry[] entries) => entries.Length > 1 ? new BatchScope(this, entries) : null;

        private void NotifyUpdated()
        {
            if (_batchDepth > 0)
            {
                _updatePending = true;
                return;
            }

            OnCollectionUpdated?.Invoke();
        }

        private void RaiseItemAdded(Item item, int quantity) => OnItemAdded?.Invoke(item, quantity);

        private void RaiseItemRemoved(Item item, int quantity) => OnItemRemoved?.Invoke(item, quantity);

        private sealed class BatchScope : IDisposable
        {
            private readonly InventoryCollection _owner;
            private IDisposable[] _handles;

            public BatchScope(InventoryCollection owner, Entry[] entries)
            {
                _owner = owner;
                owner._batchDepth++;
                _handles = new IDisposable[entries.Length];
                for (var i = 0; i < entries.Length; i++) _handles[i] = entries[i].BeginBatchUpdate();
            }

            public void Dispose()
            {
                // Idempotent: a second Dispose must not unbalance the depth counter.
                var handles = _handles;
                if (handles == null) return;
                _handles = null;

                // Every inventory must flush even if an earlier handler throws, and the depth must
                // come back down, or events stay suppressed for good. Surface the first exception last.
                ExceptionDispatchInfo first = null;

                for (var i = 0; i < handles.Length; i++)
                {
                    try
                    {
                        handles[i].Dispose();
                    }
                    catch (Exception e)
                    {
                        first ??= ExceptionDispatchInfo.Capture(e);
                    }
                }

                _owner._batchDepth--;

                if (_owner._batchDepth == 0 && _owner._updatePending)
                {
                    _owner._updatePending = false;
                    try
                    {
                        _owner.OnCollectionUpdated?.Invoke();
                    }
                    catch (Exception e)
                    {
                        first ??= ExceptionDispatchInfo.Capture(e);
                    }
                }

                first?.Throw();
            }
        }

        #endregion

        #region Entries

        // Type-erased adapter over one registered Inventory<TItem>, so the collection can hold
        // inventories of different item types and still drive them without reflection.
        private abstract class Entry
        {
            public abstract Type ItemType { get; }
            public abstract bool IsEmpty { get; }
            public abstract int TotalQuantity { get; }
            public abstract IEnumerable<Item> Items { get; }

            public abstract int GetFreeSpaceFor(Item item);
            public abstract int AddUpTo(Item item, int quantity);
            public abstract bool ContainsInstance(Item item);
            public abstract bool TryRemoveInstance(Item item);
            public abstract bool TryFindByInstanceId(Guid instanceId, out Item item);
            public abstract int GetQuantity(string uniqueId);
            public abstract int GetQuantity(Func<Item, bool> predicate);
            public abstract int RemoveUpTo(string uniqueId, int quantity);
            public abstract bool Contains(Func<Item, bool> predicate);
            public abstract bool TryFind(Func<Item, bool> predicate, out Item item);
            public abstract IDisposable BeginBatchUpdate();
            public abstract void Unsubscribe();
        }

        // Items passed to the Item-typed members are only ever routed here when their runtime type
        // derives from TItem (see GetRoute), so the casts below cannot fail.
        private sealed class Entry<TItem> : Entry where TItem : Item
        {
            private readonly InventoryCollection _owner;

            public Entry(InventoryCollection owner, Inventory<TItem> inventory)
            {
                _owner = owner;
                Inventory = inventory;
                inventory.OnItemAdded += HandleItemAdded;
                inventory.OnItemRemoved += HandleItemRemoved;
                inventory.OnInventoryUpdated += HandleInventoryUpdated;
            }

            public Inventory<TItem> Inventory { get; }

            public override Type ItemType => typeof(TItem);
            public override bool IsEmpty => Inventory.IsEmpty;
            public override int TotalQuantity => Inventory.TotalQuantity;
            public override IEnumerable<Item> Items => Inventory.Items;

            public override int GetFreeSpaceFor(Item item) => Inventory.GetFreeSpaceFor((TItem)item);
            public override int AddUpTo(Item item, int quantity) => Inventory.AddUpTo((TItem)item, quantity);
            public override bool ContainsInstance(Item item) => Inventory.ContainsInstance((TItem)item);
            public override bool TryRemoveInstance(Item item) => Inventory.TryRemoveInstance((TItem)item);

            public override bool TryFindByInstanceId(Guid instanceId, out Item item)
            {
                if (Inventory.TryFindByInstanceId(instanceId, out var found))
                {
                    item = found;
                    return true;
                }

                item = null;
                return false;
            }

            public override int GetQuantity(string uniqueId) => Inventory.GetQuantity(uniqueId);

            // Func<Item,bool> converts to Func<TItem,bool> by contravariance, so no wrapper is needed.
            public override int GetQuantity(Func<Item, bool> predicate) => Inventory.GetQuantity(predicate);
            public override int RemoveUpTo(string uniqueId, int quantity) => Inventory.RemoveUpTo(uniqueId, quantity);
            public override bool Contains(Func<Item, bool> predicate) => Inventory.Contains(predicate);

            public override bool TryFind(Func<Item, bool> predicate, out Item item)
            {
                if (Inventory.TryFind(predicate, out var found))
                {
                    item = found;
                    return true;
                }

                item = null;
                return false;
            }

            public override IDisposable BeginBatchUpdate() => Inventory.BeginBatchUpdate();

            public override void Unsubscribe()
            {
                Inventory.OnItemAdded -= HandleItemAdded;
                Inventory.OnItemRemoved -= HandleItemRemoved;
                Inventory.OnInventoryUpdated -= HandleInventoryUpdated;
            }

            private void HandleItemAdded(TItem item, int quantity) => _owner.RaiseItemAdded(item, quantity);
            private void HandleItemRemoved(TItem item, int quantity) => _owner.RaiseItemRemoved(item, quantity);
            private void HandleInventoryUpdated() => _owner.NotifyUpdated();
        }

        #endregion
    }
}
