using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    /// <summary>
    /// DS009: Pre-NRT null safety — finds common likely-null dereferences in projects
    /// not yet on nullable reference types. Complements DS006.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NullSafetyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DS009";

        private static readonly DiagnosticDescriptor FirstOrDefaultRule = new(
            DiagnosticId,
            title: "Possible null dereference after FirstOrDefault()",
            messageFormat: "Result of FirstOrDefault() is accessed without a null check. Use ?. or add a null guard.",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor SingleOrDefaultRule = new(
            "DS009b",
            title: "Possible null dereference after SingleOrDefault()",
            messageFormat: "Result of SingleOrDefault() is accessed without a null check.",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(FirstOrDefaultRule, SingleOrDefaultRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;

            // Look for pattern: someCollection.FirstOrDefault().Property
            // The expression being accessed must itself be an invocation of FirstOrDefault/SingleOrDefault
            if (memberAccess.Expression is not InvocationExpressionSyntax invocation) return;

            if (invocation.Expression is not MemberAccessExpressionSyntax innerMember) return;

            var methodName = innerMember.Name.Identifier.Text;
            if (methodName != "FirstOrDefault" && methodName != "SingleOrDefault") return;

            // Verify via semantic model that the invocation returns a nullable type
            var typeInfo = context.SemanticModel.GetTypeInfo(invocation);
            if (typeInfo.Type is INamedTypeSymbol named && named.IsValueType)
                return; // value types are fine (Nullable<T> would still have .Value but that's separate)

            // Check parent — if parent is already a conditional access (?.), we're safe
            if (memberAccess.Parent is ConditionalAccessExpressionSyntax) return;

            // Check if the result is in a null check (if (x != null), x is null, x == null patterns)
            if (IsInsideNullCheck(memberAccess)) return;

            var rule = methodName == "FirstOrDefault" ? FirstOrDefaultRule : SingleOrDefaultRule;
            context.ReportDiagnostic(Diagnostic.Create(rule, memberAccess.GetLocation()));
        }

        private static bool IsInsideNullCheck(SyntaxNode node)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is IfStatementSyntax ifStmt)
                {
                    var condition = ifStmt.Condition.ToString();
                    if (condition.Contains("null") || condition.Contains("is not") || condition.Contains("!="))
                        return true;
                }
                if (parent is BinaryExpressionSyntax binary &&
                    (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)) &&
                    binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
                    return true;

                if (parent is StatementSyntax) break; // Don't walk past the containing statement
                parent = parent.Parent;
            }
            return false;
        }
    }
}
