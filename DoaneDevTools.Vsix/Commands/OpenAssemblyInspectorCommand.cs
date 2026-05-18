using System;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.AssemblyInspector;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DoaneDevTools.Commands
{
    /// <summary>
    /// Command handler for <c>Tools &gt; Doane Dev Tools &gt; Assembly Inspector...</c>.
    /// When invoked, shows (or re-focuses) the <see cref="AssemblyInspectorWindow"/> tool
    /// window.  The Assembly Inspector is powered by ICSharpCode.Decompiler and lets
    /// developers browse types, members, and IL for any assembly referenced by the solution
    /// without leaving Visual Studio.
    /// </summary>
    /// <remarks>
    /// The command is registered via <c>DoaneDevToolsCommands.vsct</c> using the pair
    /// <c>guidDoaneDevToolsCommandSet / OpenAssemblyInspectorCommandId</c>.
    /// </remarks>
    internal sealed class OpenAssemblyInspectorCommand
    {
        // --------------------------------------------------------------------
        // Fields
        // --------------------------------------------------------------------

        /// <summary>The package that owns this command.</summary>
        private readonly DoaneDevToolsPackage _package;

        /// <summary>The OleMenuCommand registered with VS.</summary>
        private readonly OleMenuCommand _menuCommand;

        // --------------------------------------------------------------------
        // Singleton
        // --------------------------------------------------------------------

        /// <summary>
        /// Gets the single initialised instance of this command, or <c>null</c>
        /// if <see cref="InitializeAsync"/> has not yet been called.
        /// </summary>
        public static OpenAssemblyInspectorCommand? Instance { get; private set; }

        // --------------------------------------------------------------------
        // Constructor (private — use InitializeAsync)
        // --------------------------------------------------------------------

        private OpenAssemblyInspectorCommand(DoaneDevToolsPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            var cmdId = new CommandID(
                PackageGuids.DoaneDevToolsCommandSetGuid,
                PackageIds.OpenAssemblyInspectorCommandId);

            _menuCommand = new OleMenuCommand(Execute, cmdId);
            commandService.AddCommand(_menuCommand);
        }

        // --------------------------------------------------------------------
        // Initialisation
        // --------------------------------------------------------------------

        /// <summary>
        /// Creates the singleton instance of the command and registers it with
        /// Visual Studio's <see cref="OleMenuCommandService"/>.
        /// Must be called from the UI thread inside
        /// <see cref="DoaneDevToolsPackage.InitializeAsync"/>.
        /// </summary>
        /// <param name="package">The owner package.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="package"/> is <c>null</c>.
        /// </exception>
        public static async Task InitializeAsync(DoaneDevToolsPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService =
                await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                ?? throw new InvalidOperationException("Could not obtain IMenuCommandService.");

            Instance = new OpenAssemblyInspectorCommand(package, commandService);
        }

        // --------------------------------------------------------------------
        // Execution
        // --------------------------------------------------------------------

        /// <summary>
        /// Handles the menu command by showing the <see cref="AssemblyInspectorWindow"/>.
        /// </summary>
        private void Execute(object sender, EventArgs e)
        {
            _ = ExecuteAsync();
        }

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            ToolWindowPane window = await _package.ShowToolWindowAsync(
                typeof(AssemblyInspectorWindow),
                id: 0,
                create: true,
                cancellationToken: _package.DisposalToken);

            if (window?.Frame is IVsWindowFrame frame)
            {
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
            }
        }
    }
}
