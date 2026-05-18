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
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(DoaneServiceScaffoldRefactoring))]
    [Shared]
    public sealed class DoaneServiceScaffoldRefactoring : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var node = root.FindNode(context.Span);
            var classDecl = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null)
                return;

            // Only trigger on empty classes (no members or only an empty body)
            if (!IsEmptyClass(classDecl))
                return;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: "Apply Doane service scaffold",
                    createChangedDocument: ct => ApplyScaffoldAsync(context.Document, classDecl, ct),
                    equivalenceKey: nameof(DoaneServiceScaffoldRefactoring)));
        }

        private static bool IsEmptyClass(ClassDeclarationSyntax classDecl)
        {
            return !classDecl.Members.Any();
        }

        private static async Task<Document> ApplyScaffoldAsync(
            Document document,
            ClassDeclarationSyntax classDecl,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            var className = classDecl.Identifier.Text;

            // Build members for the scaffold:
            // 1. private readonly ILogger<T> _logger;
            var loggerField = SyntaxFactory.ParseMemberDeclaration(
                $"private readonly ILogger<{className}> _logger;")!
                .WithAdditionalAnnotations(Formatter.Annotation);

            // 2. Constructor with ILogger<T> injection
            var constructorBody = SyntaxFactory.ParseStatement(
                $"{{ _logger = logger ?? throw new ArgumentNullException(nameof(logger)); }}");
            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                .WithParameterList(SyntaxFactory.ParseParameterList($"(ILogger<{className}> logger)"))
                .WithBody((BlockSyntax)constructorBody)
                .WithAdditionalAnnotations(Formatter.Annotation);

            // 3. Health check stub method
            var healthMethod = SyntaxFactory.ParseMemberDeclaration(@"
/// <summary>
/// Returns a health status for this service.
/// </summary>
/// <returns>A string indicating the service health.</returns>
public string GetHealth()
{
    _logger.LogInformation(""Health check called on {Service}"", nameof(" + className + @"));
    return ""healthy"";
}")!
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newMembers = SyntaxFactory.List(new MemberDeclarationSyntax[]
            {
                loggerField,
                constructor,
                healthMethod
            });

            var newClass = classDecl.WithMembers(newMembers)
                .WithAdditionalAnnotations(Formatter.Annotation);

            SyntaxNode newRoot = root.ReplaceNode(classDecl, newClass);

            // Ensure required using directives are present
            if (newRoot is CompilationUnitSyntax compilationUnit)
            {
                var requiredUsings = new[]
                {
                    "System",
                    "Microsoft.Extensions.Logging",
                };

                foreach (var ns in requiredUsings)
                {
                    bool hasUsing = compilationUnit.Usings.Any(u => u.Name.ToString() == ns);
                    if (!hasUsing)
                    {
                        var usingDirective = SyntaxFactory
                            .UsingDirective(SyntaxFactory.ParseName(ns))
                            .NormalizeWhitespace()
                            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                        compilationUnit = compilationUnit.AddUsings(usingDirective);
                    }
                }

                newRoot = compilationUnit;
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
