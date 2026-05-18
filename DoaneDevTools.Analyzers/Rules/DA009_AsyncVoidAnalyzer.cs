using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA009_AsyncVoidAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA009,
            title: "Avoid async void",
            messageFormat: "Method '{0}' is declared async void. Use async Task instead to allow proper exception handling.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "async void methods cannot be awaited and exceptions they throw will crash the process.");

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

            // Must be async
            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                return;

            // Return type must be void
            if (!(method.ReturnType is PredefinedTypeSyntax predefined) ||
                !predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
                return;

            // Exclude event handlers
            if (IsEventHandler(method))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                method.Identifier.GetLocation(),
                method.Identifier.Text));
        }

        private static bool IsEventHandler(MethodDeclarationSyntax method)
        {
            // Pattern 1: On<Event> naming convention
            var name = method.Identifier.Text;
            if (name.Length > 2 && name.StartsWith("On") && char.IsUpper(name[2]))
                return true;

            // Pattern 2: (object sender, EventArgs e) or (object sender, <X>EventArgs e)
            var parameters = method.ParameterList.Parameters;
            if (parameters.Count == 2)
            {
                var firstParam = parameters[0];
                var secondParam = parameters[1];

                bool firstIsObject = firstParam.Type is PredefinedTypeSyntax pt &&
                                     pt.Keyword.IsKind(SyntaxKind.ObjectKeyword);

                bool secondIsEventArgs = false;
                if (secondParam.Type is IdentifierNameSyntax idName)
                {
                    secondIsEventArgs = idName.Identifier.Text == "EventArgs" ||
                                        idName.Identifier.Text.EndsWith("EventArgs");
                }
                else if (secondParam.Type is QualifiedNameSyntax qName)
                {
                    secondIsEventArgs = qName.Right.Identifier.Text == "EventArgs" ||
                                        qName.Right.Identifier.Text.EndsWith("EventArgs");
                }

                if (firstIsObject && secondIsEventArgs)
                    return true;
            }

            return false;
        }
    }
}
