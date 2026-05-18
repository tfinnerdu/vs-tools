using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA005_MagicNumberTests
{
    [Fact]
    public async Task NoDiagnostic_AllowedZero()
    {
        var source = @"
class C {
    void M() {
        int x = 0;
    }
}";
        await AnalyzerVerifier<DA005_MagicNumberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoDiagnostic_ConstantDeclaration()
    {
        var source = @"
class C {
    void M() {
        const int Max = 42;
    }
}";
        await AnalyzerVerifier<DA005_MagicNumberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoDiagnostic_AllowedValues()
    {
        var source = @"
class C {
    void M() {
        int a = 1; int b = 2;
    }
}";
        await AnalyzerVerifier<DA005_MagicNumberAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_MagicNumber42()
    {
        var source = @"
class C {
    void M() {
        int x = 42;
    }
}";
        var expected = new DiagnosticResult("DA005", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(4, 17)
            .WithArguments("42");

        await AnalyzerVerifier<DA005_MagicNumberAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
