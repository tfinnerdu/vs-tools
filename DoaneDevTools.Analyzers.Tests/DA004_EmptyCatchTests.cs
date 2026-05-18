using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA004_EmptyCatchTests
    {
        private const string Id = "DA004";

        [Fact]
        public async Task NoDiagnostic_CatchWithStatement()
        {
            var source = @"
using System;
class C {
    void M() {
        try { int x = 1; }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }
}";
            await AnalyzerVerifier<DA004_EmptyCatchAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_EmptyCatchBlock()
        {
            var source = @"
class C {
    void M() {
        try { int x = 1; }
        {|DA004:catch|} { }
    }
}";
            await AnalyzerVerifier<DA004_EmptyCatchAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerWarning(Id).WithNoLocation());
        }

        [Fact]
        public async Task NoDiagnostic_AsyncTaskMethod()
        {
            var source = @"
using System;
class C {
    void M() {
        try { throw new Exception(); }
        catch (Exception ex) {
            _ = ex.Message;
            throw;
        }
    }
}";
            await AnalyzerVerifier<DA004_EmptyCatchAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task CodeFix_AddsLoggingStub()
        {
            var before = @"
class C {
    void MyMethod() {
        try { int x = 1; }
        {|DA004:catch|} { }
    }
}";
            var after = @"
class C {
    void MyMethod() {
        try { int x = 1; }
        catch {
            // TODO: handle exception
            _logger?.LogError(ex, ""Unhandled exception in {Method}"", nameof(MyMethod));
        }
    }
}";
            await CodeFixVerifier<DA004_EmptyCatchAnalyzer, DA004_EmptyCatchFix>
                .VerifyCodeFixAsync(before, after);
        }
    }
}
