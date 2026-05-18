using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA003_ClassSizeAnalyzer : DiagnosticAnalyzer
    {
        private const int MaxPublicMethods = 10;

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA003,
            title: "Class Too Large",
            messageFormat: "Class '{0}' has {1} public methods (threshold: 10). Consider applying the Single Responsibility Principle.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Classes with too many public methods may violate the Single Responsibility Principle.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;

            // Count direct public methods (not nested class methods)
            int publicMethodCount = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Count(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));

            if (publicMethodCount > MaxPublicMethods)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    classDecl.Identifier.GetLocation(),
                    classDecl.Identifier.Text,
                    publicMethodCount);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
