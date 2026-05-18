using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA009_AsyncVoidFixTests
{
    [Fact]
    public async Task CodeFix_ChangesVoidToTask()
    {
        // Analyzer fires at the method identifier; DA009 has Warning severity.
        // Line 3: "    async void DoWork() {"
        // "    async void " = 15 chars → 'D' in DoWork at col 16
        var before = @"
class C {
    async void DoWork() {
    }
}";
        var expected = new DiagnosticResult("DA009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(3, 16)
            .WithArguments("DoWork");

        // The fix adds 'using System.Threading.Tasks;' and changes 'void' to 'Task'.
        // AddUsings appends to the end of existing usings; with none present it goes first.
        var after = @"using System.Threading.Tasks;

class C {
    async Task DoWork() {
    }
}";
        await CodeFixVerifier<DA009_AsyncVoidAnalyzer, DA009_AsyncVoidFix>
            .VerifyCodeFixAsync(before, expected, after);
    }
}
