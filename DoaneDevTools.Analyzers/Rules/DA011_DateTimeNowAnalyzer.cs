using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA011_DateTimeNowAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA011,
            title: "Use DateTime.UtcNow Instead of DateTime.Now",
            messageFormat: "Use DateTime.UtcNow instead of DateTime.Now to avoid timezone-related bugs.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "DateTime.Now returns local time which can cause issues in distributed systems. Prefer DateTime.UtcNow.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;

            // Quick name check before semantic model
            if (memberAccess.Name.Identifier.Text != "Now")
                return;

            // Use semantic model to verify it's System.DateTime.Now
            var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
            if (!(symbolInfo.Symbol is IPropertySymbol property))
                return;

            if (property.Name != "Now")
                return;

            if (property.ContainingType?.SpecialType != SpecialType.System_DateTime)
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation()));
        }
    }
}
