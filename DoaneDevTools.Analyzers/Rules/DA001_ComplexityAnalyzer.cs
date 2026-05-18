using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA001_ComplexityAnalyzer : DiagnosticAnalyzer
    {
        private const int ComplexityThreshold = 15;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA001,
            title: "High Cyclomatic Complexity",
            messageFormat: "Method '{0}' has cyclomatic complexity of {1} (threshold: 15). Consider splitting into smaller methods.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Methods with high cyclomatic complexity are harder to test and maintain.");

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
            int complexity = CalculateCyclomaticComplexity(method);

            if (complexity > ComplexityThreshold)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text,
                    complexity);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            var branchingTokens = new[]
            {
                SyntaxKind.IfKeyword, SyntaxKind.WhileKeyword, SyntaxKind.ForKeyword,
                SyntaxKind.ForEachKeyword, SyntaxKind.CaseKeyword, SyntaxKind.CatchKeyword,
                SyntaxKind.ConditionalExpression,
                SyntaxKind.AmpersandAmpersandToken, SyntaxKind.BarBarToken,
                SyntaxKind.QuestionQuestionToken,
                SyntaxKind.SwitchExpressionArm,
            };
            return 1 + method.DescendantTokens().Count(t => branchingTokens.Contains(t.Kind()));
        }
    }
}
