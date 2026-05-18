using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS004_HttpClientAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS004,
            title: "Direct HttpClient Instantiation",
            messageFormat: "Avoid instantiating HttpClient directly. Use IHttpClientFactory to manage HttpClient lifetime and connection pooling.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Directly instantiating HttpClient can cause socket exhaustion. Use IHttpClientFactory instead.");

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

            // Quick syntax check
            var typeName = GetTypeName(objectCreation.Type);
            if (typeName != "HttpClient")
                return;

            // Verify via semantic model
            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken);
            var type = typeInfo.Type;
            if (type == null)
                return;

            if (type.ToDisplayString() != "System.Net.Http.HttpClient")
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
    }
}
