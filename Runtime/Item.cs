using System;
using UnityEngine;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// A runtime instance of an item. Carries identity and per-instance state only - how many units
    /// are held is owned by the <see cref="Inventory{TItem}"/> slot, never by the item.
    /// </summary>
    /// <remarks>
    /// <see cref="object.Equals(object)"/> is deliberately NOT overridden. Use
    /// <see cref="UniqueId"/> to ask what kind an item is and <see cref="CanStackWith"/> to ask
    /// whether two items are interchangeable.
    /// <para>
    /// <b>Two identity models.</b> A stackable item (<see cref="MaxStackSize"/> above 1) is
    /// fungible: it has no identity of its own, <see cref="InstanceId"/> is null, and an inventory
    /// compares it by <see cref="UniqueId"/>. A non-stackable item is a distinct object: it has an
    /// <see cref="InstanceId"/>, and an inventory compares it by that id.
    /// </para>
    /// <para>
    /// Invariant for subclasses: if your item carries mutable per-instance state that players can
    /// distinguish, either make it non-stackable or override <see cref="CanStackWith"/> to require
    /// that state to match. A stackable item is treated as fungible.
    /// </para>
    /// </remarks>
    public abstract class Item : IItemAttributes
    {
        private readonly IItemAttributes _attributes;
        private readonly Guid _instanceId;

        /// <summary>
        /// Identifies this specific object, or null when the item is stackable. Unique per
        /// constructed non-stackable instance and stable for its lifetime. Use for slot lookup
        /// (<see cref="Inventory{TItem}.TryFindByInstanceId"/>), UI drag handles, and as the
        /// save-game key for non-stackable items.
        /// </summary>
        /// <remarks>
        /// Stackable items are fungible, so they carry no identity: stacking merges units without
        /// keeping the incoming item, and one stackable reference may fill several slots. The value
        /// follows <see cref="IsStackable"/> live, so if an attributes object's
        /// <see cref="IItemAttributes.MaxStackSize"/> changes at runtime, so does this.
        /// </remarks>
        public Guid? InstanceId => IsStackable ? null : _instanceId;

        /// <summary>
        /// Constructs an item from its edit-time attributes.
        /// </summary>
        /// <param name="attributes">The edit-time attributes this instance is built from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="attributes"/> is null.</exception>
        protected Item(IItemAttributes attributes) : this(attributes, Guid.NewGuid())
        {
        }

        /// <summary>
        /// Constructs an item with a known <see cref="InstanceId"/>, for restoring a saved item.
        /// </summary>
        /// <param name="attributes">The edit-time attributes this instance is built from.</param>
        /// <param name="instanceId">
        /// The identity to restore. Must not be <see cref="Guid.Empty"/>. Kept but not exposed while
        /// the item is stackable, since <see cref="InstanceId"/> is null then.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="attributes"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="instanceId"/> is <see cref="Guid.Empty"/>.</exception>
        protected Item(IItemAttributes attributes, Guid instanceId)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            if (instanceId == Guid.Empty) throw new ArgumentException("Instance id must not be empty.", nameof(instanceId));
            _instanceId = instanceId;
        }

        /// <inheritdoc cref="IItemAttributes.UniqueId"/>
        public string UniqueId => _attributes.UniqueId;

        /// <inheritdoc cref="IItemAttributes.Name"/>
        public string Name => _attributes.Name;

        /// <inheritdoc cref="IItemAttributes.Description"/>
        public string Description => _attributes.Description;

        /// <inheritdoc cref="IItemAttributes.Icon"/>
        public Sprite Icon => _attributes.Icon;

        /// <summary>
        /// Whether more than one unit may share a slot: true exactly when <see cref="MaxStackSize"/>
        /// is above 1. Not overridable, so every stacking decision agrees.
        /// </summary>
        public bool IsStackable => MaxStackSize > 1;

        /// <summary>
        /// Units of this item that fit in a single slot: the attributes'
        /// <see cref="IItemAttributes.MaxStackSize"/> clamped to a minimum of 1, so a misconfigured
        /// definition can never yield a zero-capacity slot.
        /// </summary>
        public int MaxStackSize => Math.Max(1, _attributes.MaxStackSize);

        /// <summary>
        /// Whether this item may share an inventory slot with <paramref name="other"/>.
        /// Default: both items are stackable and share the same <see cref="UniqueId"/>.
        /// </summary>
        /// <param name="other">The item already occupying the candidate slot. May be null.</param>
        /// <returns>True if the two items are interchangeable and may be merged into one stack.</returns>
        /// <remarks>
        /// Override to additionally require per-instance state to match (durability, enchantment,
        /// applied patches). Overrides must be symmetric - <c>a.CanStackWith(b)</c> must equal
        /// <c>b.CanStackWith(a)</c> - because the inventory calls it in whichever direction is
        /// convenient. Must return false for a null argument, and must never return true for items
        /// with different <see cref="UniqueId"/>s - the inventory also checks this when adding.
        /// </remarks>
        public virtual bool CanStackWith(Item other)
        {
            if (other == null) return false;
            if (!IsStackable || !other.IsStackable) return false;
            return string.Equals(UniqueId, other.UniqueId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns a diagnostic string of the form <c>DiscItem(disc_destroyer)</c>.
        /// </summary>
        public override string ToString() => $"{GetType().Name}({UniqueId})";
    }

    /// <summary>
    /// An <see cref="Item"/> that keeps a strongly typed reference to the attributes it was built from.
    /// </summary>
    /// <typeparam name="T">
    /// The attributes type. Constrained to <see cref="IItemAttributes"/> - deliberately NOT to a
    /// project-specific definition type - so this assembly carries no dependency on project code or
    /// any ScriptableObject base.
    /// </typeparam>
    public abstract class Item<T> : Item where T : class, IItemAttributes
    {
        /// <summary>
        /// The typed attributes this item was built from. Never null.
        /// </summary>
        public T Definition { get; }

        /// <summary>
        /// Constructs an item from its typed edit-time definition.
        /// </summary>
        /// <param name="definition">The typed attributes this instance is built from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        protected Item(T definition) : base(definition)
        {
            Definition = definition;
        }

        /// <summary>
        /// Constructs an item from its typed definition with a known <see cref="Item.InstanceId"/>,
        /// for restoring a saved item.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="instanceId"/> is <see cref="Guid.Empty"/>.</exception>
        protected Item(T definition, Guid instanceId) : base(definition, instanceId)
        {
            Definition = definition;
        }
    }
}
