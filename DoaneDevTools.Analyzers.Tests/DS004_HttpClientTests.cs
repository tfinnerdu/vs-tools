using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DS004_HttpClientTests
    {
        [Fact]
        public async Task NoDiagnostic_InjectedClient()
        {
            var source = @"
using System.Net.Http;
class C {
    private readonly HttpClient _client;
    C(HttpClient client) { _client = client; }
}";
            await AnalyzerVerifier<DS004_HttpClientAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_DirectInstantiation()
        {
            var source = @"
using System.Net.Http;
class C {
    void M() {
        var client = new HttpClient();
    }
}";
            // `new HttpClient()` starts at `n` in `new` on line 5
            // "        var client = " = 21 chars -> `n` at col 22
            var expected = new DiagnosticResult("DS004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(5, 22);

            await AnalyzerVerifier<DS004_HttpClientAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
