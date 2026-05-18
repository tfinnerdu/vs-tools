using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.DependencyMap.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DoaneDevTools.ToolWindows.DependencyMap.Services
{
    /// <summary>
    /// Builds a dependency graph from a Roslyn <see cref="Compilation"/>, computes
    /// code-quality metrics per type, detects circular dependencies, and calculates
    /// transitive-impact sets for "What breaks if I change X?" queries.
    /// </summary>
    public class RoslynGraphBuilder
    {
        // -----------------------------------------------------------------------
        // Public entry points
        // -----------------------------------------------------------------------

        /// <summary>
        /// Analyses <paramref name="compilation"/> and returns populated
        /// <see cref="GraphNode"/> and <see cref="GraphEdge"/> collections
        /// ready for layout and rendering.
        /// </summary>
        public async Task<(List<GraphNode> nodes, List<GraphEdge> edges)> BuildAsync(
            Compilation compilation,
            NodeType scope)
        {
            var typeDeps = await Task.Run(() => BuildTypeDependencies(compilation));
            var metrics  = await Task.Run(() => ComputeMetrics(compilation));

            var nodes = new List<GraphNode>();
            var edges = new List<GraphEdge>();

            // ── Build nodes ──────────────────────────────────────────────────
            var symbolToId = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

            foreach (var symbol in typeDeps.Keys)
            {
                var id = MakeId(symbol);
                symbolToId[symbol] = id;

                var node = new GraphNode
                {
                    Id          = id,
                    Name        = symbol.Name,
                    FullName    = symbol.ToDisplayString(),
                    Type        = NodeType.Class,
                    AssemblyName = symbol.ContainingAssembly?.Name ?? string.Empty
                };

                if (metrics.TryGetValue(id, out var m))
                {
                    node.LinesOfCode          = m.LinesOfCode;
                    node.CyclomaticComplexity = m.CyclomaticComplexity;
                    node.EfferentCoupling     = typeDeps[symbol].Count;
                }

                nodes.Add(node);
            }

            // ── Afferent coupling pass ───────────────────────────────────────
            foreach (var (source, targets) in typeDeps)
            {
                foreach (var target in targets)
                {
                    var targetNode = nodes.FirstOrDefault(n =>
                        n.Id == (symbolToId.TryGetValue(target, out var tid) ? tid : string.Empty));
                    if (targetNode != null)
                        targetNode.AfferentCoupling++;
                }
            }

            // ── Build edges ──────────────────────────────────────────────────
            foreach (var (source, targets) in typeDeps)
            {
                if (!symbolToId.TryGetValue(source, out var srcId)) continue;

                foreach (var target in targets)
                {
                    if (!symbolToId.TryGetValue(target, out var tgtId)) continue;

                    var edgeType = DetermineEdgeType(source, target);
                    edges.Add(new GraphEdge
                    {
                        SourceId  = srcId,
                        TargetId  = tgtId,
                        Type      = edgeType
                    });
                }
            }

            // ── Mark cycle participants ──────────────────────────────────────
            var cycles = DetectCycles(typeDeps, symbolToId);
            foreach (var cycle in cycles)
            {
                foreach (var id in cycle)
                {
                    var node = nodes.FirstOrDefault(n => n.Id == id);
                    if (node != null) node.IsInCycle = true;
                }
            }

            return (nodes, edges);
        }

        // -----------------------------------------------------------------------
        // Cycle detection (DFS / Tarjan-based path tracking)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Detects all cycles in the dependency graph using depth-first search.
        /// Returns a list of cycle paths, where each path is a list of node IDs
        /// forming the cycle (first ID == last ID).
        /// </summary>
        public List<List<string>> DetectCycles(
            Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> graph,
            Dictionary<ITypeSymbol, string> symbolToId)
        {
            var cycles    = new List<List<string>>();
            var visited   = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var inStack   = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var pathStack = new Stack<string>();

            foreach (var symbol in graph.Keys)
            {
                if (!visited.Contains(symbol))
                    DfsCycle(symbol, graph, symbolToId, visited, inStack, pathStack, cycles);
            }

            return cycles;
        }

        private static void DfsCycle(
            ITypeSymbol current,
            Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> graph,
            Dictionary<ITypeSymbol, string> symbolToId,
            HashSet<ITypeSymbol> visited,
            HashSet<ITypeSymbol> inStack,
            Stack<string> pathStack,
            List<List<string>> cycles)
        {
            visited.Add(current);
            inStack.Add(current);

            if (symbolToId.TryGetValue(current, out var currentId))
                pathStack.Push(currentId);

            if (graph.TryGetValue(current, out var neighbours))
            {
                foreach (var neighbour in neighbours)
                {
                    if (!visited.Contains(neighbour))
                    {
                        DfsCycle(neighbour, graph, symbolToId, visited, inStack, pathStack, cycles);
                    }
                    else if (inStack.Contains(neighbour) &&
                             symbolToId.TryGetValue(neighbour, out var neighbourId))
                    {
                        // Found a back edge — extract the cycle path.
                        var cyclePath = new List<string>();
                        var stackArr  = pathStack.ToArray();
                        bool inCycle  = false;

                        for (int i = stackArr.Length - 1; i >= 0; i--)
                        {
                            if (stackArr[i] == neighbourId) inCycle = true;
                            if (inCycle) cyclePath.Add(stackArr[i]);
                        }

                        cyclePath.Add(neighbourId); // close the cycle
                        cycles.Add(cyclePath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentId))
                pathStack.TryPop(out _);

            inStack.Remove(current);
        }

        // -----------------------------------------------------------------------
        // Metrics computation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Computes per-type metrics (LOC, cyclomatic complexity) by walking syntax trees.
        /// Returns a dictionary keyed by the node ID string used in <see cref="BuildAsync"/>.
        /// </summary>
        public Dictionary<string, TypeMetrics> ComputeMetrics(Compilation compilation)
        {
            var result = new Dictionary<string, TypeMetrics>(StringComparer.Ordinal);

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model     = compilation.GetSemanticModel(tree);
                var root      = tree.GetRoot();
                var typeNodes = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

                foreach (var typeNode in typeNodes)
                {
                    var symbol = model.GetDeclaredSymbol(typeNode) as ITypeSymbol;
                    if (symbol == null) continue;

                    var id  = MakeId(symbol);
                    var loc = CountLinesOfCode(typeNode.ToFullString());
                    var cc  = ComputeCyclomaticComplexity(typeNode);

                    result[id] = new TypeMetrics
                    {
                        LinesOfCode          = loc,
                        CyclomaticComplexity = cc
                    };
                }
            }

            return result;
        }

        // -----------------------------------------------------------------------
        // Impact analysis
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the set of node IDs that transitively depend on
        /// <paramref name="symbol"/> — i.e. everything that would break if
        /// <paramref name="symbol"/> were changed.
        /// </summary>
        public HashSet<string> GetImpact(
            ITypeSymbol symbol,
            Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> graph,
            Dictionary<ITypeSymbol, string> symbolToId)
        {
            // Build a reverse graph: target → set of sources that reference it.
            var reverseGraph = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(
                SymbolEqualityComparer.Default);

            foreach (var (src, targets) in graph)
            {
                foreach (var tgt in targets)
                {
                    if (!reverseGraph.TryGetValue(tgt, out var set))
                    {
                        set = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                        reverseGraph[tgt] = set;
                    }
                    set.Add(src);
                }
            }

            // BFS from symbol through reverse edges.
            var impacted = new HashSet<string>(StringComparer.Ordinal);
            var queue    = new Queue<ITypeSymbol>();
            var seen     = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            queue.Enqueue(symbol);
            seen.Add(symbol);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (!SymbolEqualityComparer.Default.Equals(current, symbol) &&
                    symbolToId.TryGetValue(current, out var id))
                {
                    impacted.Add(id);
                }

                if (reverseGraph.TryGetValue(current, out var dependents))
                {
                    foreach (var dep in dependents)
                    {
                        if (!seen.Contains(dep))
                        {
                            seen.Add(dep);
                            queue.Enqueue(dep);
                        }
                    }
                }
            }

            return impacted;
        }

        // -----------------------------------------------------------------------
        // Core graph builder (from handoff doc Section 9)
        // -----------------------------------------------------------------------

        private static Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> BuildTypeDependencies(
            Compilation compilation)
        {
            var graph = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(
                SymbolEqualityComparer.Default);

            foreach (var tree in compilation.SyntaxTrees)
            {
                var model     = compilation.GetSemanticModel(tree);
                var typeNodes = tree.GetRoot().DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();

                foreach (var typeNode in typeNodes)
                {
                    var typeSymbol = model.GetDeclaredSymbol(typeNode) as ITypeSymbol;
                    if (typeSymbol == null) continue;

                    if (!graph.ContainsKey(typeSymbol))
                        graph[typeSymbol] = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

                    var referencedTypes = typeNode.DescendantNodes()
                        .Select(n => model.GetTypeInfo(n).Type)
                        .Where(t => t != null &&
                                    !SymbolEqualityComparer.Default.Equals(t, typeSymbol))
                        .Distinct(SymbolEqualityComparer.Default);

                    foreach (var referenced in referencedTypes)
                        graph[typeSymbol].Add(referenced!);

                    // Also capture base class and interfaces as explicit edges.
                    if (typeSymbol.BaseType != null &&
                        !SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, typeSymbol))
                    {
                        graph[typeSymbol].Add(typeSymbol.BaseType);
                    }

                    foreach (var iface in typeSymbol.Interfaces)
                        graph[typeSymbol].Add(iface);
                }
            }

            return graph;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static string MakeId(ITypeSymbol symbol) =>
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private static EdgeType DetermineEdgeType(ITypeSymbol source, ITypeSymbol target)
        {
            if (source.BaseType != null &&
                SymbolEqualityComparer.Default.Equals(source.BaseType, target))
                return EdgeType.Inheritance;

            foreach (var iface in source.Interfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, target))
                    return EdgeType.Implementation;
            }

            return EdgeType.Reference;
        }

        private static int CountLinesOfCode(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return 0;

            int count = 0;
            foreach (var line in source.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith("//") &&
                    !trimmed.StartsWith("/*") && !trimmed.StartsWith("*"))
                    count++;
            }
            return count;
        }

        private static int ComputeCyclomaticComplexity(TypeDeclarationSyntax typeNode)
        {
            // McCabe CC = 1 + number of decision points (if, while, for, foreach,
            // case, catch, &&, ||, ??, ?:) per method, summed over all methods.
            int cc = 0;

            foreach (var method in typeNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                cc += 1; // base CC for each method
                cc += method.DescendantNodes().Count(n =>
                    n is IfStatementSyntax       ||
                    n is WhileStatementSyntax    ||
                    n is ForStatementSyntax      ||
                    n is ForEachStatementSyntax  ||
                    n is SwitchSectionSyntax     ||
                    n is CatchClauseSyntax       ||
                    n is ConditionalExpressionSyntax ||
                    (n is BinaryExpressionSyntax bin &&
                     (bin.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression) ||
                      bin.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression))));
            }

            return Math.Max(cc, 1);
        }
    }

    /// <summary>Holds computed metrics for a single type.</summary>
    public class TypeMetrics
    {
        public int LinesOfCode { get; set; }
        public int CyclomaticComplexity { get; set; }
    }
}
