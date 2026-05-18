using System;
using System.Collections.Generic;
using System.Linq;
using DoaneDevTools.ToolWindows.TestDataSeeder.Models;

namespace DoaneDevTools.ToolWindows.TestDataSeeder.Services
{
    /// <summary>Kahn's algorithm topological sort on the FK dependency graph.</summary>
    public static class TopologicalSortService
    {
        /// <summary>
        /// Returns tables sorted so parent tables come before child tables.
        /// Tables in circular FK chains are flagged and placed at end.
        /// </summary>
        public static (List<TableSeedConfig> Sorted, List<string> CircularChains) Sort(
            IEnumerable<TableSeedConfig> tables)
        {
            var tableList = tables.ToList();
            var tableMap = tableList.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

            // Build adjacency: edge from dep → table (dep must come before table)
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tableList)
            {
                if (!inDegree.ContainsKey(t.TableName)) inDegree[t.TableName] = 0;
                if (!dependents.ContainsKey(t.TableName)) dependents[t.TableName] = new List<string>();
            }

            foreach (var t in tableList)
            {
                foreach (var dep in t.ForeignKeyDependencies)
                {
                    if (!tableMap.ContainsKey(dep)) continue;
                    if (!dependents.ContainsKey(dep)) dependents[dep] = new List<string>();
                    dependents[dep].Add(t.TableName);
                    inDegree[t.TableName] = inDegree.GetValueOrDefault(t.TableName) + 1;
                }
            }

            // Kahn's BFS
            var queue = new Queue<string>(
                inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var sorted = new List<TableSeedConfig>();

            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (tableMap.TryGetValue(name, out var table))
                    sorted.Add(table);

                foreach (var dependent in dependents.GetValueOrDefault(name) ?? new List<string>())
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        queue.Enqueue(dependent);
                }
            }

            // Any remaining tables are in cycles
            var cycleNames = inDegree
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var name in cycleNames)
                if (tableMap.TryGetValue(name, out var table))
                    sorted.Add(table);

            var circularChains = cycleNames.Any()
                ? new List<string> { $"Circular FK chain detected among: {string.Join(", ", cycleNames)}" }
                : new List<string>();

            return (sorted, circularChains);
        }
    }
}
