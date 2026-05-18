using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS002_SqlConnectionAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS002,
            title: "SqlConnection Not in Using",
            messageFormat: "SqlConnection should be wrapped in a 'using' statement to ensure the connection is properly closed.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "SqlConnection implements IDisposable. Always wrap it in a using statement to ensure the connection is closed and resources are released.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            // Quick name check
            var typeName = GetTypeName(objectCreation.Type);
            if (typeName != "SqlConnection")
                return;

            // Verify via semantic model
            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken);
            var type = typeInfo.Type;
            if (type == null)
                return;

            var fullName = type.ToDisplayString();
            if (fullName != "System.Data.SqlClient.SqlConnection" &&
                fullName != "Microsoft.Data.SqlClient.SqlConnection")
                return;

            // Check if wrapped in using
            if (IsWrappedInUsing(objectCreation))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, objectCreation.GetLocation()));
        }

        private static string GetTypeName(TypeSyntax type)
        {
            if (type is IdentifierNameSyntax id)
                return id.Identifier.Text;
            if (type is QualifiedNameSyntax qualified)
                return qualified.Right.Identifier.Text;
            return string.Empty;
        }

        private static bool IsWrappedInUsing(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                // using (var conn = new SqlConnection(...)) { }
                if (ancestor is UsingStatementSyntax)
                    return true;

                // using var conn = new SqlConnection(...);  OR  await using var conn = ...
                if (ancestor is LocalDeclarationStatementSyntax localDecl)
                {
                    if (localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
                        return true;
                }

                // Stop searching once we leave the statement boundary
                if (ancestor is BlockSyntax || ancestor is MethodDeclarationSyntax)
                    break;
            }
            return false;
        }
    }
}
