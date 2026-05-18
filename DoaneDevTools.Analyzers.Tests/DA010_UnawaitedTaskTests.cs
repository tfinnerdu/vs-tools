using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA010_UnawaitedTaskTests
    {
        [Fact]
        public async Task NoDiagnostic_AwaitedTask()
        {
            var source = @"
using System.Threading.Tasks;
class C {
    async Task M() {
        await DoWorkAsync();
    }
    Task DoWorkAsync() => Task.CompletedTask;
}";
            await AnalyzerVerifier<DA010_UnawaitedTaskAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_AssignedTask()
        {
            var source = @"
using System.Threading.Tasks;
class C {
    void M() {
        var t = DoWorkAsync();
    }
    Task DoWorkAsync() => Task.CompletedTask;
}";
            await AnalyzerVerifier<DA010_UnawaitedTaskAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_Discard()
        {
            var source = @"
using System.Threading.Tasks;
class C {
    void M() {
        _ = DoWorkAsync();
    }
    Task DoWorkAsync() => Task.CompletedTask;
}";
            await AnalyzerVerifier<DA010_UnawaitedTaskAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_BareUnawaitedTask()
        {
            var source = @"
using System.Threading.Tasks;
class C {
    async Task M() {
        DoWorkAsync();
    }
    Task DoWorkAsync() => Task.CompletedTask;
}";
            // `DoWorkAsync()` invocation on line 5, col 9 (8 spaces + `D`)
            var expected = new DiagnosticResult("DA010", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(5, 9)
                .WithArguments("DoWorkAsync");

            await AnalyzerVerifier<DA010_UnawaitedTaskAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
