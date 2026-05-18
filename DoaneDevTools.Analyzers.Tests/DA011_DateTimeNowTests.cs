using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA011_DateTimeNowTests
    {
        [Fact]
        public async Task NoDiagnostic_DateTimeUtcNow()
        {
            var source = @"
using System;
class C {
    void M() { var dt = DateTime.UtcNow; }
}";
            await AnalyzerVerifier<DA011_DateTimeNowAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_DateTimeNow()
        {
            var source = @"
using System;
class C {
    void M() {
        var dt = DateTime.Now;
    }
}";
            // `DateTime.Now` member access starts at `D` on line 5
            // "        var dt = " = 17 chars -> `D` at col 18
            var expected = new DiagnosticResult("DA011", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(5, 18);

            await AnalyzerVerifier<DA011_DateTimeNowAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
