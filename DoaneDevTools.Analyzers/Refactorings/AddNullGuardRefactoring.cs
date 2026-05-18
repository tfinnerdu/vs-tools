using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace DoaneDevTools.Analyzers.Refactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AddNullGuardRefactoring))]
    [Shared]
    public sealed class AddNullGuardRefactoring : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var node = root.FindNode(context.Span);

            // Find a parameter node under the cursor
            var parameter = node.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
            if (parameter == null)
                return;

            // Find the containing method
            var method = parameter.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method == null || method.Body == null)
                return;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: "Add null guard",
                    createChangedDocument: ct => AddNullGuardsAsync(context.Document, method, parameter, ct),
                    equivalenceKey: nameof(AddNullGuardRefactoring)));
        }

        private static async Task<Document> AddNullGuardsAsync(
            Document document,
            MethodDeclarationSyntax method,
            ParameterSyntax selectedParameter,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Determine which parameters to guard: the selected one (and any others that are reference/nullable types)
            var paramsToGuard = new List<ParameterSyntax> { selectedParameter };

            // Build ThrowIfNull statements for each param
            var guardStatements = new List<StatementSyntax>();
            foreach (var param in paramsToGuard)
            {
                var paramName = param.Identifier.Text;
                // ArgumentNullException.ThrowIfNull(paramName);
                var statement = SyntaxFactory.ParseStatement(
                    $"ArgumentNullException.ThrowIfNull({paramName});")
                    .WithAdditionalAnnotations(Formatter.Annotation);
                guardStatements.Add(statement);
            }

            // Insert at the top of the method body
            var existingStatements = method.Body!.Statements;
            var newStatements = SyntaxFactory.List(
                guardStatements.Concat(existingStatements));

            var newBody = method.Body.WithStatements(newStatements)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newMethod = method.WithBody(newBody);
            var newRoot = root.ReplaceNode(method, newMethod);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
