using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DoaneDevTools.Analyzers.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DA008_StringFormatFix))]
    [Shared]
    public sealed class DA008_StringFormatFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.DA008);

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
                    title: "Convert to string interpolation",
                    createChangedDocument: ct => ConvertToInterpolationAsync(context.Document, invocation, ct),
                    equivalenceKey: nameof(DA008_StringFormatFix)),
                diagnostic);
        }

        private static async Task<Document> ConvertToInterpolationAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
                return document;

            var args = invocation.ArgumentList.Arguments;
            if (args.Count < 2)
                return document;

            // First arg is the format string
            if (!(args[0].Expression is LiteralExpressionSyntax formatLiteral))
                return document;

            var formatString = formatLiteral.Token.ValueText;

            // The remaining args are the values to interpolate
            var formatArgs = args.Skip(1).Select(a => a.Expression).ToList();

            // Build the interpolated string content by replacing {N} and {N:format} placeholders
            var interpolatedContent = BuildInterpolatedString(formatString, formatArgs);
            if (interpolatedContent == null)
                return document;

            var newRoot = root.ReplaceNode(invocation, interpolatedContent
                .WithTriviaFrom(invocation));

            return document.WithSyntaxRoot(newRoot);
        }

        private static InterpolatedStringExpressionSyntax? BuildInterpolatedString(
            string format,
            System.Collections.Generic.List<ExpressionSyntax> args)
        {
            // Parse {N} and {N:formatSpec} patterns
            var placeholderPattern = new Regex(@"\{(\d+)(?::([^}]*))?\}");
            var contents = new System.Collections.Generic.List<InterpolatedStringContentSyntax>();

            int lastIndex = 0;
            foreach (Match match in placeholderPattern.Matches(format))
            {
                // Text before this placeholder
                if (match.Index > lastIndex)
                {
                    var text = format.Substring(lastIndex, match.Index - lastIndex)
                        .Replace("{", "{{").Replace("}", "}}"); // escape braces in text portions
                    // Unescape for interpolated string — the SyntaxFactory handles this
                    contents.Add(SyntaxFactory.InterpolatedStringText(
                        SyntaxFactory.Token(
                            SyntaxTriviaList.Empty,
                            SyntaxKind.InterpolatedStringTextToken,
                            format.Substring(lastIndex, match.Index - lastIndex),
                            format.Substring(lastIndex, match.Index - lastIndex),
                            SyntaxTriviaList.Empty)));
                }

                // Get the argument index
                if (!int.TryParse(match.Groups[1].Value, out int argIndex))
                    return null;

                if (argIndex >= args.Count)
                    return null;

                var argExpr = args[argIndex];
                var formatSpec = match.Groups[2].Success ? match.Groups[2].Value : null;

                InterpolationSyntax interpolation;
                if (formatSpec != null)
                {
                    var formatClause = SyntaxFactory.InterpolationFormatClause(
                        SyntaxFactory.Token(SyntaxKind.ColonToken),
                        SyntaxFactory.Token(
                            SyntaxTriviaList.Empty,
                            SyntaxKind.InterpolatedStringTextToken,
                            formatSpec,
                            formatSpec,
                            SyntaxTriviaList.Empty));
                    interpolation = SyntaxFactory.Interpolation(argExpr, null, formatClause);
                }
                else
                {
                    interpolation = SyntaxFactory.Interpolation(argExpr);
                }

                contents.Add(interpolation);
                lastIndex = match.Index + match.Length;
            }

            // Remaining text after the last placeholder
            if (lastIndex < format.Length)
            {
                var remaining = format.Substring(lastIndex);
                contents.Add(SyntaxFactory.InterpolatedStringText(
                    SyntaxFactory.Token(
                        SyntaxTriviaList.Empty,
                        SyntaxKind.InterpolatedStringTextToken,
                        remaining,
                        remaining,
                        SyntaxTriviaList.Empty)));
            }

            return SyntaxFactory.InterpolatedStringExpression(
                SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
                SyntaxFactory.List(contents),
                SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));
        }
    }
}
