using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DA005_MagicNumberAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DA005,
            title: "Magic Number",
            messageFormat: "Magic number '{0}' detected. Consider extracting it to a named constant.",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Magic numbers make code harder to understand and maintain.");

        // Allowed numeric literal values
        private static readonly double[] AllowedValues = { 0, 1, -1, 2 };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNumericLiteral, SyntaxKind.NumericLiteralExpression);
        }

        private static void AnalyzeNumericLiteral(SyntaxNodeAnalysisContext context)
        {
            var literal = (LiteralExpressionSyntax)context.Node;
            var token = literal.Token;

            // Only check int, double, float, decimal literals
            if (token.Kind() != SyntaxKind.NumericLiteralToken)
                return;

            // Try to get the numeric value
            double numericValue = 0;
            bool parsed = false;

            if (token.Value is int intVal) { numericValue = intVal; parsed = true; }
            else if (token.Value is long longVal) { numericValue = longVal; parsed = true; }
            else if (token.Value is double dblVal) { numericValue = dblVal; parsed = true; }
            else if (token.Value is float fltVal) { numericValue = fltVal; parsed = true; }
            else if (token.Value is decimal decVal) { numericValue = (double)decVal; parsed = true; }
            else if (token.Value is uint uintVal) { numericValue = uintVal; parsed = true; }
            else if (token.Value is ulong ulongVal) { numericValue = ulongVal; parsed = true; }

            if (!parsed)
                return;

            // Check if value is in the allowed list
            foreach (var allowed in AllowedValues)
            {
                if (numericValue == allowed)
                    return;
            }

            // Check for negation: PrefixUnaryExpression with MinusToken wrapping this literal
            // so -1 is allowed: the literal itself is 1, parent is PrefixUnaryExpressionSyntax
            var parent = literal.Parent;
            if (parent is PrefixUnaryExpressionSyntax prefix &&
                prefix.OperatorToken.IsKind(SyntaxKind.MinusToken))
            {
                // Check if the negated value is allowed
                double negated = -numericValue;
                foreach (var allowed in AllowedValues)
                {
                    if (negated == allowed)
                        return;
                }
            }

            // Exclude: enum member declarations
            if (IsInsideEnumMember(literal))
                return;

            // Exclude: attribute arguments
            if (IsInsideAttribute(literal))
                return;

            // Exclude: default parameter values
            if (IsDefaultParameterValue(literal))
                return;

            // Exclude: field or property initializers with const
            if (IsConstFieldOrPropertyInitializer(literal))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, literal.GetLocation(), token.Text));
        }

        private static bool IsInsideEnumMember(SyntaxNode node)
        {
            return node.Ancestors().OfType<EnumMemberDeclarationSyntax>().Any();
        }

        private static bool IsInsideAttribute(SyntaxNode node)
        {
            return node.Ancestors().OfType<AttributeArgumentSyntax>().Any();
        }

        private static bool IsDefaultParameterValue(SyntaxNode node)
        {
            return node.Ancestors().OfType<ParameterSyntax>().Any(p => p.Default != null && p.Default.Value.Contains(node));
        }

        private static bool IsConstFieldOrPropertyInitializer(SyntaxNode node)
        {
            // Check if inside a field declaration that is const
            var fieldDecl = node.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();
            if (fieldDecl != null)
            {
                if (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    return true;
            }

            // Check if inside a local declaration that is const
            var localDecl = node.Ancestors().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
            if (localDecl != null)
            {
                if (localDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    return true;
            }

            return false;
        }
    }

    internal static class SyntaxNodeExtensions
    {
        internal static bool Contains(this SyntaxNode node, SyntaxNode descendant)
        {
            return descendant.Ancestors().Any(a => a == node);
        }
    }
}
