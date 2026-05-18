using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using DoaneDevTools.ToolWindows.AssemblyInspector.Models;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace DoaneDevTools.ToolWindows.AssemblyInspector.Services
{
    /// <summary>
    /// Wraps ICSharpCode.Decompiler to provide C# decompilation, IL disassembly,
    /// and type enumeration for arbitrary managed assemblies.
    /// </summary>
    public class DecompilerService
    {
        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Decompiles the entire type identified by <paramref name="fullTypeName"/>
        /// to C# source.
        /// </summary>
        /// <param name="assemblyPath">Absolute path to the assembly on disk.</param>
        /// <param name="fullTypeName">
        /// Fully-qualified type name in the form accepted by
        /// <see cref="ICSharpCode.Decompiler.TypeSystem.FullTypeName"/>,
        /// e.g. <c>System.Net.Http.HttpClient</c>.
        /// </param>
        public string DecompileType(string assemblyPath, string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(fullTypeName))
                throw new ArgumentNullException(nameof(fullTypeName));

            try
            {
                var settings = BuildSettings();
                var decompiler = new CSharpDecompiler(assemblyPath, settings);
                var typeName = new FullTypeName(fullTypeName);
                return decompiler.DecompileTypeAsString(typeName);
            }
            catch (Exception ex)
            {
                return $"// Decompilation failed for {fullTypeName}\r\n// {ex.Message}";
            }
        }

        /// <summary>
        /// Decompiles a single method within the specified type.
        /// Falls back to full-type decompilation if the method cannot be isolated.
        /// </summary>
        /// <param name="assemblyPath">Absolute path to the assembly on disk.</param>
        /// <param name="fullTypeName">Fully-qualified type name.</param>
        /// <param name="methodName">Simple method name (without signature).</param>
        public string DecompileMethod(string assemblyPath, string fullTypeName, string methodName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(fullTypeName))
                throw new ArgumentNullException(nameof(fullTypeName));
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentNullException(nameof(methodName));

            try
            {
                var settings = BuildSettings();
                var decompiler = new CSharpDecompiler(assemblyPath, settings);

                // Resolve the type in the decompiler's type system.
                var typeSystem = decompiler.TypeSystem;
                var compilation = typeSystem.MainModule.Compilation;
                var fullTypeNameObj = new FullTypeName(fullTypeName);

                var typeDefinition = typeSystem.MainModule.GetTypeDefinition(fullTypeNameObj);
                if (typeDefinition == null)
                    return DecompileType(assemblyPath, fullTypeName);

                // Find matching methods by name.
                var methods = typeDefinition.GetMethods(m =>
                    string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!methods.Any())
                {
                    // Try properties with get_/set_ prefix.
                    methods = typeDefinition.GetMethods(m =>
                        string.Equals(m.Name, $"get_{methodName}", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.Name, $"set_{methodName}", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!methods.Any())
                    return DecompileType(assemblyPath, fullTypeName);

                // Decompile first match; if multiple overloads exist, decompile all.
                var results = new System.Text.StringBuilder();
                foreach (var method in methods)
                {
                    try
                    {
                        var entityHandle = (EntityHandle)method.MetadataToken;
                        results.AppendLine(decompiler.DecompileAsString(entityHandle));
                        results.AppendLine();
                    }
                    catch
                    {
                        // Skip methods that cannot be individually decompiled.
                    }
                }

                return results.Length > 0
                    ? results.ToString()
                    : DecompileType(assemblyPath, fullTypeName);
            }
            catch (Exception ex)
            {
                return $"// Decompilation failed for {fullTypeName}.{methodName}\r\n// {ex.Message}";
            }
        }

        /// <summary>
        /// Returns the CIL (Common Intermediate Language) disassembly for the
        /// specified type, rendered as a human-readable text listing.
        /// </summary>
        /// <param name="assemblyPath">Absolute path to the assembly on disk.</param>
        /// <param name="fullTypeName">Fully-qualified type name.</param>
        public string GetIL(string assemblyPath, string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(fullTypeName))
                throw new ArgumentNullException(nameof(fullTypeName));

            try
            {
                using var peFile = new PEFile(assemblyPath);
                var reader = peFile.Metadata;

                var output = new PlainTextOutput();
                var disassembler = new ReflectionDisassembler(output, default);

                // Find the type definition handle.
                foreach (var typeDefHandle in reader.TypeDefinitions)
                {
                    var typeDef = reader.GetTypeDefinition(typeDefHandle);
                    var ns = reader.GetString(typeDef.Namespace);
                    var name = reader.GetString(typeDef.Name);
                    var candidate = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                    if (string.Equals(candidate, fullTypeName, StringComparison.OrdinalIgnoreCase))
                    {
                        disassembler.DisassembleType(peFile, typeDefHandle);
                        return output.ToString();
                    }
                }

                return $"// Type '{fullTypeName}' not found in {Path.GetFileName(assemblyPath)}";
            }
            catch (Exception ex)
            {
                return $"// IL disassembly failed for {fullTypeName}\r\n// {ex.Message}";
            }
        }

        /// <summary>
        /// Enumerates all public (and internal) types in the assembly, returning
        /// <see cref="TypeInfo"/> records with method signatures pre-populated.
        /// </summary>
        /// <param name="assemblyPath">Absolute path to the assembly on disk.</param>
        public IEnumerable<TypeInfo> GetTypes(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            var results = new List<TypeInfo>();

            try
            {
                var settings = BuildSettings();
                var decompiler = new CSharpDecompiler(assemblyPath, settings);
                var typeSystem = decompiler.TypeSystem;
                var module = typeSystem.MainModule;

                foreach (var typeDefinition in module.TypeDefinitions)
                {
                    // Skip compiler-generated types, anonymous types, and nested private types.
                    if (typeDefinition.Name.StartsWith("<", StringComparison.Ordinal))
                        continue;
                    if (typeDefinition.IsAnonymousType())
                        continue;

                    var info = new TypeInfo
                    {
                        FullName = typeDefinition.FullName,
                        Namespace = typeDefinition.Namespace,
                        ShortName = typeDefinition.Name
                    };

                    foreach (var method in typeDefinition.GetMethods())
                    {
                        if (method.Name.StartsWith("<", StringComparison.Ordinal))
                            continue;

                        var sig = BuildMethodSignature(method);
                        info.MethodSignatures.Add(sig);
                    }

                    results.Add(info);
                }
            }
            catch (Exception ex)
            {
                results.Add(new TypeInfo
                {
                    FullName = $"Error: {ex.Message}",
                    Namespace = string.Empty,
                    ShortName = "Error"
                });
            }

            return results;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static DecompilerSettings BuildSettings() =>
            new DecompilerSettings(LanguageVersion.CSharp10_0)
            {
                ThrowOnAssemblyResolveErrors = false,
                RemoveDeadCode = false,
                ShowXmlDocumentation = true
            };

        private static string BuildMethodSignature(IMethod method)
        {
            var paramList = string.Join(", ", method.Parameters.Select(p =>
                $"{p.Type.Name} {p.Name}"));

            var returnType = method.ReturnType.Name;
            return $"{method.Name}({paramList}) → {returnType}";
        }
    }
}
