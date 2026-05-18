using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS006_EnvVarNullCheckAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS006,
            title: "Environment Variable Not Null-Checked",
            messageFormat: "The result of Environment.GetEnvironmentVariable is used without a null check. The variable may not be set.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Environment.GetEnvironmentVariable returns null when the variable is not set. Always check for null before use.");

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

            if (!IsEnvironmentGetVariable(invocation))
                return;

            // Verify via semantic model
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                if (method.Name != "GetEnvironmentVariable" ||
                    method.ContainingType?.ToDisplayString() != "System.Environment")
                    return;
            }
            else
            {
                // Fallback: accept based on syntax check
            }

            // Check the parent to see if there's a null guard
            var parent = invocation.Parent;
            if (parent == null)
                return;

            // Patterns that indicate a null check:
            // 1. ?? operator: invocation ?? fallback
            if (parent is BinaryExpressionSyntax binary &&
                binary.IsKind(SyntaxKind.CoalesceExpression))
                return;

            // 2. null-conditional: not applicable here (result is used directly)
            // 3. is null / != null check — the invocation is inside an if condition
            if (IsInsideNullCheck(invocation))
                return;

            // 4. Conditional access on result: e.g. var x = env?.Length (null-conditional on result)
            if (parent is ConditionalAccessExpressionSyntax)
                return;

            // 5. Variable declared and immediately checked
            if (IsAssignedThenNullChecked(invocation, context))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
        }

        private static bool IsEnvironmentGetVariable(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return false;

            if (memberAccess.Name.Identifier.Text != "GetEnvironmentVariable")
                return false;

            // Check receiver is Environment or System.Environment
            var receiver = memberAccess.Expression.ToString();
            return receiver == "Environment" || receiver == "System.Environment";
        }

        private static bool IsInsideNullCheck(SyntaxNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                // if (x == null) or if (x != null) or if (x is null)
                if (parent is IfStatementSyntax)
                    return true;

                // Ternary: x != null ? x : fallback
                if (parent is ConditionalExpressionSyntax)
                    return true;

                // Binary equality: x == null, x != null
                if (parent is BinaryExpressionSyntax binaryExpr)
                {
                    if (binaryExpr.IsKind(SyntaxKind.EqualsExpression) ||
                        binaryExpr.IsKind(SyntaxKind.NotEqualsExpression))
                    {
                        // Check if one side is a null literal
                        bool hasNull = binaryExpr.Left is LiteralExpressionSyntax leftLit &&
                                       leftLit.IsKind(SyntaxKind.NullLiteralExpression) ||
                                       binaryExpr.Right is LiteralExpressionSyntax rightLit &&
                                       rightLit.IsKind(SyntaxKind.NullLiteralExpression);
                        if (hasNull)
                            return true;
                    }
                }

                // is null pattern
                if (parent is IsPatternExpressionSyntax)
                    return true;

                // Stop at statement boundary
                if (parent is StatementSyntax)
                    break;

                parent = parent.Parent;
            }
            return false;
        }

        private static bool IsAssignedThenNullChecked(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
        {
            // If this invocation is assigned to a local variable, look for a subsequent null check of that variable
            var localDecl = invocation.Ancestors().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
            if (localDecl == null)
                return false;

            // We conservatively return false here — the check at call site is enough
            return false;
        }
    }
}
