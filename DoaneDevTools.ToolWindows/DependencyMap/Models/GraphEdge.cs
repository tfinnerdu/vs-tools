namespace DoaneDevTools.ToolWindows.DependencyMap.Models
{
    /// <summary>Semantic relationship represented by an edge in the dependency graph.</summary>
    public enum EdgeType
    {
        /// <summary>One type references another (field, parameter, return type, etc.).</summary>
        Reference,

        /// <summary>A class inherits from another class.</summary>
        Inheritance,

        /// <summary>A class or struct implements an interface.</summary>
        Implementation
    }

    /// <summary>
    /// Directed edge in the dependency graph connecting a source node to a target node.
    /// </summary>
    public class GraphEdge
    {
        /// <summary>
        /// <see cref="GraphNode.Id"/> of the node that declares the dependency.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// <see cref="GraphNode.Id"/> of the node being depended upon.
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>The semantic relationship this edge represents.</summary>
        public EdgeType Type { get; set; }

        /// <summary>
        /// Number of method-call sites from source to target (populated when
        /// scope is Method-level). Zero for non-call-site relationships.
        /// </summary>
        public int CallCount { get; set; }
    }
}
