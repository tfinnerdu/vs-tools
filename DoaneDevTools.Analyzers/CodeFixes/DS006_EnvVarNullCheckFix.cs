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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DS006_EnvVarNullCheckFix))]
    [Shared]
    public sealed class DS006_EnvVarNullCheckFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DS006);

        public override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocation = root.FindToken(diagnosticSpan.Start)
                .Parent?
                .AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();

            if (invocation == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add null guard for environment variable",
                    createChangedDocument: ct => AddNullGuardAsync(context.Document, invocation, ct),
                    equivalenceKey: nameof(DS006_EnvVarNullCheckFix)),
                diagnostic);
        }

        private static async Task<Document> AddNullGuardAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Extract the variable name from the first argument for the error message
            string varName = "ENV_VAR";
            var args = invocation.ArgumentList.Arguments;
            if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax lit)
            {
                varName = lit.Token.ValueText;
            }

            // Build: Environment.GetEnvironmentVariable("VAR")
            //        ?? throw new InvalidOperationException($"Required environment variable 'VAR' is not set")
            var throwExpr = SyntaxFactory.ParseExpression(
                $"throw new InvalidOperationException($\"Required environment variable '{varName}' is not set\")");

            var coalesceExpr = SyntaxFactory.BinaryExpression(
                SyntaxKind.CoalesceExpression,
                invocation.WithoutTrivia(),
                throwExpr)
                .WithTriviaFrom(invocation)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(invocation, coalesceExpr);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
