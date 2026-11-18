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
        public readonly ItemDefinition Definition;
        
        private readonly Guid _instanceId;
        private readonly bool _isStackable;

        /// <summary>
        /// Identifies this specific object, or null when the item is stackable. Unique per
        /// constructed non-stackable instance and stable for its lifetime. Use for slot lookup
        /// (<see cref="Inventory{TItem}.TryFindByInstanceId"/>), UI drag handles, and as the
        /// save-game key for non-stackable items.
        /// </summary>
        /// <remarks>
        /// Stackable items are fungible, so they carry no identity: stacking merges units without
        /// keeping the incoming item, and one stackable reference may fill several slots. Whether an
        /// item is stackable is decided once, at construction, so this never changes for the
        /// lifetime of the object even if the attributes' <see cref="IItemAttributes.MaxStackSize"/>
        /// does. Flipping identity mid-life would leave one id in several slots.
        /// </remarks>
        public Guid? InstanceId => _isStackable ? null : _instanceId;

        /// <summary>
        /// Constructs an item from its edit-time definition.
        /// </summary>
        /// <param name="definition">The edit-time definition this instance is built from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        protected Item(ItemDefinition definition) : this(definition, Guid.NewGuid())
        {
        }

        /// <summary>
        /// Constructs an item with a known <see cref="InstanceId"/>, for restoring a saved item.
        /// </summary>
        /// <param name="definition">The edit-time definition this instance is built from.</param>
        /// <param name="instanceId">
        /// The identity to restore. Must not be <see cref="Guid.Empty"/>. Kept but not exposed while
        /// the item is stackable, since <see cref="InstanceId"/> is null then.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="instanceId"/> is <see cref="Guid.Empty"/>.</exception>
        protected Item(ItemDefinition definition, Guid instanceId)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (instanceId == Guid.Empty) throw new ArgumentException("Instance id must not be empty.", nameof(instanceId));
            _instanceId = instanceId;
            _isStackable = definition.MaxStackSize > 1;
        }

        /// <inheritdoc cref="IItemAttributes.UniqueId"/>
        public string UniqueId => Definition.UniqueId;

        /// <inheritdoc cref="IItemAttributes.Name"/>
        public string Name => Definition.Name;

        /// <inheritdoc cref="IItemAttributes.Description"/>
        public string Description => Definition.Description;

        /// <inheritdoc cref="IItemAttributes.Icon"/>
        public Sprite Icon => Definition.Icon;

        /// <summary>
        /// Whether more than one unit may share a slot: true exactly when the attributes'
        /// <see cref="IItemAttributes.MaxStackSize"/> was above 1 when the item was constructed.
        /// Fixed for the item's lifetime and not overridable, so every stacking decision agrees.
        /// </summary>
        public bool IsStackable => _isStackable;

        /// <summary>
        /// Units of this item that fit in a single slot: the attributes'
        /// <see cref="IItemAttributes.MaxStackSize"/> clamped to a minimum of 1, so a misconfigured
        /// definition can never yield a zero-capacity slot. The limit of a stackable item may change
        /// at runtime (stacks above a shrunken limit simply have no room left); a non-stackable item
        /// is always 1, even if its attributes later grow.
        /// </summary>
        public int MaxStackSize => _isStackable ? Math.Max(1, Definition.MaxStackSize) : 1;

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
    /// <typeparam name="TDef">
    /// The definition type. Constrained to <see cref="ItemDefinition"/>, so this assembly carries no
    /// dependency on project code. The definition passed to the constructor must be of this type.
    /// </typeparam>
    public abstract class Item<TDef> : Item where TDef : ItemDefinition
    {
        /// <summary>
        /// The typed definition this item was built from. Never null.
        /// </summary>
        public new TDef Definition => (TDef)base.Definition;

        /// <summary>
        /// Constructs an item from its typed edit-time definition.
        /// </summary>
        /// <param name="definition">The typed definition this instance is built from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        protected Item(TDef definition) : base(definition)
        {
        }

        /// <summary>
        /// Constructs an item from its typed definition with a known <see cref="Item.InstanceId"/>,
        /// for restoring a saved item.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="instanceId"/> is <see cref="Guid.Empty"/>.</exception>
        protected Item(TDef definition, Guid instanceId) : base(definition, instanceId)
        {
        }
    }
}
