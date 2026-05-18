namespace DoaneDevTools.ToolWindows.DependencyMap.Models
{
    /// <summary>Granularity of a node in the dependency graph.</summary>
    public enum NodeType
    {
        Assembly,
        Namespace,
        Class,
        Method
    }

    /// <summary>
    /// Represents a single node in the dependency graph canvas.
    /// Carries both structural metadata (type, assembly) and computed
    /// code-quality metrics (LOC, cyclomatic complexity, coupling).
    /// </summary>
    public class GraphNode
    {
        /// <summary>Stable unique identifier used to correlate edges.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Short display name shown on the canvas.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Fully-qualified name (namespace + type).</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Granularity of this node.</summary>
        public NodeType Type { get; set; }

        // -----------------------------------------------------------------------
        // Canvas layout (set by ForceLayoutService)
        // -----------------------------------------------------------------------

        public double X { get; set; }
        public double Y { get; set; }
        public double Width  { get; set; } = 120;
        public double Height { get; set; } = 40;

        // -----------------------------------------------------------------------
        // Code-quality metrics (set by RoslynGraphBuilder.ComputeMetrics)
        // -----------------------------------------------------------------------

        /// <summary>Lines of code (non-blank, non-comment) in the type.</summary>
        public int LinesOfCode { get; set; }

        /// <summary>McCabe cyclomatic complexity summed across all methods.</summary>
        public int CyclomaticComplexity { get; set; }

        /// <summary>Afferent coupling — number of types that depend on this type.</summary>
        public int AfferentCoupling { get; set; }

        /// <summary>Efferent coupling — number of types this type depends on.</summary>
        public int EfferentCoupling { get; set; }

        // -----------------------------------------------------------------------
        // Graph analysis flags
        // -----------------------------------------------------------------------

        /// <summary>True if this node participates in a circular dependency cycle.</summary>
        public bool IsInCycle { get; set; }

        /// <summary>True when the "What breaks if I change X?" impact analysis highlights this node.</summary>
        public bool IsImpacted { get; set; }

        /// <summary>Name of the assembly that contains this type/namespace.</summary>
        public string AssemblyName { get; set; } = string.Empty;
    }
}
