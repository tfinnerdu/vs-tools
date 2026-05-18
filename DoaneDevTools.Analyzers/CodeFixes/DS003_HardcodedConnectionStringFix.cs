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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DS003_HardcodedConnectionStringFix))]
    [Shared]
    public sealed class DS003_HardcodedConnectionStringFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DS003);

        public override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var literal = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<LiteralExpressionSyntax>()
                .FirstOrDefault();

            if (literal == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Replace with Environment.GetEnvironmentVariable",
                    createChangedDocument: ct => ReplaceWithEnvVarAsync(context.Document, literal, ct),
                    equivalenceKey: nameof(DS003_HardcodedConnectionStringFix)),
                diagnostic);
        }

        private static async Task<Document> ReplaceWithEnvVarAsync(
            Document document,
            LiteralExpressionSyntax literal,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Build: Environment.GetEnvironmentVariable("CONNECTION_STRING")
            //        ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set")
            const string envVarName = "CONNECTION_STRING";

            var replacementText =
                $"Environment.GetEnvironmentVariable(\"{envVarName}\") " +
                $"?? throw new InvalidOperationException(\"{envVarName} environment variable is not set\")";

            var replacement = SyntaxFactory.ParseExpression(replacementText)
                .WithTriviaFrom(literal)
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Add a TODO comment on the preceding line by attaching it as leading trivia
            var todoTrivia = SyntaxFactory.TriviaList(
                SyntaxFactory.Comment("// TODO: set CONNECTION_STRING in your .env file"),
                SyntaxFactory.CarriageReturnLineFeed);

            var existingLeading = replacement.GetLeadingTrivia();
            replacement = replacement.WithLeadingTrivia(todoTrivia.AddRange(existingLeading));

            var newRoot = root.ReplaceNode(literal, replacement);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
