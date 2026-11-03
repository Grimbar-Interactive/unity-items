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

        /// <inheritdoc cref="IItemAttributes.IsStackable"/>
        [field: SerializeField] public virtual bool IsStackable { get; private set; }

        /// <inheritdoc cref="IItemAttributes.MaxStackSize"/>
        [field: SerializeField, Min(1)] public virtual int MaxStackSize { get; private set; } = 1;

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
