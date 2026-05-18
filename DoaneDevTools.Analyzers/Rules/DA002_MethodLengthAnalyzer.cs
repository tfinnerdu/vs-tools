using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA002_MethodLengthAnalyzer : DiagnosticAnalyzer
    {
        private const int MaxLogicalLines = 50;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA002,
            title: "Method Too Long",
            messageFormat: "Method '{0}' has {1} logical lines (threshold: 50). Consider splitting into smaller methods.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Long methods are harder to read and maintain.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;

            if (method.Body == null)
                return;

            int logicalLines = CountLogicalLines(method.Body);

            if (logicalLines > MaxLogicalLines)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text,
                    logicalLines);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static int CountLogicalLines(BlockSyntax body)
        {
            var text = body.SyntaxTree.GetText();
            int count = 0;

            foreach (var statement in body.DescendantNodes())
            {
                // Only count leaf-level statements (not blocks themselves)
                if (statement is StatementSyntax stmt && !(stmt is BlockSyntax))
                {
                    var lineSpan = text.Lines.GetLineFromPosition(stmt.SpanStart);
                    // Check it's not a blank line
                    var lineText = lineSpan.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(lineText))
                    {
                        count++;
                    }
                }
            }

            // Fallback: if no statements found, count non-blank, non-comment lines
            if (count == 0)
            {
                var bodyText = body.ToString();
                var lines = bodyText.Split('\n');
                count = lines.Count(l =>
                {
                    var trimmed = l.Trim();
                    return !string.IsNullOrWhiteSpace(trimmed)
                        && !trimmed.StartsWith("//")
                        && !trimmed.StartsWith("/*")
                        && !trimmed.StartsWith("*")
                        && trimmed != "{"
                        && trimmed != "}";
                });
            }

            return count;
        }
    }
}
