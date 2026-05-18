using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA012_MissingXmlDocAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA012,
            title: "Missing XML Documentation",
            messageFormat: "Public {0} '{1}' is missing an XML documentation comment.",
            category: "Documentation",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Public API members should have XML documentation comments for IntelliSense and generated docs.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                ctx => AnalyzeNode(ctx, "method"),
                SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(
                ctx => AnalyzeNode(ctx, "class"),
                SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(
                ctx => AnalyzeNode(ctx, "property"),
                SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context, string memberKind)
        {
            SyntaxTokenList modifiers;
            SyntaxToken identifier;
            SyntaxNode node = context.Node;

            switch (node)
            {
                case MethodDeclarationSyntax method:
                    modifiers = method.Modifiers;
                    identifier = method.Identifier;
                    break;
                case ClassDeclarationSyntax classDecl:
                    modifiers = classDecl.Modifiers;
                    identifier = classDecl.Identifier;
                    break;
                case PropertyDeclarationSyntax property:
                    modifiers = property.Modifiers;
                    identifier = property.Identifier;
                    break;
                default:
                    return;
            }

            // Only check public members
            if (!modifiers.Any(SyntaxKind.PublicKeyword))
                return;

            // Check for XML doc comment in leading trivia
            if (HasXmlDocComment(node))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                identifier.GetLocation(),
                memberKind,
                identifier.Text));
        }

        private static bool HasXmlDocComment(SyntaxNode node)
        {
            foreach (var trivia in node.GetLeadingTrivia())
            {
                if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    // Verify it contains a <summary> element
                    var structure = trivia.GetStructure();
                    if (structure is DocumentationCommentTriviaSyntax docComment)
                    {
                        bool hasSummary = docComment.ChildNodes()
                            .OfType<XmlElementSyntax>()
                            .Any(el => el.StartTag?.Name?.LocalName.Text == "summary");
                        if (hasSummary)
                            return true;
                    }
                    // Even without summary tag, presence of doc comment trivia counts
                    return true;
                }
            }
            return false;
        }
    }
}
