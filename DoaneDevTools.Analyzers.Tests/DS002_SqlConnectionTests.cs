using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DS002_SqlConnectionTests
    {
        // Stub the SqlConnection type so the semantic model can resolve it to the expected namespace
        private const string SqlConnectionStub = @"
namespace System.Data.SqlClient {
    public class SqlConnection : System.IDisposable {
        public SqlConnection(string cs) { }
        public void Dispose() { }
    }
}";

        [Fact]
        public async Task NoDiagnostic_UsingDeclaration()
        {
            var source = SqlConnectionStub + @"
class C {
    void M() {
        using var conn = new System.Data.SqlClient.SqlConnection(""Server=x"");
    }
}";
            await AnalyzerVerifier<DS002_SqlConnectionAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_UsingStatement()
        {
            var source = SqlConnectionStub + @"
class C {
    void M() {
        using (var conn = new System.Data.SqlClient.SqlConnection(""Server=x"")) { }
    }
}";
            await AnalyzerVerifier<DS002_SqlConnectionAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_NotWrappedInUsing()
        {
            // SqlConnection stub is 7 lines of content; combined source puts the class at line 9
            // The object creation at line 12, col 20 (at the `new` keyword)
            var source = SqlConnectionStub + @"
class C {
    void M() {
        var conn = new System.Data.SqlClient.SqlConnection(""Server=x"");
    }
}";
            // Combined source: stub is lines 1-7, class C at line 8, void M at line 9,
            // var conn = new SqlConnection at line 10, col 20 (at the `new` keyword)
            var expected = new DiagnosticResult("DS002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .WithLocation(10, 20);

            await AnalyzerVerifier<DS002_SqlConnectionAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
