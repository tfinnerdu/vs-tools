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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DA004_EmptyCatchFix))]
    [Shared]
    public sealed class DA004_EmptyCatchFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DA004);

        public override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var catchClause = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<CatchClauseSyntax>()
                .FirstOrDefault();

            if (catchClause == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add exception logging stub",
                    createChangedDocument: ct => AddLoggingStubAsync(context.Document, catchClause, ct),
                    equivalenceKey: nameof(DA004_EmptyCatchFix)),
                diagnostic);
        }

        private static async Task<Document> AddLoggingStubAsync(
            Document document,
            CatchClauseSyntax catchClause,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Find the containing method name
            var containingMethod = catchClause.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
            var methodName = containingMethod?.Identifier.Text ?? "UnknownMethod";

            // Find the exception variable name from the catch declaration
            var exceptionVar = catchClause.Declaration?.Identifier.Text ?? "ex";

            // Build the comment and logging statement
            var todoComment = SyntaxFactory.Comment("// TODO: handle exception");
            var loggingText = $"_logger?.LogError({exceptionVar}, \"Unhandled exception in {{Method}}\", nameof({methodName}));";

            // Parse the logging statement
            var loggingStatement = SyntaxFactory.ParseStatement(loggingText)
                .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Build comment as leading trivia on the statement
            var commentTrivia = SyntaxFactory.TriviaList(
                SyntaxFactory.ElasticCarriageReturnLineFeed,
                todoComment,
                SyntaxFactory.ElasticCarriageReturnLineFeed);

            loggingStatement = loggingStatement.WithLeadingTrivia(commentTrivia);

            // Create a new block with the stub
            var newBlock = catchClause.Block.AddStatements(loggingStatement)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newCatchClause = catchClause.WithBlock(newBlock);
            var newRoot = root.ReplaceNode(catchClause, newCatchClause);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
