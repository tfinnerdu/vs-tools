using System.Threading.Tasks;
using DoaneDevTools.Analyzers.Rules;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace DoaneDevTools.Analyzers.Tests;

public class DS007_ConductorTaskResultTests
{
    [Fact]
    public async Task NoDiagnostic_CompleteTaskResult()
    {
        // TaskResult has both Status and Output — no diagnostic
        var source = @"
[WorkerTask]
class MyWorker {
    public TaskResult Execute() => new TaskResult();
}
class TaskResult {
    public string Status { get; set; }
    public string Output { get; set; }
}
class WorkerTaskAttribute : System.Attribute {}";

        await AnalyzerVerifier<DS007_ConductorTaskResultAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Diagnostic_MissingOutputProperty()
    {
        // BadResult only has Status, missing Output
        // MyWorker identifier: line 3, col 7 ("class " = 6 chars → col 7)
        var source = @"
[WorkerTask]
class MyWorker {
    public BadResult Execute() => new BadResult();
}
class BadResult {
    public string Status { get; set; }
}
class WorkerTaskAttribute : System.Attribute {}";

        var expected = new DiagnosticResult("DS007", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(3, 7)
            .WithArguments("MyWorker", "Output");

        await AnalyzerVerifier<DS007_ConductorTaskResultAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task NoDiagnostic_PlainClassIgnored()
    {
        // No [WorkerTask] attribute — analyzer ignores it
        var source = @"
class PlainClass {
    public NoResult Execute() => new NoResult();
}
class NoResult { }";

        await AnalyzerVerifier<DS007_ConductorTaskResultAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
