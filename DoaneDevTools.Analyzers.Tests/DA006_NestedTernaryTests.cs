using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA006_NestedTernaryTests
{
    [Fact]
    public async Task NoDiagnostic_SingleTernary()
    {
        var source = @"
class C {
    int M(bool a) {
        return a ? 1 : 0;
    }
}";
        await AnalyzerVerifier<DA006_NestedTernaryAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_NestedTernary()
    {
        var source = @"
class C {
    int M(bool a, bool b) {
        return a ? (b ? 1 : 2) : 3;
    }
}";
        var expected = new DiagnosticResult("DA006", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(3, 21);

        await AnalyzerVerifier<DA006_NestedTernaryAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
