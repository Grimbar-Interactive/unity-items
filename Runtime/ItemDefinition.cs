using GI.UnityToolkit.Variables;
using UnityEngine;

namespace GI.UnityToolkit.Items
{
    /// <summary>
    /// Base class for all item definitions.
    /// </summary>
    public abstract class ItemDefinition : DataObject, IItemAttributes
    {
        /// <inheritdoc cref="IItemAttributes.UniqueId"/>
        [field: SerializeField] public string UniqueId { get; private set;}

        /// <inheritdoc cref="IItemAttributes.Name"/>
        [field: SerializeField] public string Name { get; private set; }

        /// <inheritdoc cref="IItemAttributes.Description"/>
        [field: SerializeField] public string Description { get; private set; }

        /// <inheritdoc cref="IItemAttributes.Icon"/>
        [field: SerializeField] public Sprite Icon { get; private set; }

        /// <inheritdoc cref="IItemAttributes.MaxStackSize"/>
        /// <remarks>
        /// Always 1 here, so a plain definition is not stackable. Derive from
        /// <see cref="StackableItemDefinition"/> to make items stack.
        /// </remarks>
        public virtual int MaxStackSize => 1;

        /// <summary>
        /// Whether items built from this definition will stack: <see cref="MaxStackSize"/> above 1.
        /// A convenience for editor and UI code that has no constructed <see cref="Item"/>; it is
        /// deliberately not part of <see cref="IItemAttributes"/>, and <see cref="Item.IsStackable"/>
        /// remains the authority for a live item.
        /// </summary>
        public bool IsStackable => MaxStackSize > 1;

        /// <summary>
        /// Editor-only validation: trims <see cref="UniqueId"/> and warns when it is missing, since
        /// an empty id breaks stacking and lookup. Override and call the base when extending.
        /// </summary>
        /// <remarks>
        /// Declared in every build, with only the body compiled out of players, so a subclass that
        /// overrides it compiles everywhere without wrapping its own override in <c>#if</c>. Unity
        /// only ever calls it in the editor.
        /// </remarks>
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            UniqueId = UniqueId?.Trim();

            if (string.IsNullOrEmpty(UniqueId))
            {
                Debug.LogWarning($"{GetType().Name} '{name}' has no UniqueId. Items need one to stack and be looked up.", this);
            }
#endif
        }
    }
}
