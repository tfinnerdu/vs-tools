using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    /// <summary>
    /// DS-SEC: Scans string literals for high-entropy API keys, tokens, passwords, and connection strings
    /// that should not be committed to source control. Complements DS003 with a broader sweep.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SecretScannerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DSSEC";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Potential secret or credential in source",
            messageFormat: "String literal appears to contain a secret ({0}). Move to environment variable or secrets manager.",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Hardcoded secrets, API keys, and passwords should never appear in source code.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        // High-confidence prefixes for known secret formats
        private static readonly Regex[] SecretPatterns = new[]
        {
            new Regex(@"(?i)(password|passwd|pwd)\s*=\s*[""'][^""']{6,}", RegexOptions.Compiled),
            new Regex(@"(?i)api[_-]?key\s*=\s*[""'][^""']{10,}", RegexOptions.Compiled),
            new Regex(@"(?i)(secret|token)\s*=\s*[""'][^""']{10,}", RegexOptions.Compiled),
            new Regex(@"(?i)bearer\s+[A-Za-z0-9+/=_\-]{20,}", RegexOptions.Compiled),
            new Regex(@"sk_live_[A-Za-z0-9]{20,}", RegexOptions.Compiled),   // Stripe live key
            new Regex(@"sk_test_[A-Za-z0-9]{20,}", RegexOptions.Compiled),   // Stripe test key
            new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled),            // AWS access key
            new Regex(@"ghp_[A-Za-z0-9]{36}", RegexOptions.Compiled),         // GitHub personal access token
            new Regex(@"glpat-[A-Za-z0-9\-_]{20}", RegexOptions.Compiled),   // GitLab PAT
            new Regex(@"(?i)Server=.{5,};.{0,20}Password=[^;]{3,}", RegexOptions.Compiled), // Connection string with password
        };

        private static readonly Regex HighEntropyBase64 = new(
            @"[A-Za-z0-9+/]{40,}={0,2}", RegexOptions.Compiled);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression);
            context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.InterpolatedStringText);
        }

        private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
        {
            string text;
            Location location;

            if (context.Node is LiteralExpressionSyntax literal)
            {
                text = literal.Token.ValueText;
                location = literal.GetLocation();
            }
            else if (context.Node is InterpolatedStringTextSyntax interpolatedText)
            {
                text = interpolatedText.TextToken.ValueText;
                location = interpolatedText.GetLocation();
            }
            else return;

            if (string.IsNullOrWhiteSpace(text) || text.Length < 8) return;

            // Skip test/example values
            if (text.Contains("example", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("change_me", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("your_key_here", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var pattern in SecretPatterns)
            {
                if (pattern.IsMatch(text))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule, location, DescribeMatch(pattern, text)));
                    return;
                }
            }

            // High-entropy check: long Base64-like strings that aren't obvious data
            if (text.Length >= 40 && HighEntropyBase64.IsMatch(text) && CalculateEntropy(text) > 4.5)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, location, "high-entropy string (possible token or key)"));
            }
        }

        private static string DescribeMatch(Regex pattern, string text)
        {
            if (text.Contains("password", StringComparison.OrdinalIgnoreCase)) return "password";
            if (text.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("apikey", StringComparison.OrdinalIgnoreCase)) return "API key";
            if (text.Contains("token", StringComparison.OrdinalIgnoreCase)) return "token";
            if (text.Contains("secret", StringComparison.OrdinalIgnoreCase)) return "secret";
            if (text.StartsWith("sk_", StringComparison.OrdinalIgnoreCase)) return "Stripe key";
            if (text.StartsWith("AKIA")) return "AWS access key";
            if (text.StartsWith("ghp_")) return "GitHub PAT";
            return "credential pattern";
        }

        private static double CalculateEntropy(string s)
        {
            var freq = new int[256];
            foreach (var c in s)
                if (c < 256) freq[c]++;

            double entropy = 0;
            foreach (var f in freq)
            {
                if (f == 0) continue;
                double p = (double)f / s.Length;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy;
        }
    }
}
