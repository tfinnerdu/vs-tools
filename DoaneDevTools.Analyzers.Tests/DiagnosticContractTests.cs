using System.Linq;
using System.Text.RegularExpressions;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

/// <summary>
/// Hardcoded contract tests for every Roslyn analyzer descriptor.
/// These tests always pass when contracts are honored and fail the moment someone changes
/// an ID, severity, or message format — catching accidental breakage of known-good behavior.
/// No Roslyn compilation is required; each test simply inspects SupportedDiagnostics.
/// </summary>
public class DiagnosticContractTests
{
    // Count the highest format-placeholder index + 1  (e.g. "'{0}' has {1}" → 2)
    private static int ArgCount(string format)
    {
        int max = -1;
        foreach (Match m in Regex.Matches(format, @"\{(\d+)"))
            if (int.TryParse(m.Groups[1].Value, out int i) && i > max)
                max = i;
        return max + 1;
    }

    // ─── DA (code-quality) rules ───────────────────────────────────────────────

    [Fact]
    public void DA001_Complexity_Descriptor()
    {
        var d = Assert.Single(new DA001_ComplexityAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA001", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
        Assert.Contains("threshold: 15", d.MessageFormat.ToString());
    }

    [Fact]
    public void DA002_MethodLength_Descriptor()
    {
        var d = Assert.Single(new DA002_MethodLengthAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA002", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
        Assert.Contains("threshold: 50", d.MessageFormat.ToString());
    }

    [Fact]
    public void DA003_ClassSize_Descriptor()
    {
        var d = Assert.Single(new DA003_ClassSizeAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA003", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
        Assert.Contains("threshold: 10", d.MessageFormat.ToString());
    }

    [Fact]
    public void DA004_EmptyCatch_Descriptor()
    {
        var d = Assert.Single(new DA004_EmptyCatchAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA004", d.Id);
        Assert.Equal(DiagnosticSeverity.Error, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA005_MagicNumber_Descriptor()
    {
        var d = Assert.Single(new DA005_MagicNumberAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA005", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(1, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA006_NestedTernary_Descriptor()
    {
        var d = Assert.Single(new DA006_NestedTernaryAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA006", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA007_ParameterCount_Descriptor()
    {
        var d = Assert.Single(new DA007_ParameterCountAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA007", d.Id);
        Assert.Equal(DiagnosticSeverity.Info, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
        Assert.Contains("threshold: 5", d.MessageFormat.ToString());
    }

    [Fact]
    public void DA008_StringFormat_Descriptor()
    {
        var d = Assert.Single(new DA008_StringFormatAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA008", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA009_AsyncVoid_Descriptor()
    {
        var d = Assert.Single(new DA009_AsyncVoidAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA009", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(1, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA010_UnawaitedTask_Descriptor()
    {
        var d = Assert.Single(new DA010_UnawaitedTaskAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA010", d.Id);
        Assert.Equal(DiagnosticSeverity.Error, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(1, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA011_DateTimeNow_Descriptor()
    {
        var d = Assert.Single(new DA011_DateTimeNowAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA011", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DA012_MissingXmlDoc_Descriptor()
    {
        var d = Assert.Single(new DA012_MissingXmlDocAnalyzer().SupportedDiagnostics);
        Assert.Equal("DA012", d.Id);
        Assert.Equal(DiagnosticSeverity.Info, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DSSEC_SecretScanner_Descriptor()
    {
        var d = Assert.Single(new SecretScannerAnalyzer().SupportedDiagnostics);
        Assert.Equal("DSSEC", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(1, ArgCount(d.MessageFormat.ToString()));
        Assert.Equal("Security", d.Category);
    }

    // ─── DS (Doane-stack) rules ────────────────────────────────────────────────

    [Fact]
    public void DS002_SqlConnection_Descriptor()
    {
        var d = Assert.Single(new DS002_SqlConnectionAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS002", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS003_HardcodedConnectionString_Descriptor()
    {
        var d = Assert.Single(new DS003_HardcodedConnectionStringAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS003", d.Id);
        Assert.Equal(DiagnosticSeverity.Error, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS004_HttpClient_Descriptor()
    {
        var d = Assert.Single(new DS004_HttpClientAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS004", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS005_SqlCancellationToken_Descriptor()
    {
        var d = Assert.Single(new DS005_SqlCancellationTokenAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS005", d.Id);
        Assert.Equal(DiagnosticSeverity.Info, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(1, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS006_EnvVarNullCheck_Descriptor()
    {
        var d = Assert.Single(new DS006_EnvVarNullCheckAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS006", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(0, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS007_ConductorTaskResult_Descriptor()
    {
        var d = Assert.Single(new DS007_ConductorTaskResultAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS007", d.Id);
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS008_ClassFileName_Descriptor()
    {
        var d = Assert.Single(new DS008_ClassFileNameAnalyzer().SupportedDiagnostics);
        Assert.Equal("DS008", d.Id);
        Assert.Equal(DiagnosticSeverity.Info, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal(2, ArgCount(d.MessageFormat.ToString()));
    }

    [Fact]
    public void DS009_NullSafety_HasTwoDescriptors()
    {
        var analyzer = new NullSafetyAnalyzer();
        Assert.Equal(2, analyzer.SupportedDiagnostics.Length);

        var first = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS009");
        Assert.Equal(DiagnosticSeverity.Warning, first.DefaultSeverity);
        Assert.True(first.IsEnabledByDefault);
        Assert.Contains("FirstOrDefault", first.MessageFormat.ToString());

        var second = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS009b");
        Assert.Equal(DiagnosticSeverity.Warning, second.DefaultSeverity);
        Assert.True(second.IsEnabledByDefault);
        Assert.Contains("SingleOrDefault", second.MessageFormat.ToString());
    }

    [Fact]
    public void DS010_SqlEmbedded_HasFourDescriptors()
    {
        var analyzer = new SqlEmbeddedAnalyzer();
        Assert.Equal(4, analyzer.SupportedDiagnostics.Length);

        var selectStar = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS010");
        Assert.Equal(DiagnosticSeverity.Warning, selectStar.DefaultSeverity);
        Assert.Equal("Performance", selectStar.Category);

        var noLock = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS010b");
        Assert.Equal(DiagnosticSeverity.Warning, noLock.DefaultSeverity);
        Assert.Equal("Reliability", noLock.Category);

        var dynamicSql = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS010c");
        Assert.Equal(DiagnosticSeverity.Error, dynamicSql.DefaultSeverity);
        Assert.Equal("Security", dynamicSql.Category);

        var missingWhere = analyzer.SupportedDiagnostics.Single(d => d.Id == "DS010d");
        Assert.Equal(DiagnosticSeverity.Warning, missingWhere.DefaultSeverity);
        Assert.Equal("Correctness", missingWhere.Category);
    }

    // ─── Registration sanity ──────────────────────────────────────────────────

    [Fact]
    public void AllAnalyzers_HaveAtLeastOneDescriptor()
    {
        var analyzers = new Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer[]
        {
            new DA001_ComplexityAnalyzer(),
            new DA002_MethodLengthAnalyzer(),
            new DA003_ClassSizeAnalyzer(),
            new DA004_EmptyCatchAnalyzer(),
            new DA005_MagicNumberAnalyzer(),
            new DA006_NestedTernaryAnalyzer(),
            new DA007_ParameterCountAnalyzer(),
            new DA008_StringFormatAnalyzer(),
            new DA009_AsyncVoidAnalyzer(),
            new DA010_UnawaitedTaskAnalyzer(),
            new DA011_DateTimeNowAnalyzer(),
            new DA012_MissingXmlDocAnalyzer(),
            new SecretScannerAnalyzer(),
            new DS002_SqlConnectionAnalyzer(),
            new DS003_HardcodedConnectionStringAnalyzer(),
            new DS004_HttpClientAnalyzer(),
            new DS005_SqlCancellationTokenAnalyzer(),
            new DS006_EnvVarNullCheckAnalyzer(),
            new DS007_ConductorTaskResultAnalyzer(),
            new DS008_ClassFileNameAnalyzer(),
            new NullSafetyAnalyzer(),
            new SqlEmbeddedAnalyzer(),
        };

        foreach (var analyzer in analyzers)
            Assert.NotEmpty(analyzer.SupportedDiagnostics);
    }
}
