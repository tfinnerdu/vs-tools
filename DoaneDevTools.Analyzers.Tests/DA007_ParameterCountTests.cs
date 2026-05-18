using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA007_ParameterCountTests
{
    [Fact]
    public async Task NoDiagnostic_FiveParameters()
    {
        var source = @"
class C {
    void Method(int a, int b, int c, int d, int e) {}
}";
        await AnalyzerVerifier<DA007_ParameterCountAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_SixParameters()
    {
        var source = @"
class C {
    void Method(int a, int b, int c, int d, int e, int f) {}
}";
        var expected = new DiagnosticResult("DA007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
            .WithLocation(3, 10)
            .WithArguments("Method", 6);

        await AnalyzerVerifier<DA007_ParameterCountAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoDiagnostic_FourParameters()
    {
        var source = @"
class C {
    void Method(int a, int b, int c, int d) {}
}";
        await AnalyzerVerifier<DA007_ParameterCountAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
