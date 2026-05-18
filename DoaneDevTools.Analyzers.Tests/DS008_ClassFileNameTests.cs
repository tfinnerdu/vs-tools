using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS008_ClassFileNameTests
{
    [Fact]
    public async Task NoDiagnostic_ClassMatchesFileName()
    {
        // In the Roslyn testing framework the default file name is "Test0.cs"
        // so "Test0" matches "Test0" — no diagnostic
        var source = @"class Test0 {}";

        await AnalyzerVerifier<DS008_ClassFileNameAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_ClassDoesNotMatchFileName()
    {
        // "WrongName" != "Test0" (the default test file name)
        // Identifier at line 1, col 7 ("class " = 6 chars)
        var source = @"class WrongName {}";

        var expected = new DiagnosticResult("DS008", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
            .WithLocation(1, 7)
            .WithArguments("WrongName", "Test0.cs");

        await AnalyzerVerifier<DS008_ClassFileNameAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoDiagnostic_NestedClassIgnored()
    {
        // The top-level class Test0 matches the file name.
        // The nested class Inner is skipped by the analyzer.
        var source = @"
class Test0 {
    class Inner {}
}";

        await AnalyzerVerifier<DS008_ClassFileNameAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
