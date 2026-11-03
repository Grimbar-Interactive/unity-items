using System;
using UnityEditor;

namespace GI.UnityToolkit.Items.Editor
{
    /// <summary>
    /// Re-checks for duplicate <see cref="IItemAttributes.UniqueId"/>s whenever an
    /// <see cref="ItemDefinition"/> asset is created, duplicated, saved or moved, so a copy-pasted
    /// definition is flagged immediately instead of surfacing as a stacking bug later.
    /// </summary>
    /// <remarks>
    /// Scanning is project-wide, so it runs once per import batch (via a queued callback) rather than
    /// per asset, and never from <c>OnValidate</c>, where it would repeat for every definition on
    /// every inspector edit.
    /// </remarks>
    internal sealed class ItemDefinitionDuplicatePostprocessor : AssetPostprocessor
    {
        private static bool _scanQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_scanQueued) return;
            if (!ContainsItemDefinition(importedAssets) && !ContainsItemDefinition(movedAssets)) return;

            _scanQueued = true;
            EditorApplication.delayCall += RunQueuedScan;
        }

        private static bool ContainsItemDefinition(string[] paths)
        {
            foreach (var path in paths)
            {
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null) return true;
            }

            return false;
        }

        private static void RunQueuedScan()
        {
            _scanQueued = false;
            ItemDefinitionValidator.ReportDuplicates(logWhenClean: false);
        }
    }
}
