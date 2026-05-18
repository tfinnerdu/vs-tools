using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA012_SecretScannerTests
    {
        [Fact]
        public async Task NoDiagnostic_PlainString()
        {
            var source = @"
class C {
    string name = ""hello world"";
}";
            await AnalyzerVerifier<SecretScannerAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_AwsAccessKey()
        {
            var source = @"
class C {
    string key = ""AKIAIOSFODNN7ABCDEFG"";
}";
            // `"AKIAIOSFODNN7ABCDEFG"` string literal on line 3
            // "    string key = " = 17 chars -> `"` at col 18
            var expected = new DiagnosticResult("DSSEC", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(3, 18)
                .WithArguments("AWS access key");

            await AnalyzerVerifier<SecretScannerAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_GitHubPat()
        {
            var source = @"
class C {
    string token = ""ghp_abcdefghijklmnopqrstuvwxyz1234567890"";
}";
            // `"ghp_..."` string on line 3
            // "    string token = " = 19 chars -> `"` at col 20
            var expected = new DiagnosticResult("DSSEC", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(3, 20)
                .WithArguments("GitHub PAT");

            await AnalyzerVerifier<SecretScannerAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_PasswordAssignment()
        {
            var source = @"
class C {
    void M() {
        var pw = ""password=\""mysecret123\"""";
    }
}";
            // String value: password="mysecret123" matches (?i)(password|passwd|pwd)\s*=\s*["'][^"']{6,}
            // `"password=..."` string literal on line 4
            // "        var pw = " = 17 chars -> `"` at col 18
            var expected = new DiagnosticResult("DSSEC", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(4, 18)
                .WithArguments("password");

            await AnalyzerVerifier<SecretScannerAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
