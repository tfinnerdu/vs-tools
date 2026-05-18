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
    void M() { var cs = {|DS003:""Server=myserver;Database=mydb;Trusted_Connection=True;""}; }
}";
            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerError("DS003").WithNoLocation());
        }

        [Fact]
        public async Task Diagnostic_PasswordEquals_CaseInsensitive()
        {
            var source = @"
class C {
    void M() { var cs = {|DS003:""password=supersecret123""}; }
}";
            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerError("DS003").WithNoLocation());
        }

        [Fact]
        public async Task Diagnostic_DataSource()
        {
            var source = @"
class C {
    string ConnStr = {|DS003:""Data Source=localhost;Initial Catalog=MyDb;""|};
}";
            await AnalyzerVerifier<DS003_HardcodedConnectionStringAnalyzer>.VerifyAnalyzerAsync(source,
                DiagnosticResult.CompilerError("DS003").WithNoLocation());
        }
    }
}
