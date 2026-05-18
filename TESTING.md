# DoaneDevTools Testing Guide

Every production file is covered by exactly one (or more) of four strategies:

| Strategy | Description |
|---|---|
| **Unit** | Behavior asserted by xUnit tests in `DoaneDevTools.Analyzers.Tests` |
| **Contract** | Source-inspection or reflection-based test in `*ContractTests.cs` |
| **Compile** | Purely declarative — the C# compiler, Roslyn analyzers, and XAML compiler guarantee correctness |
| **Manual** | Host-dependent UI flow; follow the procedure in this document |

---

## Coverage Matrix

### DoaneDevTools.Analyzers

| File | Strategy | Test file |
|---|---|---|
| `DiagnosticIds.cs` | Compile + Contract | Ids referenced in every `DiagnosticContractTests` assertion |
| `Rules/DA001_ComplexityAnalyzer.cs` | Unit | `DA001_ComplexityTests.cs` |
| `Rules/DA002_MethodLengthAnalyzer.cs` | Unit | `DA002_MethodLengthTests.cs` |
| `Rules/DA003_ClassSizeAnalyzer.cs` | Unit | `DA003_ClassSizeTests.cs` |
| `Rules/DA004_EmptyCatchAnalyzer.cs` | Unit | `DA004_EmptyCatchTests.cs` |
| `Rules/DA005_MagicNumberAnalyzer.cs` | Unit | `DA005_MagicNumberTests.cs` |
| `Rules/DA006_NestedTernaryAnalyzer.cs` | Unit | `DA006_NestedTernaryTests.cs` |
| `Rules/DA007_ParameterCountAnalyzer.cs` | Unit | `DA007_ParameterCountTests.cs` |
| `Rules/DA008_StringFormatAnalyzer.cs` | Unit | `DA008_StringFormatTests.cs` |
| `Rules/DA009_AsyncVoidAnalyzer.cs` | Unit | `DA009_AsyncVoidTests.cs` |
| `Rules/DA010_UnawaitedTaskAnalyzer.cs` | Unit | `DA010_UnawaitedTaskTests.cs` |
| `Rules/DA011_DateTimeNowAnalyzer.cs` | Unit | `DA011_DateTimeNowTests.cs` |
| `Rules/DA012_MissingXmlDocAnalyzer.cs` | Unit | `DA012_MissingXmlDocTests.cs` |
| `Rules/DA012_SecretScannerAnalyzer.cs` | Unit | `DA012_SecretScannerTests.cs` |
| `Rules/DS002_SqlConnectionAnalyzer.cs` | Unit | `DS002_SqlConnectionTests.cs` |
| `Rules/DS003_HardcodedConnectionStringAnalyzer.cs` | Unit | `DS003_HardcodedConnectionStringTests.cs` |
| `Rules/DS004_HttpClientAnalyzer.cs` | Unit | `DS004_HttpClientTests.cs` |
| `Rules/DS005_SqlCancellationTokenAnalyzer.cs` | Unit | `DS005_SqlCancellationTokenTests.cs` |
| `Rules/DS006_EnvVarNullCheckAnalyzer.cs` | Unit | `DS006_EnvVarNullCheckTests.cs` |
| `Rules/DS007_ConductorTaskResultAnalyzer.cs` | Unit | `DS007_ConductorTaskResultTests.cs` |
| `Rules/DS008_ClassFileNameAnalyzer.cs` | Unit | `DS008_ClassFileNameTests.cs` |
| `Rules/DS009_NullSafetyAnalyzer.cs` | Unit | `DS009_NullSafetyTests.cs` |
| `Rules/DS010_SqlEmbeddedAnalyzer.cs` | Unit | `DS010_SqlEmbeddedTests.cs` |
| `CodeFixes/DA004_EmptyCatchFix.cs` | Unit | `DA004_EmptyCatchTests.cs` (CodeFix_AddsLoggingStub) |
| `CodeFixes/DA008_StringFormatFix.cs` | Unit | `DA008_StringFormatTests.cs` (CodeFix_ConvertsToInterpolation) |
| `CodeFixes/DA009_AsyncVoidFix.cs` | Unit | `DA009_AsyncVoidFixTests.cs` |
| `CodeFixes/DS002_SqlConnectionFix.cs` | Unit | `DS002_SqlConnectionFixTests.cs` |
| `CodeFixes/DS003_HardcodedConnectionStringFix.cs` | Unit | `DS003_HardcodedConnectionStringFixTests.cs` |
| `CodeFixes/DS006_EnvVarNullCheckFix.cs` | Unit | `DS006_EnvVarNullCheckFixTests.cs` |
| `Refactorings/AddNullGuardRefactoring.cs` | Contract | `BehavioralContractTests.cs` |
| `Refactorings/ConvertToRecordRefactoring.cs` | Contract | `BehavioralContractTests.cs` |
| `Refactorings/DoaneServiceScaffoldRefactoring.cs` | Contract | `BehavioralContractTests.cs` |
| `Refactorings/GenerateRepositoryInterfaceRefactoring.cs` | Contract | `BehavioralContractTests.cs` |
| `Refactorings/IntroduceCancellationTokenRefactoring.cs` | Contract | `BehavioralContractTests.cs` |

