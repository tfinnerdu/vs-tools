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
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(IntroduceCancellationTokenRefactoring))]
    [Shared]
    public sealed class IntroduceCancellationTokenRefactoring : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var node = root.FindNode(context.Span);

            // Trigger on a method declaration
            var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method == null)
                return;

            // Skip if already has a CancellationToken parameter
            bool alreadyHas = method.ParameterList.Parameters
                .Any(p => p.Type != null && IsCancellationTokenType(p.Type));

            if (alreadyHas)
                return;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: "Add CancellationToken parameter",
                    createChangedDocument: ct => AddCancellationTokenAsync(context.Document, method, ct),
                    equivalenceKey: nameof(IntroduceCancellationTokenRefactoring)));
        }

        private static async Task<Document> AddCancellationTokenAsync(
            Document document,
            MethodDeclarationSyntax method,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Build: CancellationToken cancellationToken = default
            var ctParameter = SyntaxFactory.Parameter(
                    SyntaxFactory.List<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(),
                    SyntaxFactory.ParseTypeName("CancellationToken"),
                    SyntaxFactory.Identifier(" cancellationToken"),
                    SyntaxFactory.EqualsValueClause(
                        SyntaxFactory.Token(SyntaxKind.EqualsToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
                        SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression,
                            SyntaxFactory.Token(SyntaxKind.DefaultKeyword))))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newParameterList = method.ParameterList.AddParameters(ctParameter);
            var newMethod = method.WithParameterList(newParameterList);

            SyntaxNode newRoot = root.ReplaceNode(method, newMethod);

            // Add using System.Threading; if missing
            if (newRoot is CompilationUnitSyntax compilationUnit)
            {
                bool hasUsing = compilationUnit.Usings
                    .Any(u => u.Name.ToString() == "System.Threading");

                if (!hasUsing)
                {
                    var usingDirective = SyntaxFactory
                        .UsingDirective(SyntaxFactory.ParseName("System.Threading"))
                        .NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    newRoot = compilationUnit.AddUsings(usingDirective);
                }
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private static bool IsCancellationTokenType(TypeSyntax type)
        {
            if (type is IdentifierNameSyntax id)
                return id.Identifier.Text == "CancellationToken";
            if (type is QualifiedNameSyntax qualified)
                return qualified.Right.Identifier.Text == "CancellationToken";
            return false;
        }
    }
}
