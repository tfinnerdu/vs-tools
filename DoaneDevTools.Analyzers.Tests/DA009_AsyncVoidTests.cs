using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA009_AsyncVoidTests
    {
        [Fact]
        public async Task NoDiagnostic_AsyncTask()
        {
            var source = @"
using System.Threading.Tasks;
class C {
    public async Task FooAsync() { await Task.CompletedTask; }
}";
            await AnalyzerVerifier<DA009_AsyncVoidAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_EventHandler_SenderArgs()
        {
            var source = @"
using System;
class C {
    async void Button_Click(object sender, EventArgs e) { await System.Threading.Tasks.Task.CompletedTask; }
}";
            await AnalyzerVerifier<DA009_AsyncVoidAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_OnNamed()
        {
            var source = @"
class C {
    async void OnLoaded() { await System.Threading.Tasks.Task.CompletedTask; }
}";
            await AnalyzerVerifier<DA009_AsyncVoidAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_AsyncVoidMethod()
        {
            var source = @"
class C {
    public async void DoWork() { await System.Threading.Tasks.Task.CompletedTask; }
}";
            // Diagnostic at the method identifier "DoWork" (line 3, col 23)
            var expected = new DiagnosticResult("DA009", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(3, 23)
                .WithArguments("DoWork");

            await AnalyzerVerifier<DA009_AsyncVoidAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task NoDiagnostic_NonAsyncVoid()
        {
            var source = @"
class C {
    public void DoWork() { }
}";
            await AnalyzerVerifier<DA009_AsyncVoidAnalyzer>.VerifyAnalyzerAsync(source);
        }
    }
}
