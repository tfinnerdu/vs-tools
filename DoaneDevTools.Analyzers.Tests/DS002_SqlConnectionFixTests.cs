using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS002_SqlConnectionFixTests
{
    private const string SqlConnectionStub = @"
namespace System.Data.SqlClient {
    public class SqlConnection : System.IDisposable {
        public SqlConnection(string cs) {}
        public void Dispose() {}
    }
}";

    [Fact]
    public async Task CodeFix_WrapsInUsing()
    {
        // The analyzer fires at objectCreation.GetLocation() — the 'new' keyword.
        // Stub (7 lines: blank + namespace + class + ctor + Dispose + } + }) ends at line 7.
        // Combined source:
        //   Line 1:  (blank from stub leading \n)
        //   Lines 2-7: namespace stub content
        //   Line 8:  class C {
        //   Line 9:      void M() {
        //   Line 10:         var conn = new SqlConnection("Server=test");
        //   Line 11:     }
        //   Line 12: }
        //
        // Line 10: "        var conn = new SqlConnection(...);"
        // "        var conn = " = 19 chars → 'n' in new at col 20
        var before = SqlConnectionStub + @"
class C {
    void M() {
        var conn = new SqlConnection(""Server=test"");
    }
}";
        var expected = new DiagnosticResult("DS002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(10, 20);

        var after = SqlConnectionStub + @"
class C {
    void M() {
        using var conn = new SqlConnection(""Server=test"");
    }
}";
        await CodeFixVerifier<DS002_SqlConnectionAnalyzer, DS002_SqlConnectionFix>
            .VerifyCodeFixAsync(before, expected, after);
    }
}
