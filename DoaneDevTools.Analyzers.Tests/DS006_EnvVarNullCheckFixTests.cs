using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS006_EnvVarNullCheckFixTests
{
    [Fact]
    public async Task CodeFix_AddsNullGuard()
    {
        // DS006 fires at the invocation expression (Environment.GetEnvironmentVariable(...)).
        // Line 5: "        var val = Environment.GetEnvironmentVariable(""MY_VAR"");"
        // "        var val = " = 18 chars → 'E' in Environment at col 19
        var before = @"
using System;
class C {
    void M() {
        var val = Environment.GetEnvironmentVariable(""MY_VAR"");
    }
}";
        var expected = new DiagnosticResult("DS006", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(5, 19);

        // The fix wraps the invocation in a null-coalescing throw expression.
        // DS006_EnvVarNullCheckFix builds:
        //   Environment.GetEnvironmentVariable("MY_VAR")
        //   ?? throw new InvalidOperationException($"Required environment variable 'MY_VAR' is not set")
        var after = @"
using System;
class C {
    void M() {
        var val = Environment.GetEnvironmentVariable(""MY_VAR"") ?? throw new InvalidOperationException($""Required environment variable 'MY_VAR' is not set"");
    }
}";
        await CodeFixVerifier<DS006_EnvVarNullCheckAnalyzer, DS006_EnvVarNullCheckFix>
            .VerifyCodeFixAsync(before, expected, after);
    }
}
