using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA001_ComplexityTests
    {
        [Fact]
        public async Task NoDiagnostic_SimpleMethod()
        {
            var source = @"
class C {
    int M(int x) {
        if (x > 0) return 1;
        if (x < 0) return -1;
        return 0;
    }
}";
            await AnalyzerVerifier<DA001_ComplexityAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_HighComplexity()
        {
            // 16 if-branches → CC = 17, which exceeds the threshold of 15
            var source = @"
class C {
    int HighCC(int x) {
        if (x == 1) return 1;
        if (x == 2) return 2;
        if (x == 3) return 3;
        if (x == 4) return 4;
        if (x == 5) return 5;
        if (x == 6) return 6;
        if (x == 7) return 7;
        if (x == 8) return 8;
        if (x == 9) return 9;
        if (x == 10) return 10;
        if (x == 11) return 11;
        if (x == 12) return 12;
        if (x == 13) return 13;
        if (x == 14) return 14;
        if (x == 15) return 15;
        if (x == 16) return 16;
        return 0;
    }
}";
            // Diagnostic at the method identifier "HighCC" on line 3, col 9
            var expected = new DiagnosticResult("DA001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(3, 9)
                .WithArguments("HighCC", 17);

            await AnalyzerVerifier<DA001_ComplexityAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
