using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS003_HardcodedConnectionStringAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS003,
            title: "Hardcoded Connection String",
            messageFormat: "Connection string appears to be hardcoded. Use environment variables or configuration instead.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Hardcoded connection strings expose credentials and make configuration changes difficult. Use environment variables or a configuration provider.");

        // Patterns that indicate a connection string (case-insensitive checked below)
        private static readonly string[] ConnectionStringPatterns =
        {
            "Server=",
            "Data Source=",
            "Initial Catalog=",
            "Password=",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
        }

        private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            var value = literal.Token.ValueText;

            if (string.IsNullOrEmpty(value))
                return;

            foreach (var pattern in ConnectionStringPatterns)
            {
                if (value.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation()));
                    return;
                }
            }
        }
    }
}
