using System.Collections.Generic;
using System.Reflection;
using GI.UnityToolkit.Items.Test;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

[assembly: DestroyFakeDefinitions]

namespace GI.UnityToolkit.Items.Test
{
    /// <summary>
    /// A configurable <see cref="ItemDefinition"/> for tests. Instances are tracked and destroyed
    /// after every test by <see cref="DestroyFakeDefinitionsAttribute"/>.
    /// </summary>
    internal sealed class FakeItemDefinition : ItemDefinition<TestItem>
    {
        private static readonly List<FakeItemDefinition> Created = new();

        private int _maxStackSize = 1;

        public static FakeItemDefinition Create(string uniqueId, int maxStackSize = 1, string name = null, string description = null)
        {
            var definition = CreateInstance<FakeItemDefinition>();
            Set(definition, nameof(UniqueId), uniqueId);
            Set(definition, nameof(Name), name);
            Set(definition, nameof(Description), description);
            definition._maxStackSize = maxStackSize;
            Created.Add(definition);
            return definition;
        }

        public override int MaxStackSize => _maxStackSize;

        /// <summary>
        /// Changes the stack limit at any time, with no clamping, so tests can shrink or grow it
        /// after items were built.
        /// </summary>
        public void SetMaxStackSize(int value) => _maxStackSize = value;

        protected override TestItem Construct() => new TestItem(this);

        public static void DestroyAll()
        {
            foreach (var definition in Created)
            {
                if (definition != null) DestroyImmediate(definition);
            }

            Created.Clear();
        }

        private static void Set(FakeItemDefinition definition, string propertyName, string value)
        {
            typeof(ItemDefinition)
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(definition, value);
        }
    }

    /// <summary>
    /// Assembly-wide NUnit action that destroys every <see cref="FakeItemDefinition"/> created by a test.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    internal sealed class DestroyFakeDefinitionsAttribute : System.Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
        }

        public void AfterTest(ITest test) => FakeItemDefinition.DestroyAll();
    }
}
