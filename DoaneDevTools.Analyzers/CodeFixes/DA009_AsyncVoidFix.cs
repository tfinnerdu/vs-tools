using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DoaneDevTools.Analyzers.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DA009_AsyncVoidFix))]
    [Shared]
    public sealed class DA009_AsyncVoidFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DA009);

        public override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var method = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (method == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change return type to Task",
                    createChangedDocument: ct => ChangeToAsyncTaskAsync(context.Document, method, ct),
                    equivalenceKey: nameof(DA009_AsyncVoidFix)),
                diagnostic);
        }

        private static async Task<Document> ChangeToAsyncTaskAsync(
            Document document,
            MethodDeclarationSyntax method,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Replace the void return type with Task
            var taskType = SyntaxFactory.ParseTypeName("Task")
                .WithTriviaFrom(method.ReturnType);

            var newMethod = method.WithReturnType(taskType);

            // Check if we need to add a using for System.Threading.Tasks
            var compilationUnit = root as CompilationUnitSyntax;
            SyntaxNode newRoot = root.ReplaceNode(method, newMethod);

            if (compilationUnit != null)
            {
                var hasTasksUsing = compilationUnit.Usings
                    .Any(u => u.Name.ToString() == "System.Threading.Tasks");

                if (!hasTasksUsing)
                {
                    var updatedUnit = (CompilationUnitSyntax)newRoot;
                    var usingDirective = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName("System.Threading.Tasks"))
                        .NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    updatedUnit = updatedUnit.AddUsings(usingDirective);
                    newRoot = updatedUnit;
                }
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
