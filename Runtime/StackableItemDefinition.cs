using System;
using UnityEngine;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// Base class for item definitions with a configurable stack limit. A plain
    /// <see cref="ItemDefinition"/> is never stackable; derive from this class to get a serialized
    /// stack limit of at least 1. A limit of 1 means items of that definition do not stack, so
    /// inheritors can let individual items opt out of stacking.
    /// </summary>
    public abstract class StackableItemDefinition : ItemDefinition
    {
        /// <summary>
        /// The smallest stack limit a definition may have. A limit of 1 makes the definition
        /// non-stackable (see <see cref="ItemDefinition.IsStackable"/>).
        /// </summary>
        public const int MINIMUM_STACK_SIZE = 1;

        /// <summary>
        /// The stack limit a newly created definition starts with.
        /// </summary>
        public const int DEFAULT_STACK_SIZE = 64;

        [Tooltip("Maximum units of this item in one inventory slot. Must be at least 1; 1 means the item does not stack.")]
        [SerializeField, Min(MINIMUM_STACK_SIZE)] private int maxStackSize = DEFAULT_STACK_SIZE;

        /// <inheritdoc cref="IItemAttributes.MaxStackSize"/>
        public override int MaxStackSize => maxStackSize;

        /// <summary>
        /// Editor-only validation: keeps the stack limit at or above <see cref="MINIMUM_STACK_SIZE"/>,
        /// then runs <see cref="ItemDefinition.OnValidate"/>.
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();
#if UNITY_EDITOR
            maxStackSize = Math.Max(MINIMUM_STACK_SIZE, maxStackSize);
#endif
        }
    }
}
