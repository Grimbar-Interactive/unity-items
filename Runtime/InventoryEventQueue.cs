using System;
using System.Collections.Generic;
// ReSharper disable ForCanBeConvertedToForeach

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// The container side of <see cref="InventoryEventQueue{TItem}"/>: where queued notifications are
    /// finally raised. Implemented explicitly by <see cref="Inventory{TItem}"/> and
    /// <see cref="LinkedInventory{TItem}"/> so it adds nothing to their public surface.
    /// </summary>
    internal interface IInventoryEventSink<TItem> where TItem : Item
    {
        /// <summary>
        /// Live slot count, used to skip slot indices that no longer exist when a flush runs.
        /// </summary>
        int SlotCount { get; }

        void RaiseItemAdded(TItem item, int quantity);
        void RaiseItemRemoved(TItem item, int quantity);
        void RaiseSlotChanged(int index);
        void RaiseInventoryUpdated();

        /// <summary>
        /// Internal notification for dependents that must stay in sync with this container (for
        /// example a linked inventory following its source). Raised after the public events, and
        /// even when a public handler throws.
        /// </summary>
        void RaiseSynchronize();
    }

    /// <summary>
    /// Batching and event coalescing shared by every inventory type. Mutating code enqueues
    /// notifications; the outermost batch scope flushes them in a fixed order (item events, slot
    /// events ascending and de-duplicated, then one update).
    /// </summary>
    /// <remarks>
    /// Pending notifications are held in plain lists and flushed through swap-in spare buffers, so a
    /// steady-state add/remove allocates nothing and a flush stays safe against re-entrancy.
    /// </remarks>
    internal sealed class InventoryEventQueue<TItem> where TItem : Item
    {
        private readonly IInventoryEventSink<TItem> _sink;

        private List<PendingItemEvent> _pendingItemEvents = new();
        private List<PendingItemEvent> _spareItemEvents;
        private List<int> _pendingSlots = new();
        private List<int> _spareSlots;
        private bool _pendingInventoryUpdated;
        private int _batchDepth;

        /// <summary>
        /// Incremented by every mutation. Enumerators compare it to detect modification.
        /// </summary>
        public int Version;

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

        public InventoryEventQueue(IInventoryEventSink<TItem> sink)
        {
            _sink = sink;
        }

        public void EnqueueItemAdded(TItem item, int quantity)
        {
            Version++;
            _pendingItemEvents.Add(new PendingItemEvent(true, item, quantity));
            _pendingInventoryUpdated = true;
        }

        public void EnqueueItemRemoved(TItem item, int quantity)
        {
            Version++;
            _pendingItemEvents.Add(new PendingItemEvent(false, item, quantity));
            _pendingInventoryUpdated = true;
        }

        public void MarkSlotChanged(int index)
        {
            Version++;

            // Duplicates are collapsed at flush time; skipping an immediate repeat keeps hot loops small.
            var pending = _pendingSlots;
            if (pending.Count == 0 || pending[^1] != index) pending.Add(index);
            _pendingInventoryUpdated = true;
        }

        public Batch OpenBatch() => new Batch(this);

        public IDisposable BeginBatchScope() => new BatchScope(this);

        private void FlushPending()
        {
            var events = _pendingItemEvents;
            var slots = _pendingSlots;
            var updated = _pendingInventoryUpdated;
            _pendingInventoryUpdated = false;

            if (events.Count == 0 && slots.Count == 0 && !updated) return;

            // Hand fresh (or recycled) buffers to the queue before raising anything, so a
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
                    if (pending.Added)
                    {
                        _sink.RaiseItemAdded(pending.Item, pending.Quantity);
                    }
                    else
                    {
                        _sink.RaiseItemRemoved(pending.Item, pending.Quantity);
                    }
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
                        if (index == previous || index >= _sink.SlotCount) continue;
                        previous = index;
                        _sink.RaiseSlotChanged(index);
                    }
                }

                if (updated) _sink.RaiseInventoryUpdated();
            }
            finally
            {
                events.Clear();
                slots.Clear();
                _spareItemEvents = events;
                _spareSlots = slots;

                // Dependents must hear about the change even if a public handler threw above,
                // otherwise they keep state the container no longer backs.
                if (updated) _sink.RaiseSynchronize();
            }
        }

        /// <summary>
        /// Allocation-free batch scope for internal operations. Not idempotent - dispose exactly once,
        /// which <c>using</c> guarantees. The public batch handle is the idempotent
        /// <see cref="BatchScope"/> class instead.
        /// </summary>
        public readonly struct Batch : IDisposable
        {
            private readonly InventoryEventQueue<TItem> _queue;

            public Batch(InventoryEventQueue<TItem> queue)
            {
                _queue = queue;
                queue._batchDepth++;
            }

            public void Dispose()
            {
                _queue._batchDepth--;
                if (_queue._batchDepth == 0) _queue.FlushPending();
            }
        }

        private sealed class BatchScope : IDisposable
        {
            private readonly InventoryEventQueue<TItem> _queue;
            private bool _disposed;

            public BatchScope(InventoryEventQueue<TItem> queue)
            {
                _queue = queue;
                _queue._batchDepth++;
            }

            public void Dispose()
            {
                // Idempotent: a second Dispose must not unbalance the depth counter.
                if (_disposed) return;
                _disposed = true;

                _queue._batchDepth--;
                if (_queue._batchDepth == 0) _queue.FlushPending();
            }
        }
    }
}
