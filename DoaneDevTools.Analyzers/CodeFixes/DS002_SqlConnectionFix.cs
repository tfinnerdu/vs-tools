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
using Microsoft.CodeAnalysis.Formatting;

namespace DoaneDevTools.Analyzers.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DS002_SqlConnectionFix))]
    [Shared]
    public sealed class DS002_SqlConnectionFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DS002);

        public override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var objectCreation = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<ObjectCreationExpressionSyntax>()
                .FirstOrDefault();

            if (objectCreation == null)
                return;

            // Find the containing local variable declaration
            var localDecl = objectCreation.Ancestors()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault();

            if (localDecl == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Wrap SqlConnection in using",
                    createChangedDocument: ct => WrapInUsingAsync(context.Document, localDecl, ct),
                    equivalenceKey: nameof(DS002_SqlConnectionFix)),
                diagnostic);
        }

        private static async Task<Document> WrapInUsingAsync(
            Document document,
            LocalDeclarationStatementSyntax localDecl,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Determine if the containing method is async
            var containingMethod = localDecl.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            bool isAsync = containingMethod?.Modifiers.Any(SyntaxKind.AsyncKeyword) == true;

            // Add the 'using' keyword modifier to the local declaration
            SyntaxToken usingKeyword = SyntaxFactory.Token(SyntaxKind.UsingKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);

            LocalDeclarationStatementSyntax newLocalDecl;

            if (isAsync)
            {
                // await using var conn = new SqlConnection(...);
                SyntaxToken awaitKeyword = SyntaxFactory.Token(SyntaxKind.AwaitKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space);

                newLocalDecl = localDecl
                    .WithAwaitKeyword(awaitKeyword)
                    .WithUsingKeyword(usingKeyword)
                    .WithAdditionalAnnotations(Formatter.Annotation);
            }
            else
            {
                // using var conn = new SqlConnection(...);
                newLocalDecl = localDecl
                    .WithUsingKeyword(usingKeyword)
                    .WithAdditionalAnnotations(Formatter.Annotation);
            }

            var newRoot = root.ReplaceNode(localDecl, newLocalDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
