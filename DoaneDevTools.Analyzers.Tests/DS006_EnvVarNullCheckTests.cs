using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DS006_EnvVarNullCheckTests
    {
        [Fact]
        public async Task NoDiagnostic_NullCoalesce()
        {
            var source = @"
using System;
class C {
    void M() { var v = Environment.GetEnvironmentVariable(""X"") ?? ""default""; }
}";
            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_ConditionalAccess()
        {
            var source = @"
using System;
class C {
    void M() { var len = Environment.GetEnvironmentVariable(""X"")?.Length; }
}";
            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_InsideNullCheckIf()
        {
            var source = @"
using System;
class C {
    void M() {
        if (Environment.GetEnvironmentVariable(""X"") != null) { }
    }
}";
            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_BareLocalDeclaration()
        {
            // GetEnvironmentVariable result stored in a local with no null guard
            var source = @"
using System;
class C {
    void M() {
        string v = Environment.GetEnvironmentVariable(""X"");
    }
}";
            // The invocation starts at "Environment.GetEnvironmentVariable" — line 5, col 20
            var expected = new DiagnosticResult("DS006", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(5, 20);

            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_PassedDirectlyToMethod()
        {
            var source = @"
using System;
class C {
    void M() {
        Console.WriteLine(Environment.GetEnvironmentVariable(""KEY""));
    }
}";
            // Invocation at line 5, col 27
            var expected = new DiagnosticResult("DS006", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(5, 27);

            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
