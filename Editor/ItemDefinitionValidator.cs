using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GI.UnityToolkit.Items.Editor
{
    /// <summary>
    /// Finds <see cref="ItemDefinition"/> assets that share a <see cref="IItemAttributes.UniqueId"/>.
    /// Two kinds with one id are treated as the same kind by every inventory: they stack together,
    /// are removed together, and collide in saves.
    /// </summary>
    public static class ItemDefinitionValidator
    {
        /// <summary>
        /// Groups <paramref name="definitions"/> by <see cref="IItemAttributes.UniqueId"/> and returns
        /// only the ids used more than once, ordered by id. Comparison is ordinal and case-sensitive,
        /// matching how inventories compare ids. Null entries and null or empty ids are skipped (an
        /// empty id is a different problem, already warned about by <see cref="ItemDefinition"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="definitions"/> is null.</exception>
        public static IReadOnlyDictionary<string, IReadOnlyList<T>> FindDuplicateUniqueIds<T>(IEnumerable<T> definitions)
            where T : class, IItemAttributes
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var byId = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (ReferenceEquals(definition, null)) continue;

                var id = definition.UniqueId;
                if (string.IsNullOrEmpty(id)) continue;

                if (!byId.TryGetValue(id, out var group))
                {
                    group = new List<T>();
                    byId.Add(id, group);
                }

                group.Add(definition);
            }

            var duplicates = new SortedDictionary<string, IReadOnlyList<T>>(StringComparer.Ordinal);
            foreach (var pair in byId)
            {
                if (pair.Value.Count > 1) duplicates.Add(pair.Key, pair.Value);
            }

            return duplicates;
        }

        /// <summary>
        /// Scans every <see cref="ItemDefinition"/> asset in the project for shared ids.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<ItemDefinition>> FindDuplicateUniqueIdsInProject()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(ItemDefinition)}");
            var definitions = new List<ItemDefinition>(guids.Length);

            foreach (var guid in guids)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null) definitions.Add(definition);
            }

            return FindDuplicateUniqueIds(definitions);
        }

        /// <summary>
        /// Scans the project and logs one warning per duplicated id, listing every asset involved.
        /// Clicking the warning pings the first asset.
        /// </summary>
        /// <param name="logWhenClean">Also log a confirmation when there are no duplicates.</param>
        /// <returns>Number of duplicated ids found.</returns>
        public static int ReportDuplicates(bool logWhenClean)
        {
            var duplicates = FindDuplicateUniqueIdsInProject();

            if (duplicates.Count == 0)
            {
                if (logWhenClean) Debug.Log("No duplicate ItemDefinition UniqueIds found.");
                return 0;
            }

            foreach (var pair in duplicates)
            {
                var message = new StringBuilder();
                message.Append("ItemDefinition UniqueId '").Append(pair.Key).Append("' is shared by ")
                    .Append(pair.Value.Count).Append(" assets. Items with one id are treated as the same kind:");

                foreach (var definition in pair.Value)
                {
                    message.Append("\n  ").Append(AssetDatabase.GetAssetPath(definition));
                }

                Debug.LogWarning(message.ToString(), pair.Value[0]);
            }

            return duplicates.Count;
        }

        [MenuItem("Grimbar Interactive/Items/Validate Unique Ids")]
        private static void ValidateFromMenu() => ReportDuplicates(logWhenClean: true);
    }
}
