using System;
using UnityEngine;

namespace GI.UnityToolkit.Items.Test
{
    internal sealed class PlainTestDefinition : ItemDefinition
    {
        public void RunOnValidate() => OnValidate();
    }

    internal sealed class StackableTestDefinition : StackableItemDefinition
    {
        public void RunOnValidate() => OnValidate();

        /// <summary>
        /// Writes the serialized field the way the inspector would, bypassing OnValidate.
        /// </summary>
        public void SetMaxStackSize(int value) =>
            JsonUtility.FromJsonOverwrite($"{{\"maxStackSize\":{value}}}", this);
    }

    internal sealed class DefinitionTestItem : Item<ItemDefinition>
    {
        public DefinitionTestItem(ItemDefinition definition) : base(definition)
        {
        }

        public DefinitionTestItem(ItemDefinition definition, Guid instanceId) : base(definition, instanceId)
        {
        }
    }
}
