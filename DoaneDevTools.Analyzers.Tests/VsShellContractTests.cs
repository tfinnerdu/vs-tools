using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

/// <summary>
/// Contract tests for VS-shell-dependent components. Two strategies are used:
///
/// 1. SOURCE INSPECTION (primary) — reads .cs source files and asserts that known-good
///    constants, patterns, and attribute arguments are present. Runs everywhere (CI, Linux,
///    no build required) and catches the most common kinds of accidental breakage.
///
/// 2. ASSEMBLY REFLECTION (secondary) — uses MetadataLoadContext to load the net48 VSIX
///    DLL and verify compiled metadata (const field values, attribute arguments). These
///    tests are skipped automatically when the assembly has not been built yet. Run a
///    full Windows build first to activate them. Reflection catches renames and moves
///    that string matching misses.
///
/// Neither strategy requires a running VS instance.
/// </summary>
public class VsShellContractTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string RepoRoot()
    {
        var dir = Path.GetDirectoryName(typeof(VsShellContractTests).Assembly.Location)!;
        while (dir != null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Cannot locate repo root (.git not found)");
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relativePath));

    /// <summary>
    /// Tries to find the VSIX assembly from a Windows Debug or Release build output.
    /// Returns null when the project has not been built yet — callers skip the test.
    /// </summary>
    private static string? FindVsixAssembly()
    {
        foreach (var config in new[] { "Release", "Debug" })
        {
            var path = Path.Combine(RepoRoot(),
                "DoaneDevTools.Vsix", "bin", config,
                "DoaneDevTools.Vsix.dll");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    // ─── Source inspection: GitBlameMargin ────────────────────────────────────

    [Fact]
    public void GitBlameMargin_Width_Is220()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("MarginWidth = 220", src);
    }

    [Fact]
    public void GitBlameMargin_MarginName_IsDoaneGitBlame()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("\"DoaneGitBlame\"", src);
    }

    [Fact]
    public void GitBlameMargin_AuthorColor_IsExpected()
    {
        // Solarized green — change this assertion if the palette changes intentionally
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("0x85, 0x99, 0x00", src);
    }

    [Fact]
    public void GitBlameMargin_DateColor_IsExpected()
    {
        // Solarized blue
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("0x65, 0x8B, 0xD0", src);
    }

    [Fact]
    public void GitBlameMargin_BackgroundColor_IsExpected()
    {
        // Near-black panel background
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("0x25, 0x25, 0x26", src);
    }

    [Fact]
    public void GitBlameMargin_ExportsIWpfTextViewMarginProvider()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("[Export(typeof(IWpfTextViewMarginProvider))]", src);
    }

    [Fact]
    public void GitBlameMargin_ContentTypes_IncludeCSharpBasicPython()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/GitBlameMargin.cs");
        Assert.Contains("[ContentType(\"CSharp\")]", src);
        Assert.Contains("[ContentType(\"Basic\")]", src);
        Assert.Contains("[ContentType(\"Python\")]", src);
    }

    // ─── Source inspection: PerformanceHotspotTagger ──────────────────────────

    [Fact]
    public void PerformanceHotspot_ExportsITaggerProvider()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/PerformanceHotspotTagger.cs");
        Assert.Contains("[Export(typeof(ITaggerProvider))]", src);
    }

    [Fact]
    public void PerformanceHotspot_DebounceMs_Is800()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/PerformanceHotspotTagger.cs");
        // Timer constructed with 800ms delay
        Assert.Contains("800", src);
    }

    [Fact]
    public void PerformanceHotspot_ClassificationNames_AreStable()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Editor/PerformanceHotspotTagger.cs");
        Assert.Contains("DoaneHotspotWarning", src);
        Assert.Contains("DoaneHotspotCritical", src);
    }

    // ─── Source inspection: DoaneDevToolsOptions (default thresholds) ──────────

    [Fact]
    public void Options_HotspotWarningThreshold_DefaultIs11()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Options/DoaneDevToolsOptions.cs");
        // Both the [DefaultValue] attribute and the property initializer encode 11
        Assert.Matches(new Regex(@"HotspotWarningThreshold\b[^\n]+= 11\b"), src);
    }

    [Fact]
    public void Options_HotspotCriticalThreshold_DefaultIs16()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Options/DoaneDevToolsOptions.cs");
        Assert.Matches(new Regex(@"HotspotCriticalThreshold\b[^\n]+= 16\b"), src);
    }

    [Fact]
    public void Options_ShowGitBlameMargin_Exists()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Options/DoaneDevToolsOptions.cs");
        Assert.Contains("ShowGitBlameMargin", src);
    }

    // ─── Source inspection: PreCommitHookInstaller ────────────────────────────

    [Fact]
    public void PreCommitHook_ContainsAwsAccessKeyPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("AKIA[0-9A-Z]", src);
    }

    [Fact]
    public void PreCommitHook_ContainsStripeKeyPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("sk_(live|test)_", src);
    }

    [Fact]
    public void PreCommitHook_ContainsGitHubPatPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("ghp_", src);
    }

    [Fact]
    public void PreCommitHook_ContainsGitLabPatPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("glpat-", src);
    }

    [Fact]
    public void PreCommitHook_ContainsPasswordPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains(@"password\s*=\s*", src);
    }

    [Fact]
    public void PreCommitHook_ContainsBearerTokenPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("Bearer", src);
    }

    [Fact]
    public void PreCommitHook_ContainsConnectionStringPattern()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("Server=", src);
        Assert.Contains("Password=", src);
    }

    [Fact]
    public void PreCommitHook_BlocksCommitOnFailure()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains("exit 1", src);
        Assert.Contains("Commit blocked", src);
    }

    [Fact]
    public void PreCommitHook_SkipsBinaryAndLockFiles()
    {
        var src = ReadSource("DoaneDevTools.Vsix/Services/PreCommitHookInstaller.cs");
        Assert.Contains(".dll", src);
        Assert.Contains(".lock", src);
    }

    // ─── Source inspection: EnvManagerViewModel file-discovery rules ──────────

    [Fact]
    public void EnvManager_Discovers_DotEnvFiles()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/EnvManager/EnvManagerViewModel.cs");
        Assert.Contains("\".env\"", src);
    }

    [Fact]
    public void EnvManager_Discovers_AppSettingsJson()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/EnvManager/EnvManagerViewModel.cs");
        Assert.Contains("appsettings*.json", src);
    }

    [Fact]
    public void EnvManager_Discovers_StartLocalScript()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/EnvManager/EnvManagerViewModel.cs");
        Assert.Contains("start-local.ps1", src);
    }

    [Fact]
    public void EnvManager_Discovers_SecretYamlFiles()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/EnvManager/EnvManagerViewModel.cs");
        Assert.Contains("*secret*.yaml", src);
    }

    [Fact]
    public void EnvManager_Discovers_DockerComposeFiles()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/EnvManager/EnvManagerViewModel.cs");
        Assert.Contains("docker-compose*.yml", src);
    }

    // ─── Source inspection: package attribute registration ────────────────────

    [Fact]
    public void DoaneDevToolsPackage_RegistersAllToolWindows()
    {
        var src = ReadSource("DoaneDevTools.Vsix/DoaneDevToolsPackage.cs");
        Assert.Contains("EnvManagerWindow", src);
        Assert.Contains("TemplateBuilderWindow", src);
        Assert.Contains("TodoDashboardWindow", src);
        Assert.Contains("DependencyMapWindow", src);
        Assert.Contains("AssemblyInspectorWindow", src);
        Assert.Contains("CssInspectorWindow", src);
        Assert.Contains("TestDataSeederWindow", src);
        Assert.Contains("ApiDriftCheckerWindow", src);
        Assert.Contains("SolutionScoreWindow", src);
        Assert.Contains("CodeQueryWindow", src);
    }

    [Fact]
    public void DoaneDevToolsPackage_RegistersOptionPage()
    {
        var src = ReadSource("DoaneDevTools.Vsix/DoaneDevToolsPackage.cs");
        Assert.Contains("[ProvideOptionPage", src);
        Assert.Contains("DoaneDevToolsOptions", src);
    }

    [Fact]
    public void DoaneDevToolsPackage_InstallsPreCommitHook()
    {
        var src = ReadSource("DoaneDevTools.Vsix/DoaneDevToolsPackage.cs");
        Assert.Contains("PreCommitHookInstaller", src);
        Assert.Contains("InstallOrUpdate", src);
    }

    // ─── Assembly reflection: compiled const/attribute metadata ───────────────
    // These tests skip automatically when the VSIX project has not been built.
    // Build DoaneDevTools.Vsix on Windows (Release or Debug) to activate them.
    // They complement source inspection by verifying that compiled metadata matches
    // what the source says — catching moves, renames, and shadowed constants.

    // Note on skip behavior: these tests return early (pass silently) when the assembly
    // is absent. xUnit v2 has no built-in runtime-skip mechanism, so a passing no-op
    // is the idiomatic way to make an optional integration test. The assertions only
    // fire once someone has done a Windows Release/Debug build.

    [Fact]
    public void Reflection_GitBlameMargin_ConstWidth_Is220()
    {
        var assemblyPath = FindVsixAssembly();
        if (assemblyPath is null) return; // not yet built — nothing to verify

        using var ctx = new System.Reflection.MetadataLoadContext(
            new PathAssemblyResolver(
                Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")));

        var asm = ctx.LoadFromAssemblyPath(assemblyPath);
        var type = asm.GetType("DoaneDevTools.Vsix.Editor.GitBlameMargin")
                   ?? throw new InvalidOperationException("GitBlameMargin type not found in assembly.");

        var field = type.GetField("MarginWidth",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.NotNull(field);
        Assert.Equal(220.0, (double)field.GetRawConstantValue()!);
    }

    [Fact]
    public void Reflection_GitBlameMargin_MarginName_IsDoaneGitBlame()
    {
        var assemblyPath = FindVsixAssembly();
        if (assemblyPath is null) return;

        using var ctx = new System.Reflection.MetadataLoadContext(
            new PathAssemblyResolver(
                Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")));

        var asm = ctx.LoadFromAssemblyPath(assemblyPath);
        var type = asm.GetType("DoaneDevTools.Vsix.Editor.GitBlameMargin")
                   ?? throw new InvalidOperationException("GitBlameMargin type not found.");

        var field = type.GetField("MarginName",
            BindingFlags.NonPublic | BindingFlags.Public |
            BindingFlags.Static | BindingFlags.Instance);
        Assert.NotNull(field);
        Assert.Equal("DoaneGitBlame", (string)field.GetRawConstantValue()!);
    }

    [Fact]
    public void Reflection_DoaneDevToolsOptions_WarningThresholdDefault_Is11()
    {
        var assemblyPath = FindVsixAssembly();
        if (assemblyPath is null) return;

        using var ctx = new System.Reflection.MetadataLoadContext(
            new PathAssemblyResolver(
                Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")));

        var asm = ctx.LoadFromAssemblyPath(assemblyPath);
        var type = asm.GetType("DoaneDevTools.Vsix.Options.DoaneDevToolsOptions")
                   ?? throw new InvalidOperationException("DoaneDevToolsOptions type not found.");

        var prop = type.GetProperty("HotspotWarningThreshold");
        Assert.NotNull(prop);

        // [DefaultValue(11)] attribute encodes the contract value in metadata
        var attr = prop.GetCustomAttributesData()
                       .FirstOrDefault(a => a.AttributeType.Name == "DefaultValueAttribute");
        Assert.NotNull(attr);
        Assert.Equal(11, (int)attr.ConstructorArguments[0].Value!);
    }

    [Fact]
    public void Reflection_DoaneDevToolsOptions_CriticalThresholdDefault_Is16()
    {
        var assemblyPath = FindVsixAssembly();
        if (assemblyPath is null) return;

        using var ctx = new System.Reflection.MetadataLoadContext(
            new PathAssemblyResolver(
                Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")));

        var asm = ctx.LoadFromAssemblyPath(assemblyPath);
        var type = asm.GetType("DoaneDevTools.Vsix.Options.DoaneDevToolsOptions")
                   ?? throw new InvalidOperationException("DoaneDevToolsOptions type not found.");

        var prop = type.GetProperty("HotspotCriticalThreshold");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttributesData()
                       .FirstOrDefault(a => a.AttributeType.Name == "DefaultValueAttribute");
        Assert.NotNull(attr);
        Assert.Equal(16, (int)attr.ConstructorArguments[0].Value!);
    }
}
