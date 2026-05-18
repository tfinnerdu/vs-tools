using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA007_ParameterCountAnalyzer : DiagnosticAnalyzer
    {
        private const int MaxParameters = 5;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA007,
            title: "Too Many Parameters",
            messageFormat: "Method '{0}' has {1} parameters (threshold: 5). Consider introducing a parameter object.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Methods with too many parameters are difficult to call and understand.");

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
            int paramCount = method.ParameterList.Parameters.Count;

            if (paramCount > MaxParameters)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text,
                    paramCount);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
