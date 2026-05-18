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
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(GenerateRepositoryInterfaceRefactoring))]
    [Shared]
    public sealed class GenerateRepositoryInterfaceRefactoring : CodeRefactoringProvider
    {
        private static readonly string[] CrudPrefixes =
        {
            "Get", "Create", "Update", "Delete", "Find", "Add", "Remove"
        };

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
                return;

            var node = root.FindNode(context.Span);
            var classDecl = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null)
                return;

            // Check for CRUD-pattern methods
            var crudMethods = GetCrudMethods(classDecl);
            if (!crudMethods.Any())
                return;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: "Generate repository interface",
                    createChangedSolution: ct => GenerateInterfaceAsync(context.Document, classDecl, crudMethods, ct),
                    equivalenceKey: nameof(GenerateRepositoryInterfaceRefactoring)));
        }

        private static List<MethodDeclarationSyntax> GetCrudMethods(ClassDeclarationSyntax classDecl)
        {
            return classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                            CrudPrefixes.Any(prefix => m.Identifier.Text.StartsWith(prefix)))
                .ToList();
        }

        private static async Task<Solution> GenerateInterfaceAsync(
            Document document,
            ClassDeclarationSyntax classDecl,
            List<MethodDeclarationSyntax> crudMethods,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document.Project.Solution;

            var className = classDecl.Identifier.Text;
            var interfaceName = $"I{className}";

            // Build interface members from the CRUD methods
            var interfaceMembers = new List<MemberDeclarationSyntax>();
            foreach (var method in crudMethods)
            {
                // Build interface method: ReturnType MethodName(params);
                var interfaceMethod = SyntaxFactory.MethodDeclaration(
                        method.ReturnType.WithTrailingTrivia(SyntaxFactory.Space),
                        method.Identifier)
                    .WithParameterList(method.ParameterList)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                    .WithAdditionalAnnotations(Formatter.Annotation);

                interfaceMembers.Add(interfaceMethod);
            }

            // Get the namespace of the containing class
            var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var fileScopedNs = classDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();

            string? namespaceName = namespaceDecl?.Name.ToString() ?? fileScopedNs?.Name.ToString();

            // Build the interface declaration
            var interfaceDecl = SyntaxFactory.InterfaceDeclaration(interfaceName)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                .WithMembers(SyntaxFactory.List(interfaceMembers))
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Build the compilation unit for the interface file
            CompilationUnitSyntax interfaceCompilationUnit;

            if (namespaceName != null)
            {
                var ns = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDecl))
                    .WithAdditionalAnnotations(Formatter.Annotation);

                interfaceCompilationUnit = SyntaxFactory.CompilationUnit()
                    .WithUsings(((CompilationUnitSyntax)root).Usings)
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(ns))
                    .NormalizeWhitespace();
            }
            else
            {
                interfaceCompilationUnit = SyntaxFactory.CompilationUnit()
                    .WithUsings(((CompilationUnitSyntax)root).Usings)
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDecl))
                    .NormalizeWhitespace();
            }

            // Create a new document for the interface
            var project = document.Project;
            var interfaceDocumentId = DocumentId.CreateNewId(project.Id);
            var interfaceFileName = $"{interfaceName}.cs";

            // Determine folder
            var folders = document.Folders;

            var newSolution = project.Solution.AddDocument(
                interfaceDocumentId,
                interfaceFileName,
                interfaceCompilationUnit.ToFullString(),
                folders);

            // Also make the class implement the interface
            var updatedClassDecl = classDecl;
            if (classDecl.BaseList == null)
            {
                updatedClassDecl = classDecl.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(
                                SyntaxFactory.ParseTypeName(interfaceName)))));
            }
            else
            {
                updatedClassDecl = classDecl.WithBaseList(
                    classDecl.BaseList.AddTypes(
                        SyntaxFactory.SimpleBaseType(
                            SyntaxFactory.ParseTypeName(interfaceName))));
            }

            var newRoot = root.ReplaceNode(classDecl, updatedClassDecl);
            newSolution = newSolution.WithDocumentSyntaxRoot(document.Id, newRoot);

            return newSolution;
        }
    }
}
