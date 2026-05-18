using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests
{
    public class DA012_MissingXmlDocTests
    {
        [Fact]
        public async Task NoDiagnostic_DocumentedClass()
        {
            var source = @"
/// <summary>Documented.</summary>
public class Foo {}
";
            await AnalyzerVerifier<DA012_MissingXmlDocAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoDiagnostic_InternalClass()
        {
            var source = @"
class InternalClass {}
";
            await AnalyzerVerifier<DA012_MissingXmlDocAnalyzer>.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task Diagnostic_UndocumentedPublicClass()
        {
            var source = @"
public class Foo {}";
            // `Foo` identifier at line 2, col 14 ("public class " = 13 chars -> col 14)
            var expected = new DiagnosticResult("DA012", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(2, 14)
                .WithArguments("class", "Foo");

            await AnalyzerVerifier<DA012_MissingXmlDocAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task Diagnostic_UndocumentedPublicMethod()
        {
            var source = @"
public class C {
    public void Bar() {}
}";
            // `Bar` at line 3, col 17 ("    public void " = 16 chars -> col 17)
            var expected = new DiagnosticResult("DA012", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                .WithLocation(3, 17)
                .WithArguments("method", "Bar");

            await AnalyzerVerifier<DA012_MissingXmlDocAnalyzer>.VerifyAnalyzerAsync(source, expected);
        }
    }
}
