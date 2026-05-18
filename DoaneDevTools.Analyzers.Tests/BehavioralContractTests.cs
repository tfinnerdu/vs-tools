using System;
using System.IO;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

/// <summary>
/// Source-inspection contract tests for refactoring providers and code fix providers.
/// Reads .cs source files from the repository to verify that required attributes and
/// constant strings are present, without needing a compiled assembly or a running VS instance.
/// </summary>
public class BehavioralContractTests
{
    private static string RepoRoot()
    {
        var dir = Path.GetDirectoryName(typeof(BehavioralContractTests).Assembly.Location)!;
        while (dir != null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Cannot locate repo root (.git not found)");
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relativePath));

    // ─── Refactoring providers ────────────────────────────────────────────────

    [Fact]
    public void AddNullGuardRefactoring_HasCorrectTitle()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/Refactorings/AddNullGuardRefactoring.cs");
        Assert.Contains("[ExportCodeRefactoringProvider(LanguageNames.CSharp", src);
        Assert.Contains("\"Add null guard\"", src);
    }

    [Fact]
    public void ConvertToRecordRefactoring_HasCorrectTitle()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/Refactorings/ConvertToRecordRefactoring.cs");
        Assert.Contains("[ExportCodeRefactoringProvider(LanguageNames.CSharp", src);
        Assert.Contains("\"Convert to record\"", src);
    }

    [Fact]
    public void DoaneServiceScaffoldRefactoring_HasCorrectTitle()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/Refactorings/DoaneServiceScaffoldRefactoring.cs");
        Assert.Contains("[ExportCodeRefactoringProvider(LanguageNames.CSharp", src);
        Assert.Contains("\"Apply Doane service scaffold\"", src);
    }

    [Fact]
    public void GenerateRepositoryInterfaceRefactoring_HasCorrectTitle()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/Refactorings/GenerateRepositoryInterfaceRefactoring.cs");
        Assert.Contains("[ExportCodeRefactoringProvider(LanguageNames.CSharp", src);
        Assert.Contains("\"Generate repository interface\"", src);
    }

    [Fact]
    public void IntroduceCancellationTokenRefactoring_HasCorrectTitle()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/Refactorings/IntroduceCancellationTokenRefactoring.cs");
        Assert.Contains("[ExportCodeRefactoringProvider(LanguageNames.CSharp", src);
        Assert.Contains("\"Add CancellationToken parameter\"", src);
    }

    // ─── Code fix providers ───────────────────────────────────────────────────

    [Fact]
    public void DA004_EmptyCatchFix_FixesDA004()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DA004_EmptyCatchFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DA004", src);
    }

    [Fact]
    public void DA008_StringFormatFix_FixesDA008()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DA008_StringFormatFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DA008", src);
    }

    [Fact]
    public void DA009_AsyncVoidFix_FixesDA009()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DA009_AsyncVoidFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DA009", src);
    }

    [Fact]
    public void DS002_SqlConnectionFix_FixesDS002()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DS002_SqlConnectionFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DS002", src);
    }

    [Fact]
    public void DS003_HardcodedConnectionStringFix_FixesDS003()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DS003_HardcodedConnectionStringFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DS003", src);
    }

    [Fact]
    public void DS006_EnvVarNullCheckFix_FixesDS006()
    {
        var src = ReadSource("DoaneDevTools.Analyzers/CodeFixes/DS006_EnvVarNullCheckFix.cs");
        Assert.Contains("[ExportCodeFixProvider", src);
        Assert.Contains("DiagnosticIds.DS006", src);
    }

    // ─── VS Commands (Open*Command.cs files) ─────────────────────────────────

    [Fact]
    public void AllOpenCommands_CallShowToolWindow()
    {
        var commandFiles = new[]
        {
            "DoaneDevTools.Vsix/Commands/OpenEnvManagerCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenTemplateBuilderCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenTodoDashboardCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenDependencyMapCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenAssemblyInspectorCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenCssInspectorCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenApiDriftCheckerCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenSolutionScoreCommand.cs",
            "DoaneDevTools.Vsix/Commands/OpenCodeQueryCommand.cs",
        };

        foreach (var relPath in commandFiles)
        {
            var src = ReadSource(relPath);
            Assert.Contains("ShowToolWindowAsync", src);
            Assert.Contains("PackageIds.", src);
        }
    }

    // ─── Infrastructure: ViewModelBase ────────────────────────────────────────

    [Fact]
    public void ViewModelBase_ImplementsINotifyPropertyChanged()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/Infrastructure/ViewModelBase.cs");
        Assert.Contains("INotifyPropertyChanged", src);
        Assert.Contains("PropertyChanged", src);
        Assert.Contains("SetProperty", src);
    }

    // ─── Infrastructure: RelayCommand ─────────────────────────────────────────

    [Fact]
    public void RelayCommand_UsesCommandManagerForCanExecuteChanged()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/Infrastructure/RelayCommand.cs");
        Assert.Contains("CommandManager.RequerySuggested", src);
        Assert.Contains("RaiseCanExecuteChanged", src);
        Assert.Contains("CommandManager.InvalidateRequerySuggested", src);
    }

    [Fact]
    public void AsyncRelayCommand_ExistsAndHasExecutingFlag()
    {
        var src = ReadSource("DoaneDevTools.ToolWindows/Infrastructure/RelayCommand.cs");
        Assert.Contains("AsyncRelayCommand", src);
        Assert.Contains("_isExecuting", src);
    }
}
