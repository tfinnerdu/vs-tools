using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

/// <summary>
/// Tests for SqlEmbeddedAnalyzer (DS010/DS010b/DS010c/DS010d).
/// The analyzer fires on cmd.CommandText = "..." assignments, not on plain local variable assignments.
/// </summary>
public class DS010_SqlEmbeddedTests
{
    // Minimal SqlCommand stub so the file compiles
    private const string SqlCommandStub = @"
namespace System.Data.SqlClient {
    public class SqlCommand {
        public string CommandText { get; set; }
    }
}";

    [Fact]
    public async Task NoDiagnostic_SafeSelectWithColumns()
    {
        // Safe SQL: explicit columns and WHERE clause — no diagnostics
        var source = @"
class C {
    void M() {
        var cmd = new System.Data.SqlClient.SqlCommand();
        cmd.CommandText = ""SELECT Id, Name FROM Users WHERE Id = @id"";
    }
}" + SqlCommandStub;

        await AnalyzerVerifier<SqlEmbeddedAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_SelectStar()
    {
        // cmd.CommandText = "SELECT * FROM Users" — fires DS010
        // Line 1: blank
        // Line 2: class C {
        // Line 3:     void M() {
        // Line 4:         var cmd = ...
        // Line 5:         cmd.CommandText = "SELECT * FROM Users";
        // "        cmd.CommandText = " = 26 chars → '"' at col 27
        var source = @"
class C {
    void M() {
        var cmd = new System.Data.SqlClient.SqlCommand();
        cmd.CommandText = ""SELECT * FROM Users"";
    }
}" + SqlCommandStub;

        var expected = new DiagnosticResult("DS010", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(5, 27);

        await AnalyzerVerifier<SqlEmbeddedAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task Diagnostic_NoLock()
    {
        // NOLOCK hint — fires DS010b
        // Line 5, "        cmd.CommandText = " = 26 chars → '"' at col 27
        var source = @"
class C {
    void M() {
        var cmd = new System.Data.SqlClient.SqlCommand();
        cmd.CommandText = ""SELECT Id FROM Users WITH (NOLOCK)"";
    }
}" + SqlCommandStub;

        var expected = new DiagnosticResult("DS010b", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(5, 27);

        await AnalyzerVerifier<SqlEmbeddedAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task Diagnostic_DynamicSqlConcatenation()
    {
        // cmd.CommandText = "SELECT " + col + " FROM Users" — fires DS010c
        // AnalyzeAssignment sees BinaryExpressionSyntax on Right → reports assignment.Right.GetLocation()
        // Line 5: "        cmd.CommandText = "SELECT " + col + " FROM Users";"
        // assignment.Right starts at '"SELECT "' — after "        cmd.CommandText = " (26 chars) → col 27
        var source = @"
class C {
    void M(string col) {
        var cmd = new System.Data.SqlClient.SqlCommand();
        cmd.CommandText = ""SELECT "" + col + "" FROM Users"";
    }
}" + SqlCommandStub;

        var expected = new DiagnosticResult("DS010c", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(5, 27);

        await AnalyzerVerifier<SqlEmbeddedAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoDiagnostic_DeleteWithWhereClause()
    {
        // Has WHERE — no DS010d diagnostic
        var source = @"
class C {
    void M() {
        var cmd = new System.Data.SqlClient.SqlCommand();
        cmd.CommandText = ""DELETE FROM Users WHERE Id = @id"";
    }
}" + SqlCommandStub;

        await AnalyzerVerifier<SqlEmbeddedAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
