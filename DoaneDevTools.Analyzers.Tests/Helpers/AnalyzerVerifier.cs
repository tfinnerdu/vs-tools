using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;

namespace DoaneDevTools.Analyzers.Tests.Helpers
{
    // Thin re-export so test classes can write:
    //   AnalyzerVerifier<MyAnalyzer>.VerifyAnalyzerAsync(source, expected)
    // without importing the long Microsoft.CodeAnalysis.CSharp.Testing.XUnit namespace.
    public static class AnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
            => Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<TAnalyzer>
                .VerifyAnalyzerAsync(source, expected);
    }
}
