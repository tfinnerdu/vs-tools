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
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ConvertToRecordRefactoring))]
    [Shared]
    public sealed class ConvertToRecordRefactoring : CodeRefactoringProvider
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

            // Only trigger on simple POCO classes
            if (!IsSimplePoco(classDecl))
                return;

            context.RegisterRefactoring(
                CodeAction.Create(
                    title: "Convert to record",
                    createChangedDocument: ct => ConvertToRecordAsync(context.Document, classDecl, ct),
                    equivalenceKey: nameof(ConvertToRecordRefactoring)));
        }

        private static bool IsSimplePoco(ClassDeclarationSyntax classDecl)
        {
            var members = classDecl.Members;

            // Allow only auto-properties and at most one constructor
            foreach (var member in members)
            {
                if (member is PropertyDeclarationSyntax prop)
                {
                    // Must be an auto-property (has get; and optionally set;/init;)
                    if (prop.AccessorList == null)
                        return false;

                    bool isAuto = prop.AccessorList.Accessors.All(
                        a => a.Body == null && a.ExpressionBody == null);
                    if (!isAuto)
                        return false;
                }
                else if (member is ConstructorDeclarationSyntax)
                {
                    // Allow one constructor
                    continue;
                }
                else if (member is FieldDeclarationSyntax field)
                {
                    // Allow private fields
                    if (!field.Modifiers.Any(SyntaxKind.PrivateKeyword))
                        return false;
                }
                else if (member is MethodDeclarationSyntax)
                {
                    // No methods in a POCO
                    return false;
                }
            }

            // Must have at least one property
            return members.OfType<PropertyDeclarationSyntax>().Any();
        }

        private static async Task<Document> ConvertToRecordAsync(
            Document document,
            ClassDeclarationSyntax classDecl,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            // Collect all auto-properties
            var properties = classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .ToList();

            // Build the positional parameter list text
            var paramParts = properties.Select(p => $"{p.Type.ToString().Trim()} {p.Identifier.Text}");
            var paramListText = string.Join(", ", paramParts);

            // Gather access modifier text
            var modifiersText = classDecl.Modifiers.Any()
                ? classDecl.Modifiers.ToString() + " "
                : "public ";

            // Gather base list text if present
            var baseListText = classDecl.BaseList != null
                ? " " + classDecl.BaseList.ToString()
                : string.Empty;

            // Build: public record ClassName(Type Prop1, Type Prop2, ...);
            var recordText = $"{modifiersText}record {classDecl.Identifier.Text}({paramListText}){baseListText};";

            var recordDecl = SyntaxFactory.ParseMemberDeclaration(recordText)!
                .WithLeadingTrivia(classDecl.GetLeadingTrivia())
                .WithTrailingTrivia(classDecl.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(classDecl, recordDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
