using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DoaneDevTools.ToolWindows.DocGenerator.Services
{
    /// <summary>
    /// Uses Roslyn to add XML documentation comments to undocumented members.
    /// Generates contextually-appropriate summaries based on naming conventions
    /// (Get*, Create*, Update*, Delete*, etc.) and adds param/returns tags.
    /// </summary>
    public class XmlDocGeneratorService
    {
        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Opens the project at <paramref name="projectPath"/>, identifies all
        /// undocumented public and internal members, generates XML doc comments,
        /// and rewrites the affected source files in place.
        /// </summary>
        /// <returns>The number of XML doc comment blocks added.</returns>
        public async Task<int> GenerateForProjectAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(projectPath))
                throw new FileNotFoundException($"Project not found: {projectPath}");

            int added = 0;

            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);

            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (document.FilePath == null) continue;
                if (!document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

                if (syntaxRoot == null || semanticModel == null) continue;

                var rewriter  = new XmlDocRewriter(semanticModel);
                var newRoot   = rewriter.Visit(syntaxRoot);

                if (rewriter.AddedCount > 0)
                {
                    File.WriteAllText(document.FilePath, newRoot!.ToFullString(), Encoding.UTF8);
                    added += rewriter.AddedCount;
                }
            }

            return added;
        }

        // -----------------------------------------------------------------------
        // Summary generation logic (from handoff doc Section 6)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Generates a human-readable summary sentence for a method based on its name.
        /// Follows the naming-convention heuristics from the handoff document.
        /// </summary>
        public static string GenerateMethodSummary(string methodName, IReadOnlyList<string> paramNames)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                return "Performs the operation.";

            // --- Naming-convention heuristics ---
            if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[3..]);
                return $"Retrieves {Articulate(subject)}.";
            }

            if (methodName.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[6..]);
                return $"Creates a new {subject.ToLower()}.";
            }

            if (methodName.StartsWith("Insert", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[6..]);
                return $"Creates a new {subject.ToLower()} in the data store.";
            }

            if (methodName.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[6..]);
                return $"Updates an existing {subject.ToLower()}.";
            }

            if (methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[6..]);
                return $"Deletes the specified {subject.ToLower()}.";
            }

            if (methodName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[6..]);
                return $"Deletes the specified {subject.ToLower()}.";
            }

            if (methodName.StartsWith("Is", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Has", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Can", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName);
                return $"Determines whether {subject.ToLower()}.";
            }

            if (methodName.StartsWith("On", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[2..]);
                return $"Handles the {subject.ToLower()} event.";
            }

            if (methodName.StartsWith("Build", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[5..]);
                return $"Builds {Articulate(subject.ToLower())}.";
            }

            if (methodName.StartsWith("Load", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[4..]);
                return $"Loads {Articulate(subject.ToLower())}.";
            }

            if (methodName.StartsWith("Save", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[4..]);
                return $"Saves {Articulate(subject.ToLower())}.";
            }

            if (methodName.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(
                    methodName.StartsWith("Find", StringComparison.OrdinalIgnoreCase)
                        ? methodName[4..] : methodName[6..]);
                return $"Searches for {Articulate(subject.ToLower())}.";
            }

            if (methodName.StartsWith("Validate", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[8..]);
                return $"Validates the specified {subject.ToLower()}.";
            }

            if (methodName.StartsWith("Convert", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Transform", StringComparison.OrdinalIgnoreCase))
            {
                return $"Converts the input to the required format.";
            }

            if (methodName.StartsWith("Send", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[4..]);
                return $"Sends {Articulate(subject.ToLower())}.";
            }

            if (methodName.StartsWith("Publish", StringComparison.OrdinalIgnoreCase))
            {
                var subject = SplitCamelCase(methodName[7..]);
                return $"Publishes {Articulate(subject.ToLower())}.";
            }

            // Fallback: split the method name on camel-case boundaries.
            return $"{SplitCamelCase(methodName)}.";
        }

        /// <summary>
        /// Generates a <c>&lt;param&gt;</c> description for a parameter based on its name.
        /// </summary>
        public static string GenerateParamDescription(string paramName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(paramName)) return "The parameter value.";

            if (paramName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                paramName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                return $"The unique identifier of the {SplitCamelCase(typeName).ToLower()}.";

            if (paramName.Equals("cancellationToken", StringComparison.OrdinalIgnoreCase))
                return "Token used to cancel the operation.";

            if (paramName.Equals("connection", StringComparison.OrdinalIgnoreCase) ||
                paramName.EndsWith("Connection", StringComparison.OrdinalIgnoreCase))
                return "The database connection to use.";

            if (paramName.Equals("path", StringComparison.OrdinalIgnoreCase) ||
                paramName.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                return "The file system path.";

            if (paramName.Equals("options", StringComparison.OrdinalIgnoreCase))
                return "Configuration options for the operation.";

            return $"The {SplitCamelCase(paramName).ToLower()}.";
        }

        /// <summary>
        /// Generates a <c>&lt;returns&gt;</c> description based on the return type name.
        /// </summary>
        public static string GenerateReturnsDescription(string returnTypeName)
        {
            if (string.IsNullOrWhiteSpace(returnTypeName) ||
                returnTypeName.Equals("void", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (returnTypeName.StartsWith("Task<", StringComparison.OrdinalIgnoreCase))
            {
                var inner = returnTypeName[5..^1];
                return $"A task that represents the asynchronous operation. The task result contains {Articulate(SplitCamelCase(inner).ToLower())}.";
            }

            if (returnTypeName.Equals("Task", StringComparison.OrdinalIgnoreCase))
                return "A task that represents the asynchronous operation.";

            if (returnTypeName.Equals("bool", StringComparison.OrdinalIgnoreCase) ||
                returnTypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                return "<see langword=\"true\"/> if the operation succeeded; otherwise <see langword=\"false\"/>.";

            if (returnTypeName.StartsWith("IEnumerable", StringComparison.OrdinalIgnoreCase) ||
                returnTypeName.StartsWith("List<", StringComparison.OrdinalIgnoreCase) ||
                returnTypeName.StartsWith("IReadOnlyList", StringComparison.OrdinalIgnoreCase))
                return "A collection of matching items.";

            return $"The resulting {SplitCamelCase(returnTypeName).ToLower()}.";
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Splits a PascalCase or camelCase identifier into a space-separated phrase.
        /// e.g. "GetUserById" → "Get User By Id".
        /// </summary>
        private static string SplitCamelCase(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return identifier;
            return Regex.Replace(identifier, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        }

        /// <summary>
        /// Prepends "a" or "an" to a noun phrase based on first-letter vowel check.
        /// </summary>
        private static string Articulate(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return noun;
            return "aeiou".IndexOf(char.ToLower(noun[0])) >= 0 ? $"an {noun}" : $"a {noun}";
        }
    }

    // -----------------------------------------------------------------------
    // Roslyn SyntaxRewriter
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks the syntax tree and inserts XML doc comment trivia before each
    /// undocumented public/internal/protected member declaration.
    /// </summary>
    internal sealed class XmlDocRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        public int AddedCount { get; private set; }

        public XmlDocRewriter(SemanticModel semanticModel) : base(visitIntoStructuredTrivia: false)
        {
            _semanticModel = semanticModel;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!IsAccessible(node.Modifiers) || HasXmlDoc(node))
                return base.VisitMethodDeclaration(node);

            var symbol = _semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
            var paramNames = node.ParameterList.Parameters
                .Select(p => p.Identifier.Text)
                .ToList();

            var summary = XmlDocGeneratorService.GenerateMethodSummary(
                node.Identifier.Text, paramNames);

            var docComment = BuildDocComment(node.GetLeadingTrivia(), summary,
                node.ParameterList.Parameters,
                node.ReturnType.ToString());

            AddedCount++;
            return node.WithLeadingTrivia(docComment);
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (!IsAccessible(node.Modifiers) || HasXmlDoc(node))
                return base.VisitPropertyDeclaration(node);

            var summary = $"Gets or sets the {XmlDocGeneratorService.GenerateMethodSummary(node.Identifier.Text, Array.Empty<string>()).TrimEnd('.')}";
            var docComment = BuildDocComment(node.GetLeadingTrivia(), summary, default, null);

            AddedCount++;
            return node.WithLeadingTrivia(docComment);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!IsAccessible(node.Modifiers) || HasXmlDoc(node))
                return base.VisitClassDeclaration(node);

            var summary = $"Provides functionality related to {SplitCamelCase(node.Identifier.Text).ToLower()}.";
            var docComment = BuildDocComment(node.GetLeadingTrivia(), summary, default, null);

            AddedCount++;
            return (node.WithLeadingTrivia(docComment)
                       .WithMembers(VisitList(node.Members)));
        }

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!IsAccessible(node.Modifiers) || HasXmlDoc(node))
                return base.VisitInterfaceDeclaration(node);

            var name = node.Identifier.Text.TrimStart('I');
            var summary = $"Defines the contract for {SplitCamelCase(name).ToLower()} operations.";
            var docComment = BuildDocComment(node.GetLeadingTrivia(), summary, default, null);

            AddedCount++;
            return (node.WithLeadingTrivia(docComment)
                       .WithMembers(VisitList(node.Members)));
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static bool IsAccessible(SyntaxTokenList modifiers)
        {
            return modifiers.Any(m =>
                m.IsKind(SyntaxKind.PublicKeyword)    ||
                m.IsKind(SyntaxKind.ProtectedKeyword) ||
                m.IsKind(SyntaxKind.InternalKeyword));
        }

        private static bool HasXmlDoc(SyntaxNode node)
        {
            return node.GetLeadingTrivia().Any(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }

        private static SyntaxTriviaList BuildDocComment(
            SyntaxTriviaList existingLeading,
            string summary,
            SeparatedSyntaxList<ParameterSyntax> parameters,
            string? returnTypeName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXml(summary)}");
            sb.AppendLine("/// </summary>");

            foreach (var param in parameters)
            {
                var pName = param.Identifier.Text;
                var pType = param.Type?.ToString() ?? string.Empty;
                var desc  = XmlDocGeneratorService.GenerateParamDescription(pName, pType);
                sb.AppendLine($"/// <param name=\"{EscapeXml(pName)}\">{EscapeXml(desc)}</param>");
            }

            if (!string.IsNullOrEmpty(returnTypeName))
            {
                var returnsDesc = XmlDocGeneratorService.GenerateReturnsDescription(returnTypeName);
                if (!string.IsNullOrEmpty(returnsDesc))
                    sb.AppendLine($"/// <returns>{EscapeXml(returnsDesc)}</returns>");
            }

            // Determine indentation from existing trivia.
            string indent = string.Empty;
            foreach (var t in existingLeading)
            {
                if (t.IsKind(SyntaxKind.WhitespaceTrivia))
                    indent = t.ToString();
            }

            var docLines = sb.ToString().Split('\n');
            var trivia   = new List<SyntaxTrivia>();

            // Preserve any existing whitespace/attribute trivia.
            trivia.AddRange(existingLeading.Where(t =>
                t.IsKind(SyntaxKind.WhitespaceTrivia) ||
                t.IsKind(SyntaxKind.EndOfLineTrivia)));

            foreach (var line in docLines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                var text = indent + line.TrimStart();
                trivia.Add(SyntaxFactory.Comment(text));
                trivia.Add(SyntaxFactory.EndOfLine(Environment.NewLine));
                if (!string.IsNullOrWhiteSpace(indent))
                    trivia.Add(SyntaxFactory.Whitespace(indent));
            }

            return SyntaxFactory.TriviaList(trivia);
        }

        private static string SplitCamelCase(string s) =>
            Regex.Replace(s, @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

        private static string EscapeXml(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
