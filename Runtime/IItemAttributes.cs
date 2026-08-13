using UnityEngine;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// The read-only, edit-time description of a kind of item. Implemented by ScriptableObject
    /// definitions in game code and by lightweight plain classes in tests.
    /// </summary>
    public interface IItemAttributes
    {
        /// <summary>
        /// Stable identifier shared by every item of this kind. Drives stacking equivalence and
        /// lookup, so it must be unique per kind and never change at runtime.
        /// </summary>
        string UniqueId { get; }

        /// <summary>
        /// Player-facing display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Player-facing description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Icon shown in inventory UI. May be null.
        /// </summary>
        Sprite Icon { get; }

        /// <summary>
        /// Whether more than one unit of this kind may share a single inventory slot.
        /// </summary>
        bool IsStackable { get; }

        /// <summary>
        /// Maximum units of this kind in one slot. Ignored when <see cref="IsStackable"/> is false;
        /// use <see cref="Item.StackLimit"/> for the value an inventory actually honors.
        /// </summary>
        int MaxStackSize { get; }
    }
}