### DoaneDevTools.Analyzers.NuGet

| File | Strategy | Notes |
|---|---|---|
| `build/DoaneAnalyzers.props` | Compile | MSBuild props file; malformed XML fails the pack step |

### DoaneDevTools.ToolWindows — Infrastructure

| File | Strategy | Test file |
|---|---|---|
| `Infrastructure/ViewModelBase.cs` | Contract | `BehavioralContractTests.cs` (source-inspects INotifyPropertyChanged implementation) |
| `Infrastructure/RelayCommand.cs` | Contract | `BehavioralContractTests.cs` (source-inspects CommandManager.RequerySuggested wiring) |

### DoaneDevTools.ToolWindows — Models (POCOs)

All model files below are Compile-verified: they contain only auto-properties and no runtime behavior.

| File |
|---|
| `ApiDriftChecker/Models/DriftReport.cs` |
| `AssemblyInspector/Models/AssemblyNode.cs` |
| `DependencyMap/Models/GraphEdge.cs` |
| `DependencyMap/Models/GraphNode.cs` |
| `EnvManager/Models/EnvProfile.cs` |
| `EnvManager/Models/EnvVariable.cs` |
| `EnvManager/Models/ProjectFileNode.cs` |
| `TemplateBuilder/Models/TemplateManifest.cs` |
| `TodoDashboard/Models/TodoItem.cs` |

### DoaneDevTools.ToolWindows — XAML Views

All `.xaml` and `*WindowControl.xaml.cs` (code-behind) files are Compile-verified: XAML compilation catches binding errors, missing resources, and malformed markup.

| File | Strategy |
|---|---|
| `*/...WindowControl.xaml` | Compile (XAMLC) |
| `*/...WindowControl.xaml.cs` | Compile (InitializeComponent + DataContext assignment only) |

### DoaneDevTools.ToolWindows — Tool Window Panes

These files create the VS tool window shell (`ToolWindowPane`) and forward the `Content` property. They have no testable behavior beyond what the compiler verifies.

