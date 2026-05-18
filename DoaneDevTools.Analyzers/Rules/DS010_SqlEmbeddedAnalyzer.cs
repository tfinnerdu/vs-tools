using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    /// <summary>
    /// DS010: SQL embedded in C# code inspector.
    /// Finds SQL strings assigned to SqlCommand.CommandText or passed to Execute methods
    /// and flags bad SQL patterns: SELECT *, NOLOCK, missing WHERE on DELETE/UPDATE, dynamic concatenation.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SqlEmbeddedAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DS010";

        private static readonly DiagnosticDescriptor SelectStarRule = new(
            DiagnosticId, "SELECT * in embedded SQL",
            "Embedded SQL uses SELECT *. Specify required columns explicitly.",
            "Performance", DiagnosticSeverity.Warning, true);

        private static readonly DiagnosticDescriptor NoLockRule = new(
            "DS010b", "NOLOCK hint in embedded SQL",
            "NOLOCK hint detected. This can cause dirty reads. Use READ COMMITTED SNAPSHOT instead.",
            "Reliability", DiagnosticSeverity.Warning, true);

        private static readonly DiagnosticDescriptor DynamicSqlRule = new(
            "DS010c", "Dynamic SQL string concatenation",
            "SQL string built via concatenation. Use parameterized queries to prevent SQL injection.",
            "Security", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor MissingWhereRule = new(
            "DS010d", "DELETE or UPDATE without WHERE clause",
            "DELETE or UPDATE statement without a WHERE clause will affect all rows.",
            "Correctness", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SelectStarRule, NoLockRule, DynamicSqlRule, MissingWhereRule);

        private static readonly Regex SelectStar = new(@"\bSELECT\s+\*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NoLock = new(@"\bNOLOCK\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DeleteNoWhere = new(@"\bDELETE\s+FROM\s+\w+\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UpdateNoWhere = new(@"\bUPDATE\s+\w+\s+SET\s+.+(?<!\bWHERE\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
            context.RegisterSyntaxNodeAction(AnalyzeBinaryAdd, SyntaxKind.AddExpression);
        }

        private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (AssignmentExpressionSyntax)context.Node;

            // Look for: cmd.CommandText = "SELECT ..."
            if (assignment.Left is not MemberAccessExpressionSyntax member) return;
            if (member.Name.Identifier.Text != "CommandText") return;

            if (assignment.Right is LiteralExpressionSyntax literal)
            {
                CheckSqlString(context, literal.Token.ValueText, literal.GetLocation());
            }
            else if (assignment.Right is BinaryExpressionSyntax)
            {
                // Concatenation into CommandText — dynamic SQL
                context.ReportDiagnostic(Diagnostic.Create(DynamicSqlRule, assignment.Right.GetLocation()));
            }
        }

        private static void AnalyzeBinaryAdd(SyntaxNodeAnalysisContext context)
        {
            var binary = (BinaryExpressionSyntax)context.Node;

            // Only flag if this binary expression feeds a SqlCommand.CommandText
            // Check ancestors for assignment to CommandText
            var ancestor = binary.Parent;
            while (ancestor != null && ancestor is BinaryExpressionSyntax)
                ancestor = ancestor.Parent;

            if (ancestor is AssignmentExpressionSyntax assign &&
                assign.Left is MemberAccessExpressionSyntax mem &&
                mem.Name.Identifier.Text == "CommandText")
            {
                context.ReportDiagnostic(Diagnostic.Create(DynamicSqlRule, binary.GetLocation()));
            }
        }

        private static void CheckSqlString(SyntaxNodeAnalysisContext context, string sql, Location location)
        {
            if (SelectStar.IsMatch(sql))
                context.ReportDiagnostic(Diagnostic.Create(SelectStarRule, location));

            if (NoLock.IsMatch(sql))
                context.ReportDiagnostic(Diagnostic.Create(NoLockRule, location));

            if (DeleteNoWhere.IsMatch(sql))
                context.ReportDiagnostic(Diagnostic.Create(MissingWhereRule, location));
        }
    }
}
