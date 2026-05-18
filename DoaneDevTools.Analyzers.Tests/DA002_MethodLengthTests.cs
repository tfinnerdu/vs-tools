using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DA002_MethodLengthTests
{
    [Fact]
    public async Task NoDiagnostic_ShortMethod()
    {
        var source = @"
class C {
    void Short() {
        int a0 = 0; int a1 = 1; int a2 = 2; int a3 = 3; int a4 = 4;
        int a5 = 5; int a6 = 6; int a7 = 7; int a8 = 8; int a9 = 9;
    }
}";
        await AnalyzerVerifier<DA002_MethodLengthAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_LongMethod()
    {
        var source = @"
class C {
    void LongMethod() {
        int a0 = 0;
        int a1 = 1;
        int a2 = 2;
        int a3 = 3;
        int a4 = 4;
        int a5 = 5;
        int a6 = 6;
        int a7 = 7;
        int a8 = 8;
        int a9 = 9;
        int a10 = 10;
        int a11 = 11;
        int a12 = 12;
        int a13 = 13;
        int a14 = 14;
        int a15 = 15;
        int a16 = 16;
        int a17 = 17;
        int a18 = 18;
        int a19 = 19;
        int a20 = 20;
        int a21 = 21;
        int a22 = 22;
        int a23 = 23;
        int a24 = 24;
        int a25 = 25;
        int a26 = 26;
        int a27 = 27;
        int a28 = 28;
        int a29 = 29;
        int a30 = 30;
        int a31 = 31;
        int a32 = 32;
        int a33 = 33;
        int a34 = 34;
        int a35 = 35;
        int a36 = 36;
        int a37 = 37;
        int a38 = 38;
        int a39 = 39;
        int a40 = 40;
        int a41 = 41;
        int a42 = 42;
        int a43 = 43;
        int a44 = 44;
        int a45 = 45;
        int a46 = 46;
        int a47 = 47;
        int a48 = 48;
        int a49 = 49;
        int a50 = 50;
    }
}";
        var expected = new DiagnosticResult("DA002", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(3, 10)
            .WithArguments("LongMethod", 51);

        await AnalyzerVerifier<DA002_MethodLengthAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }
}
