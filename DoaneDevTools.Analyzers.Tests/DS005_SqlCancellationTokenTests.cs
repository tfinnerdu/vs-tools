using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS005_SqlCancellationTokenTests
{
    private const string SqlCommandStub = @"
namespace System.Data.SqlClient {
    public class SqlCommand {
        public System.Threading.Tasks.Task ExecuteReaderAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task ExecuteReaderAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task ExecuteNonQueryAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task ExecuteNonQueryAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
    }
}";

    [Fact]
    public async Task NoDiagnostic_MethodHasCancellationToken()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
class C {
    async Task M(CancellationToken ct) {
        var cmd = new System.Data.SqlClient.SqlCommand();
        await cmd.ExecuteReaderAsync(ct);
    }
}" + SqlCommandStub;

        await AnalyzerVerifier<DS005_SqlCancellationTokenAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_NoCancellationToken()
    {
        // Line 1: blank
        // Line 2: using System.Threading.Tasks;
        // Line 3: class C {
        // Line 4:     async Task M() {
        // Line 5:         var cmd = new System.Data.SqlClient.SqlCommand();
        // Line 6:         await cmd.ExecuteReaderAsync();
        //
        // "        await " = 14 chars → 'c' in cmd at col 15
        var source = @"
using System.Threading.Tasks;
class C {
    async Task M() {
        var cmd = new System.Data.SqlClient.SqlCommand();
        await cmd.ExecuteReaderAsync();
    }
}" + SqlCommandStub;

        var expected = new DiagnosticResult("DS005", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
            .WithLocation(6, 15)
            .WithArguments("ExecuteReaderAsync");

        await AnalyzerVerifier<DS005_SqlCancellationTokenAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
