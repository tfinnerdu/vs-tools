using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA003_ClassSizeTests
{
    [Fact]
    public async Task NoDiagnostic_TenPublicMethods()
    {
        var source = @"
class C {
    public void M1() {}
    public void M2() {}
    public void M3() {}
    public void M4() {}
    public void M5() {}
    public void M6() {}
    public void M7() {}
    public void M8() {}
    public void M9() {}
    public void M10() {}
}";
        await AnalyzerVerifier<DA003_ClassSizeAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_ElevenPublicMethods()
    {
        var source = @"
class BigClass {
    public void M1() {}
    public void M2() {}
    public void M3() {}
    public void M4() {}
    public void M5() {}
    public void M6() {}
    public void M7() {}
    public void M8() {}
    public void M9() {}
    public void M10() {}
    public void M11() {}
}";
        var expected = new DiagnosticResult("DA003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(2, 7)
            .WithArguments("BigClass", 11);

        await AnalyzerVerifier<DA003_ClassSizeAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoDiagnostic_PrivateMethodsIgnored()
    {
        var source = @"
class C {
    private void M1() {}
    private void M2() {}
    private void M3() {}
    private void M4() {}
    private void M5() {}
    private void M6() {}
    private void M7() {}
    private void M8() {}
    private void M9() {}
    private void M10() {}
    private void M11() {}
    private void M12() {}
    private void M13() {}
    private void M14() {}
    private void M15() {}
}";
        await AnalyzerVerifier<DA003_ClassSizeAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
