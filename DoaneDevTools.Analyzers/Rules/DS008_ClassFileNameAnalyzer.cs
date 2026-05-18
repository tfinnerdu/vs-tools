using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS008_ClassFileNameAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS008,
            title: "Class Name Does Not Match File Name",
            messageFormat: "Class '{0}' does not match the file name '{1}'. Rename the class or the file.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "The primary class in a file should have the same name as the file for better navigability.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;

            // Only check the first (primary) class in the file — those not nested inside another type
            if (classDecl.Parent is TypeDeclarationSyntax)
                return;

            // Only check the first top-level class declaration in the compilation unit
            var compilationUnit = classDecl.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
            if (compilationUnit == null)
                return;

            var firstClass = compilationUnit.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => !(c.Parent is TypeDeclarationSyntax))
                .FirstOrDefault();

            if (firstClass != classDecl)
                return;

            // Get the file name without extension
            var filePath = classDecl.SyntaxTree.FilePath;
            if (string.IsNullOrEmpty(filePath))
                return;

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

            // Strip any trailing ".g", ".designer", etc. (generated file suffixes)
            var className = classDecl.Identifier.Text;

            if (className != fileNameWithoutExtension)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    classDecl.Identifier.GetLocation(),
                    className,
                    fileNameWithoutExtension));
            }
        }
    }
}
