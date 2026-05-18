using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA006_NestedTernaryAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA006,
            title: "Nested Ternary Expression",
            messageFormat: "Nested ternary expression detected. Consider using if-else statements for clarity.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Nested ternary expressions reduce readability.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
        }

        private static void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
        {
            var conditional = (ConditionalExpressionSyntax)context.Node;

            // Check if any ancestor is also a ConditionalExpression (depth > 1)
            bool isNested = conditional.Ancestors()
                .OfType<ConditionalExpressionSyntax>()
                .Any();

            if (isNested)
            {
                // Only report on the innermost (deepest) conditional to avoid duplicate reports
                // Report if the direct parent chain includes another ConditionalExpression
                bool parentIsConditional = conditional.Parent is ConditionalExpressionSyntax
                    || (conditional.Parent != null && conditional.Parent.Parent is ConditionalExpressionSyntax);

                if (parentIsConditional)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, conditional.GetLocation()));
                }
            }
        }
    }
}
