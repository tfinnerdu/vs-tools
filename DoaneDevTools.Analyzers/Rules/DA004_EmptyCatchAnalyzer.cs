using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA004_EmptyCatchAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA004,
            title: "Empty Catch Block",
            messageFormat: "Empty catch block silently swallows exceptions. Add error handling or logging.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Empty catch blocks hide errors and make debugging difficult.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        }

        private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
        {
            var catchClause = (CatchClauseSyntax)context.Node;
            var block = catchClause.Block;

            if (!IsBlockEffectivelyEmpty(block))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation()));
        }

        private static bool IsBlockEffectivelyEmpty(BlockSyntax block)
        {
            // No statements at all
            if (!block.Statements.Any())
                return true;

            // All statements are effectively comment-only (impossible in syntax, but check for empty expression stmts)
            // In practice: if the only tokens inside the braces (excluding braces) are trivia (comments),
            // there are no actual statements.
            // Since the parser won't produce statements for comments, if Statements is empty the block is empty.
            // However we also check if statements contain only empty constructs.
            foreach (var stmt in block.Statements)
            {
                // If a statement exists and is not a comment-only artifact, the block is not empty
                if (stmt != null)
                    return false;
            }

            return true;
        }
    }
}
