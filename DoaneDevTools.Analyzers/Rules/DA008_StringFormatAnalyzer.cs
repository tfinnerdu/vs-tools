using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA008_StringFormatAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA008,
            title: "Use String Interpolation",
            messageFormat: "Use string interpolation instead of string.Format for better readability.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "String interpolation is more readable than string.Format.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Check the shape: string.Format(...)
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return;

            if (memberAccess.Name.Identifier.Text != "Format")
                return;

            // Must have at least 2 arguments (format string + at least one arg)
            var args = invocation.ArgumentList.Arguments;
            if (args.Count < 2)
                return;

            // First argument must be a string literal
            if (!(args[0].Expression is LiteralExpressionSyntax firstArg) ||
                !firstArg.IsKind(SyntaxKind.StringLiteralExpression))
                return;

            // Use semantic model to verify this is System.String.Format
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                if (method.ContainingType?.SpecialType != SpecialType.System_String)
                    return;
                if (method.Name != "Format")
                    return;
            }
            else
            {
                // If we can't resolve, check by name heuristic
                if (!(memberAccess.Expression is IdentifierNameSyntax id && id.Identifier.Text == "string") &&
                    !(memberAccess.Expression is PredefinedTypeSyntax))
                    return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }
    }
}
