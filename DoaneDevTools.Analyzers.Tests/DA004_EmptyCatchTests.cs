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
            // Diagnostic fires at the `catch` keyword; DA004 has Error severity
            var source = @"
class C {
    void M() {
        try { int x = 1; }
        catch { }
    }
}";
            var expected = new DiagnosticResult("DA004", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(5, 9);

            await AnalyzerVerifier<DA004_EmptyCatchAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_CatchWithOnlyComment()
        {
            // Comments are trivia — parser produces no Statement nodes → block is empty
            var source = @"
class C {
    void M() {
        try { int x = 1; }
        catch {
            // swallowed intentionally
        }
    }
}";
            var expected = new DiagnosticResult("DA004", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(5, 9);

            await AnalyzerVerifier<DA004_EmptyCatchAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task CodeFix_AddsLoggingStub()
        {
            // The fix inserts a TODO comment and _logger?.LogError(...) call
            var before = @"
class C {
    void MyMethod() {
        try { int x = 1; }
        catch { }
    }
}";
            // Expected diagnostic at the `catch` keyword
            var expected = new DiagnosticResult("DA004", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(5, 9);

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
                .VerifyCodeFixAsync(before, expected, after);
        }
    }
}
