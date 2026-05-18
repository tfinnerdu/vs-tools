using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA008_StringFormatTests
    {
        [Fact]
        public async Task NoDiagnostic_StringInterpolation()
        {
            var source = @"
class C {
    void M() {
        var s = $""hello {""world""}"";
    }
}";
            await AnalyzerVerifier<DA008_StringFormatAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_NonFormatCall()
        {
            var source = @"
class C {
    void M() {
        Console.WriteLine(""hello"");
    }
}";
            await AnalyzerVerifier<DA008_StringFormatAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_StringFormat()
        {
            var source = @"
class C {
    void M(string name) {
        var s = string.Format(""hello {0}"", name);
    }
}";
            // `string.Format(...)` invocation starts at `s` in `string` on line 4
            // "        var s = " = 16 chars -> `s` at col 17
            var expected = new DiagnosticResult("DA008", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(4, 17);

            await AnalyzerVerifier<DA008_StringFormatAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task CodeFix_ConvertsToInterpolation()
        {
            var before = @"
class C {
    void M(string name) {
        var s = string.Format(""hello {0}"", name);
    }
}";
            var expected = new DiagnosticResult("DA008", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(4, 17);

            var after = @"
class C {
    void M(string name) {
        var s = $""hello {name}"";
    }
}";
            await CodeFixVerifier<DA008_StringFormatAnalyzer, DA008_StringFormatFix>
                .VerifyCodeFixAsync(before, expected, after);
        }
    }
}
