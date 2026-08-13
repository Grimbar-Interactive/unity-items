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
        [field: SerializeField] public virtual int MaxStackSize { get; private set; }
    }
}