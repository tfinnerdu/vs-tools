using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DoaneDevTools.Commands;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DoaneDevTools
{
    /// <summary>
    /// Entry point for the DoaneDevTools Visual Studio extension.
    /// This sealed <see cref="AsyncPackage"/> is registered with Visual Studio via
    /// the attributes below and is the root from which every command handler and tool
    /// window is initialised.
    /// </summary>
    /// <remarks>
    /// The package loads asynchronously in the background when a solution is fully
    /// loaded (<see cref="VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string"/>),
    /// keeping VS startup time unaffected.
    ///
    /// Menu definitions live in <c>DoaneDevToolsCommands.vsct</c>.  Each tool window
    /// type resides in the <c>DoaneDevTools.ToolWindows</c> project.
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(DoaneDevToolsPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindows.EnvManager.EnvManagerWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideToolWindow(typeof(ToolWindows.TemplateBuilder.TemplateBuilderWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideToolWindow(typeof(ToolWindows.TodoDashboard.TodoDashboardWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.ErrorList)]
    [ProvideToolWindow(typeof(ToolWindows.DependencyMap.DependencyMapWindow),
        Style = VsDockStyle.Float)]
    [ProvideToolWindow(typeof(ToolWindows.AssemblyInspector.AssemblyInspectorWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.ObjectBrowser)]
    [ProvideToolWindow(typeof(ToolWindows.CssInspector.CssInspectorWindow),
        Style = VsDockStyle.Float)]
    [ProvideToolWindow(typeof(ToolWindows.TestDataSeeder.TestDataSeederWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideToolWindow(typeof(ToolWindows.ApiDriftChecker.ApiDriftCheckerWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.ErrorList)]
    [ProvideToolWindow(typeof(ToolWindows.SolutionScore.SolutionScoreWindow),
        Style = VsDockStyle.Tabbed,
        Window = ToolWindowGuids.SolutionExplorer)]
    [ProvideToolWindow(typeof(ToolWindows.CodeQuery.CodeQueryWindow),
        Style = VsDockStyle.Float)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string,
        PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class DoaneDevToolsPackage : AsyncPackage
    {
        // --------------------------------------------------------------------
        // Public constants
        // --------------------------------------------------------------------

        /// <summary>
        /// The string form of the package GUID.
        /// Must match the <c>GuidSymbol</c> value in
        /// <c>DoaneDevToolsCommands.vsct</c> and the VSIX manifest identity.
        /// </summary>
        public const string PackageGuidString = "a1b2c3d4-5e6f-7890-abcd-ef1234567890";

        /// <summary>Parsed <see cref="Guid"/> of this package.</summary>
        public static readonly Guid PackageGuid = new Guid(PackageGuidString);

        // --------------------------------------------------------------------
        // AsyncPackage overrides
        // --------------------------------------------------------------------

        /// <summary>
        /// Called by the VS shell to initialise the package asynchronously.
        /// All six command handlers are wired up here; tool windows are created
        /// on demand when the user invokes the corresponding command.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token that is cancelled if VS is shutting down before initialisation completes.
        /// </param>
        /// <param name="progress">
        /// Progress sink for the VS status bar; currently unused but available for
        /// long-running initialisation steps.
        /// </param>
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switch to the UI thread for command registration.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await OpenEnvManagerCommand.InitializeAsync(this);
            await OpenTemplateBuilderCommand.InitializeAsync(this);
            await OpenTodoDashboardCommand.InitializeAsync(this);
            await OpenDependencyMapCommand.InitializeAsync(this);
            await OpenAssemblyInspectorCommand.InitializeAsync(this);
            await OpenCssInspectorCommand.InitializeAsync(this);
            await OpenTestDataSeederCommand.InitializeAsync(this);
            await OpenApiDriftCheckerCommand.InitializeAsync(this);
            await OpenSolutionScoreCommand.InitializeAsync(this);
            await OpenCodeQueryCommand.InitializeAsync(this);
        }

        // --------------------------------------------------------------------
        // Tool-window factory helpers
        // --------------------------------------------------------------------

        /// <summary>
        /// Shows (or creates) the <c>EnvManagerWindow</c> tool window.
        /// </summary>
        public async Task ShowEnvManagerWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.EnvManager.EnvManagerWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);
        }

        /// <summary>
        /// Shows (or creates) the <c>TemplateBuilderWindow</c> tool window.
        /// </summary>
        public async Task ShowTemplateBuilderWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.TemplateBuilder.TemplateBuilderWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);
        }

        /// <summary>
        /// Shows (or creates) the <c>TodoDashboardWindow</c> tool window.
        /// </summary>
        public async Task ShowTodoDashboardWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.TodoDashboard.TodoDashboardWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);
        }

        /// <summary>
        /// Shows (or creates) the <c>DependencyMapWindow</c> tool window.
        /// </summary>
        public async Task ShowDependencyMapWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.DependencyMap.DependencyMapWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);
        }

        /// <summary>
        /// Shows (or creates) the <c>AssemblyInspectorWindow</c> tool window.
        /// </summary>
        public async Task ShowAssemblyInspectorWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.AssemblyInspector.AssemblyInspectorWindow),
                id: 0,
                create: true,
                cancellationToken: DisposalToken);
        }

        /// <summary>
        /// Shows (or creates) the <c>CssInspectorWindow</c> tool window.
        /// </summary>
        public async Task ShowCssInspectorWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.CssInspector.CssInspectorWindow),
                id: 0, create: true, cancellationToken: DisposalToken);
        }

        public async Task ShowTestDataSeederWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.TestDataSeeder.TestDataSeederWindow),
                id: 0, create: true, cancellationToken: DisposalToken);
        }

        public async Task ShowApiDriftCheckerWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.ApiDriftChecker.ApiDriftCheckerWindow),
                id: 0, create: true, cancellationToken: DisposalToken);
        }

        public async Task ShowSolutionScoreWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.SolutionScore.SolutionScoreWindow),
                id: 0, create: true, cancellationToken: DisposalToken);
        }

        public async Task ShowCodeQueryWindowAsync()
        {
            await ShowToolWindowAsync(
                typeof(ToolWindows.CodeQuery.CodeQueryWindow),
                id: 0, create: true, cancellationToken: DisposalToken);
        }
    }
}
