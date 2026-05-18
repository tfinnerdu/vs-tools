using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS005_SqlCancellationTokenAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS005,
            title: "Async SqlCommand Missing CancellationToken",
            messageFormat: "Async SqlCommand method '{0}' is called but the containing method has no CancellationToken parameter. Pass a CancellationToken to support cancellation.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Async database operations should accept and forward CancellationToken to allow proper request cancellation.");

        private static readonly ImmutableHashSet<string> AsyncSqlMethods = ImmutableHashSet.Create(
            "ExecuteReaderAsync",
            "ExecuteScalarAsync",
            "ExecuteNonQueryAsync");

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

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return;

            var methodName = memberAccess.Name.Identifier.Text;
            if (!AsyncSqlMethods.Contains(methodName))
                return;

            // Verify via semantic model that the method is on SqlCommand
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
            if (!(symbolInfo.Symbol is IMethodSymbol method))
                return;

            var containingTypeName = method.ContainingType?.Name;
            if (containingTypeName != "SqlCommand")
                return;

            // Check if the containing method has a CancellationToken parameter
            var containingMethod = invocation.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (containingMethod == null)
                return;

            bool hasCancellationToken = containingMethod.ParameterList.Parameters
                .Any(p => IsCancellationTokenType(p.Type));

            if (!hasCancellationToken)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    methodName));
            }
        }

        private static bool IsCancellationTokenType(TypeSyntax? type)
        {
            if (type == null)
                return false;

            if (type is IdentifierNameSyntax id)
                return id.Identifier.Text == "CancellationToken";

            if (type is QualifiedNameSyntax qualified)
                return qualified.Right.Identifier.Text == "CancellationToken";

            return false;
        }
    }
}
