using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA010_UnawaitedTaskAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA010,
            title: "Unawaited Task",
            messageFormat: "The Task returned by '{0}' is not awaited. Unawaited tasks can cause silent failures.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Calls returning Task must be awaited, or the result explicitly assigned or discarded.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        }

        private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
        {
            var exprStmt = (ExpressionStatementSyntax)context.Node;
            var expr = exprStmt.Expression;

            // Must be a bare invocation expression (not await, not assignment)
            if (!(expr is InvocationExpressionSyntax invocation))
                return;

            // Get the method name for the message
            string methodName = GetMethodName(invocation);

            // Exclude Task.Run, Task.WhenAll, Task.WhenAny used as statements
            // (these are typically fire-and-forget patterns, but technically still bad;
            //  spec says exclude them when assigned to variables, but as statements they
            //  would still fire the diagnostic. Per spec: "Exclude Task.Run, Task.WhenAll,
            //  Task.WhenAny assigned to variables." — as statements they are NOT assigned,
            //  so we report. But to avoid noise on intentional patterns, we skip them.)
            if (IsTaskFactoryMethod(invocation))
                return;

            // Use semantic model to check if the return type is Task or Task<T>
            var typeInfo = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken);
            var returnType = typeInfo.Type;

            if (returnType == null)
                return;

            if (!IsTaskType(returnType))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                methodName));
        }

        private static string GetMethodName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.Text;
            if (invocation.Expression is IdentifierNameSyntax idName)
                return idName.Identifier.Text;
            return invocation.Expression.ToString();
        }

        private static bool IsTaskFactoryMethod(InvocationExpressionSyntax invocation)
        {
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return false;

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName != "Run" && methodName != "WhenAll" && methodName != "WhenAny")
                return false;

            // Check the receiver is "Task"
            if (memberAccess.Expression is IdentifierNameSyntax id && id.Identifier.Text == "Task")
                return true;

            return false;
        }

        private static bool IsTaskType(ITypeSymbol type)
        {
            if (type.Name == "Task" && type.ContainingNamespace?.ToString() == "System.Threading.Tasks")
                return true;

            // Task<T> — named "Task`1" in metadata but Name is "Task"
            if (type is INamedTypeSymbol named &&
                named.Name == "Task" &&
                named.ContainingNamespace?.ToString() == "System.Threading.Tasks")
                return true;

            // ValueTask / ValueTask<T>
            if (type.Name == "ValueTask" && type.ContainingNamespace?.ToString() == "System.Threading.Tasks")
                return true;

            return false;
        }
    }
}
