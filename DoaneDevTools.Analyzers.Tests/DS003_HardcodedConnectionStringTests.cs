using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DS003_HardcodedConnectionStringTests
    {
        [Fact]
        public async Task NoDiagnostic_PlainString()
        {
            var source = @"
class C {
    void M() { var s = ""hello world""; }
}";
            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_EnvVarLookup()
        {
            var source = @"
using System;
class C {
    void M() { var cs = Environment.GetEnvironmentVariable(""CONNECTION_STRING""); }
}";
            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_ServerEquals()
        {
            var source = @"
class C {
    void M() { var cs = ""Server=myserver;Database=mydb;Trusted_Connection=True;""; }
}";
            // String literal starts at col 25 (after `var cs = `)
            var expected = new DiagnosticResult("DS003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(3, 25);

            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_PasswordEquals_CaseInsensitive()
        {
            var source = @"
class C {
    void M() { var cs = ""password=supersecret123""; }
}";
            var expected = new DiagnosticResult("DS003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(3, 25);

            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_DataSource()
        {
            var source = @"
class C {
    string ConnStr = ""Data Source=localhost;Initial Catalog=MyDb;"";
}";
            // String literal in field initializer
            var expected = new DiagnosticResult("DS003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .WithLocation(3, 22);

            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
