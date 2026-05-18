using System.Threading.Tasks;
using DoaneDevTools.Analyzers.CodeFixes;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS003_HardcodedConnectionStringFixTests
{
    [Fact]
    public async Task CodeFix_ReplacesWithEnvVar()
    {
        // DS003 fires at the string literal containing a connection string pattern.
        // Line 4: "        var cs = ""Server=localhost;Password=secret123"";"
        // "        var cs = " = 17 chars → '"' at col 18
        var before = @"
class C {
    void M() {
        var cs = ""Server=localhost;Password=secret123"";
    }
}";
        var expected = new DiagnosticResult("DS003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(4, 18);

        // The fix calls SyntaxFactory.ParseExpression and prepends a TODO comment as
        // leading trivia. The comment replaces the leading space before the literal, and
        // a \r\n + the original space indent follows it before the replacement expression.
        var after = @"
class C {
    void M() {
        var cs =// TODO: set CONNECTION_STRING in your .env file
 Environment.GetEnvironmentVariable(""CONNECTION_STRING"") ?? throw new InvalidOperationException(""CONNECTION_STRING environment variable is not set"");
    }
}";
        await CodeFixVerifier<DS003_HardcodedConnectionStringAnalyzer, DS003_HardcodedConnectionStringFix>
            .VerifyCodeFixAsync(before, expected, after);
    }
}
