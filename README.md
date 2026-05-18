# DoaneDevTools

**DoaneDevTools** is a Visual Studio 2022/2026 extension built for Doane University developers. It bundles Roslyn analyzers, code fixes, and a suite of productivity tool windows that enforce Doane development standards and streamline everyday workflows.

---

## Requirements

| Requirement | Version |
|---|---|
| Visual Studio | 2022 (17.x) or 2026 (18.x) |
| VS Workload | **Visual Studio extension development** (VS SDK) |
| .NET SDK | 8.0+ (for analyzer build tooling) |
| Target Runtime | .NET Framework 4.8 (VSIX host) |

---

## Building

1. Clone the repository and open `DoaneDevTools.sln` in Visual Studio 2022 or later.
2. Ensure the **Visual Studio extension development** workload is installed via the VS Installer.
3. Build the solution (`Ctrl+Shift+B`). The VSIX output lands in:
   ```
   DoaneDevTools.Vsix\bin\Debug\DoaneDevTools.Vsix.vsix
   ```
4. To build from the command line:
   ```
   msbuild DoaneDevTools.sln /p:Configuration=Release
   ```

---

## Installing the VSIX

**Developer machine (Debug):**
- Press **F5** inside Visual Studio to launch an Experimental Instance with the extension loaded automatically.

**Manual install:**
1. Build in Release mode.
2. Double-click `DoaneDevTools.Vsix\bin\Release\DoaneDevTools.Vsix.vsix`.
3. Follow the VSIX Installer prompts and restart Visual Studio.

**Enterprise / CI deploy:**
```
VSIXInstaller.exe /quiet DoaneDevTools.Vsix.vsix
```

---

## Features

| Feature | Description |
|---|---|
| **Roslyn Analyzers** | Compile-time diagnostics and code fixes enforcing Doane coding standards (naming, async patterns, null safety). Distributed as a standalone NuGet via `DoaneDevTools.Analyzers.NuGet`. |
| **Environment Manager** | GUI tool window (`Tools > Doane Dev Tools > Environment Manager...`) for reading, writing, and diffing `.env` / YAML config files across multiple environments without leaving Visual Studio. |
| **Template Builder** | Scaffold new Doane-standard projects, controllers, services, and test classes using curated `scriban` / T4 templates. Accessible from `Tools > Doane Dev Tools > Template Builder...`. |
| **TODO Dashboard** | Aggregated view of `TODO`, `FIXME`, `HACK`, and `UNDONE` comments across the entire solution, filterable by author, file, and severity. |
| **Dependency Map** | Visual graph of project-to-project and NuGet references within the solution, with quick-jump navigation and outdated-package highlighting. Integrates with LibGit2Sharp to correlate dependency changes to commits. |
| **Assembly Inspector** | Powered by ICSharpCode.Decompiler — browse types, members, and IL for any assembly referenced by the solution without leaving Visual Studio. |
| **CSS Layout Inspector** | Renders a live preview of CSS box-model and Flexbox/Grid rules from your stylesheet files, with an annotated visual breakdown panel. |

---

## Project Structure

```
DoaneDevTools.sln
├── DoaneDevTools.Vsix/          # Main VSIX host package, commands, and VSCT menus
├── DoaneDevTools.Analyzers/     # Roslyn DiagnosticAnalyzers and CodeFixProviders
├── DoaneDevTools.Analyzers.NuGet/ # NuGet packaging project for the analyzers
├── DoaneDevTools.ToolWindows/   # WPF tool window controls (MVVM)
└── DoaneDevTools.Visualizers/   # Debug visualizer components
```

---

## Contributing

Internal Doane University project. Contact the IT Development team for access and contribution guidelines.
