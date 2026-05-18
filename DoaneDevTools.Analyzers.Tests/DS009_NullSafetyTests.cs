using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS009_NullSafetyTests
{
    [Fact]
    public async Task NoDiagnostic_ConditionalAccess()
    {
        // Using ?. — safe access, no diagnostic
        var source = @"
using System.Linq;
using System.Collections.Generic;
class C {
    void M() {
        var items = new List<string>();
        var len = items.FirstOrDefault()?.Length;
    }
}";

        await AnalyzerVerifier<NullSafetyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NoDiagnostic_WithNullCheck()
    {
        // The result is stored and null-checked before use
        var source = @"
using System.Linq;
using System.Collections.Generic;
class C {
    void M() {
        var items = new List<string>();
        var first = items.FirstOrDefault();
        if (first != null) {
            var len = first.Length;
        }
    }
}";

        await AnalyzerVerifier<NullSafetyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_DirectMemberAccessAfterFirstOrDefault()
    {
        // items.FirstOrDefault().Length — outer MemberAccess starts at 'items'
        // Line 7: "        var len = items.FirstOrDefault().Length;"
        // "        var len = " = 18 chars → 'i' at col 19
        var source = @"
using System.Linq;
using System.Collections.Generic;
class C {
    void M() {
        var items = new List<string>();
        var len = items.FirstOrDefault().Length;
    }
}";

        var expected = new DiagnosticResult("DS009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(7, 19);

        await AnalyzerVerifier<NullSafetyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task Diagnostic_DirectMemberAccessAfterSingleOrDefault()
    {
        // items.SingleOrDefault().Length — same pattern, fires DS009b
        // Line 7, col 19 for 'items'
        var source = @"
using System.Linq;
using System.Collections.Generic;
class C {
    void M() {
        var items = new List<string>();
        var len = items.SingleOrDefault().Length;
    }
}";

        var expected = new DiagnosticResult("DS009b", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(7, 19);

        await AnalyzerVerifier<NullSafetyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