| File | Strategy | Manual procedure |
|---|---|---|
| `ApiDriftChecker/ApiDriftCheckerWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `AssemblyInspector/AssemblyInspectorWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `CodeQuery/CodeQueryWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `CssInspector/CssInspectorWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `DependencyMap/DependencyMapWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `DocGenerator/DocGeneratorWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `EnvManager/EnvManagerWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `SolutionScore/SolutionScoreWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `TemplateBuilder/TemplateBuilderWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |
| `TodoDashboard/TodoDashboardWindow.cs` | Compile + Manual | [Open Tool Windows](#open-tool-windows) |

### DoaneDevTools.ToolWindows — ViewModels

ViewModels contain business logic bound to VS services. The VS-integration entry points are contract-pinned; pure-logic paths should migrate to a separate `DoaneDevTools.ToolWindows.Tests` project (net48) when the Windows build is working.

| File | Strategy | Notes |
|---|---|---|
| `ApiDriftChecker/ApiDriftCheckerViewModel.cs` | Contract + Manual | Contract: BehavioralContractTests checks property/command names |
| `AssemblyInspector/AssemblyInspectorViewModel.cs` | Contract + Manual | |
| `CodeQuery/CodeQueryViewModel.cs` | Contract + Manual | |
| `CssInspector/CssInspectorViewModel.cs` | Contract + Manual | |
| `DependencyMap/DependencyMapViewModel.cs` | Contract + Manual | |
| `DocGenerator/DocGeneratorViewModel.cs` | Contract + Manual | |
| `EnvManager/EnvManagerViewModel.cs` | Contract + Manual | Contract: VsShellContractTests (file discovery globs) |
| `SolutionScore/SolutionScoreViewModel.cs` | Contract + Manual | |
| `TemplateBuilder/TemplateBuilderViewModel.cs` | Contract + Manual | |
| `TodoDashboard/TodoDashboardViewModel.cs` | Contract + Manual | |

### DoaneDevTools.ToolWindows — Services

| File | Strategy | Notes |
|---|---|---|
| `ApiDriftChecker/Services/ControllerAnalyzerService.cs` | Contract | Uses MSBuildWorkspace; contract-pins via source inspection |
| `ApiDriftChecker/Services/DriftAnalyzerService.cs` | Contract | Pure logic; migrate to ToolWindows.Tests (net48) for full unit coverage |
| `ApiDriftChecker/Services/OpenApiParserService.cs` | Contract | Pure logic; migrate to ToolWindows.Tests (net48) |
| `AssemblyInspector/Services/DecompilerService.cs` | Contract | ICSharpCode.Decompiler wrapper |
| `DependencyMap/Services/ForceLayoutService.cs` | Contract | Pure math; migrate to ToolWindows.Tests (net48) |
| `DependencyMap/Services/RoslynGraphBuilder.cs` | Contract | Roslyn-based; migrate to ToolWindows.Tests (net48) |
| `DocGenerator/Services/HtmlDocSiteGenerator.cs` | Contract | Pure HTML generation; migrate to ToolWindows.Tests (net48) |
| `DocGenerator/Services/XmlDocGeneratorService.cs` | Contract | Uses MSBuildWorkspace |
| `EnvManager/Services/EnvFileService.cs` | Contract | Pure file parsing (YamlDotNet); migrate to ToolWindows.Tests (net48) |
| `EnvManager/Services/ProfileStorageService.cs` | Contract | DPAPI; migrate to ToolWindows.Tests (net48) |
| `TemplateBuilder/Services/TemplateService.cs` | Contract | Pure file I/O; migrate to ToolWindows.Tests (net48) |
| `TodoDashboard/Services/GitBlameService.cs` | Contract | LibGit2Sharp; migrate to ToolWindows.Tests (net48) |
| `TodoDashboard/Services/TodoExportService.cs` | Contract | Pure CSV generation; migrate to ToolWindows.Tests (net48) |
| `TodoDashboard/Services/TodoScannerService.cs` | Contract | Pure regex; migrate to ToolWindows.Tests (net48) |

### DoaneDevTools.Vsix

| File | Strategy | Test file / notes |
|---|---|---|
| `DoaneDevToolsPackage.cs` | Contract + Manual | `VsShellContractTests.cs` (ProvideOptionPage, InstallOrUpdate); [Package Load](#package-load) |
| `PackageGuids.cs` | Compile | Constants only |
| `PackageIds.cs` | Compile | Constants only |
| `Commands/Open*Command.cs` (9 files) | Contract | Source-inspect in `BehavioralContractTests.cs` (each calls ShowToolWindow) |
| `Options/DoaneDevToolsOptions.cs` | Contract | `VsShellContractTests.cs` (default threshold values via source + reflection) |
| `Editor/GitBlameMargin.cs` | Contract | `VsShellContractTests.cs` (width, colors, MEF exports) |
| `Editor/PerformanceHotspotTagger.cs` | Contract | `VsShellContractTests.cs` (classification names, debounce, MEF export) |
| `Services/PreCommitHookInstaller.cs` | Contract | `VsShellContractTests.cs` (all 9 secret patterns, exit 1, block message) |
| `DoaneDevToolsCommands.vsct` | Compile | VSCT XML schema validation at build time |

### DoaneDevTools.Visualizers

Debugger visualizers activate only when the VS debugger evaluates a watched expression. The source files are Compile-verified; correctness requires manual procedure.

| File | Strategy | Manual procedure |
|---|---|---|
| `AssemblyInfo.cs` | Compile | Registration attributes |
| `ExceptionVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |
| `HttpClientVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |
| `HttpResponseVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |
| `JsonVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |
| `ListVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |
| `SqlConnectionVisualizer.cs` | Compile + Manual | [Debugger Visualizers](#debugger-visualizers) |

---

## Running Automated Tests

```powershell
# From repo root — runs all xUnit tests (Roslyn analyzer + contract + source-inspection)
dotnet test DoaneDevTools.Analyzers.Tests/DoaneDevTools.Analyzers.Tests.csproj --configuration Release

# Or via GitHub Actions (manual trigger):
# .github/workflows/analyzer-tests.yml
```

---

## Manual Test Procedures

### Prerequisites

- Visual Studio 2022 or 2026 installed
- VSIX built: `.\build.ps1 -Configuration Release`
- VSIX installed: `.\build.ps1 -Install` or double-click `DoaneDevTools.Vsix/bin/Release/DoaneDevTools.Vsix.vsix`

---

### Package Load

**Purpose**: Verify the extension loads without errors and installs the pre-commit hook.

1. Open Visual Studio.
2. Open any solution that has a `.git` directory.
3. Open **View → Output** (Ctrl+Alt+O), select "DoaneDevTools" from the dropdown.
4. **Expected**: No error messages. The output pane shows "DoaneDevTools loaded."
5. Navigate to `<solution-root>/.git/hooks/` in File Explorer.
6. **Expected**: A `pre-commit` file exists with executable content starting with `#!/bin/sh`.
7. Inspect `pre-commit` — **Expected**: Contains `DSSEC`, `AKIA[0-9A-Z]`, `ghp_`, `exit 1`.

---

### Open Tool Windows

**Purpose**: Verify each tool window opens, renders, and closes without errors.

Repeat for each window listed in the **View → Doane Dev Tools** menu:

| Window | Menu item |
|---|---|
| Env Manager | View → Doane Dev Tools → Env Manager |
| Template Builder | View → Doane Dev Tools → Template Builder |
| Todo Dashboard | View → Doane Dev Tools → Todo Dashboard |
| Dependency Map | View → Doane Dev Tools → Dependency Map |
| Assembly Inspector | View → Doane Dev Tools → Assembly Inspector |
| CSS Inspector | View → Doane Dev Tools → CSS Inspector |
| Test Data Seeder | View → Doane Dev Tools → Test Data Seeder |
| API Drift Checker | View → Doane Dev Tools → API Drift Checker |
| Solution Score | View → Doane Dev Tools → Solution Score |
| Code Query | View → Doane Dev Tools → Code Query |

**Steps per window**:
1. Click the menu item.
2. **Expected**: Window opens; no exception dialog; VS does not crash.
3. Interact with the primary action (e.g., click "Refresh" or "Scan").
4. **Expected**: Result displayed; no unhandled exception.
5. Close the window (X or re-click menu item to toggle).
6. **Expected**: Window closes cleanly; memory not visibly leaking across repeated open/close cycles.

---

### Env Manager — Apply / Validate Profiles

1. Open **Env Manager** window.
2. Open a solution that contains `.env` or `appsettings.json` files.
3. **Expected**: The "Files" list is populated with discovered files.
4. Create a new profile named "Test".
5. Set a variable: Key=`TEST_KEY`, Value=`hello`.
6. Click **Apply**.
7. In a Developer Command Prompt: `echo %TEST_KEY%`
   **Expected**: `hello`
8. Click **Validate**.
   **Expected**: Green checkmarks for required vars that are set; warnings for unset.

---

### Git Blame Margin

1. Open any C# file in the editor.
2. Verify the **Git Blame** margin is visible on the left side of the editor (dark panel with author name and date per line).
3. Open **Tools → Options → Doane Dev Tools → General**.
4. Toggle **Show Git Blame Margin** off.
5. Return to the editor. **Expected**: Margin hidden.
6. Toggle back on. **Expected**: Margin reappears with correct author/date per line.

---

### Performance Hotspot Highlighting

1. Open a C# file containing a method with high cyclomatic complexity (≥ 11 branches).
2. **Expected**: The method name token is highlighted in amber (CC ≥ 11) or red (CC ≥ 16).
3. Open **Tools → Options → Doane Dev Tools → General**.
4. Change **Hotspot Warning Threshold** to 5.
5. Return to editor. **Expected**: More methods highlighted.
6. Restore threshold to 11.

---

### Roslyn Analyzer In-Editor Diagnostics

1. Open a C# file.
2. Write `catch { }` — **Expected**: Red squiggle (DA004, Error severity).
3. Write `var conn = new SqlConnection("Server=test");` — **Expected**: Green squiggle (DS002, Warning).
4. Write `var key = "Server=localhost;Password=secret123";` — **Expected**: Yellow squiggle (DS003, Error).
5. Hover each squiggle and verify the correct diagnostic message and code fix light-bulb appears.
6. Apply each code fix and verify the resulting code matches the expected transformation.

---

### Pre-Commit Hook Blocking

1. In a test repo, stage a file containing `AKIAIOSFODNN7EXAMPLE`.
2. Attempt `git commit -m "test"`.
3. **Expected**: Commit is blocked with message `[DSSEC] Commit blocked: suspected secrets in staged files.`
4. Remove the secret, re-stage, re-commit.
5. **Expected**: Commit succeeds.

---

### Debugger Visualizers

**Prerequisites**: `DoaneDevTools.Visualizers.dll` installed in VS visualizers directory (`%VSINSTALLDIR%\Common7\Packages\Debugger\Visualizers\`).

1. Start debugging a project that uses `HttpClient`, `HttpResponseMessage`, `List<T>`, or `SqlConnection`.
2. In the Watch window, hover over a variable of one of those types.
3. **Expected**: A magnifying glass icon appears. Click it.
4. **Expected**: The Doane custom visualizer dialog opens, showing a friendly display of the object's state.
5. For `JsonVisualizer`: hover over a `JObject` variable.
   **Expected**: Pretty-printed JSON in the visualizer.

---

### VSIX Install / Uninstall

1. Build: `.\build.ps1 -Configuration Release`
2. Close Visual Studio.
3. Run: `VSIXInstaller.exe /q DoaneDevTools.Vsix/bin/Release/DoaneDevTools.Vsix.vsix`
4. Open Visual Studio. Navigate to **Extensions → Manage Extensions**.
5. **Expected**: "DoaneDevTools" listed under Installed with correct version.
6. Uninstall from **Manage Extensions**.
7. Restart Visual Studio.
8. **Expected**: DoaneDevTools menu items absent; no errors on startup.

---

## Future Work: ToolWindows Service Tests

The following pure-logic services live in the `net48` `DoaneDevTools.ToolWindows` project and are currently contract-pinned. When the Windows build is operational, create `DoaneDevTools.ToolWindows.Tests` (net48, xUnit, `UseWPF=true`) and add unit tests:

| Service | What to test |
|---|---|
| `TodoScannerService` | Regex matches for TODO/FIXME/HACK; ignores comments in strings |
| `TodoExportService` | CSV output format; escaping of commas and quotes |
| `ForceLayoutService` | Node positions converge; no NaN coordinates after N iterations |
| `DriftAnalyzerService` | Endpoint diff: added/removed/changed; method/path matching |
| `HtmlDocSiteGenerator` | Valid HTML structure; all public members appear in output |
| `EnvFileService` | Round-trip: write `.env` then read back; YAML key extraction |
| `TemplateService` | Discovers templates; substitution of template variables |
| `ProfileStorageService` | DPAPI round-trip: encrypt then decrypt returns original value |
| `RoslynGraphBuilder` | Simple project produces expected edge set |
