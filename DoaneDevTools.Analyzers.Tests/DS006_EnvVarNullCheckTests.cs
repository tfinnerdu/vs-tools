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
            // GetEnvironmentVariable result stored in a local with no guard — fires at the call site
            var source = @"
using System;
class C {
    void M() {
        string v = {|DS006:Environment.GetEnvironmentVariable(""X"")|};
    }
}";
            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerWarning("DS006").WithNoLocation());
        }

        [Fact]
        public async Task Diagnostic_PassedDirectlyToMethod()
        {
            var source = @"
using System;
class C {
    void M() {
        Console.WriteLine({|DS006:Environment.GetEnvironmentVariable(""KEY"")|});
    }
}";
            await AnalyzerVerifier<DS006_EnvVarNullCheckAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerWarning("DS006").WithNoLocation());
        }
    }
}
