using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DS002_SqlConnectionTests
    {
        // Stub the SqlConnection type so the semantic model can resolve it
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
            var source = SqlConnectionStub + @"
class C {
    void M() {
        var conn = new {|DS002:System.Data.SqlClient.SqlConnection(""Server=x"")|};
    }
}";
            await AnalyzerVerifier<DS002_SqlConnectionAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerWarning("DS002").WithNoLocation());
        }
    }
}
