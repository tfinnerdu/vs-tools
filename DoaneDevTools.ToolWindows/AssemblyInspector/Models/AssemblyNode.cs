using System.Collections.Generic;

namespace DoaneDevTools.ToolWindows.AssemblyInspector.Models
{
    /// <summary>
    /// Represents a loaded assembly in the Members tree.
    /// Children are namespace groups containing <see cref="TypeNode"/> instances.
    /// </summary>
    public class AssemblyNode
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public List<NamespaceNode> Children { get; set; } = new List<NamespaceNode>();

        public override string ToString() => Name;
    }

    /// <summary>
    /// Groups types that share a common namespace beneath an <see cref="AssemblyNode"/>.
    /// </summary>
    public class NamespaceNode
    {
        public string Namespace { get; set; } = string.Empty;
        public List<TypeNode> Types { get; set; } = new List<TypeNode>();

        public override string ToString() => Namespace;
    }

    /// <summary>
    /// Represents a single type (class, interface, struct, enum, delegate) inside an assembly.
    /// </summary>
    public class TypeNode
    {
        public string FullName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string AssemblyPath { get; set; } = string.Empty;
        public List<MethodNode> Methods { get; set; } = new List<MethodNode>();

        public override string ToString() => ShortName;
    }

    /// <summary>
    /// Represents a method or property accessor within a <see cref="TypeNode"/>.
    /// </summary>
    public class MethodNode
    {
        public string Signature { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public int MetadataToken { get; set; }
        public string AssemblyPath { get; set; } = string.Empty;
        public string DeclaringTypeFullName { get; set; } = string.Empty;

        public override string ToString() => Signature;
    }

    /// <summary>
    /// Flat type metadata returned by the decompiler service enumeration.
    /// </summary>
    public class TypeInfo
    {
        public string FullName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public List<string> MethodSignatures { get; set; } = new List<string>();
    }
}
