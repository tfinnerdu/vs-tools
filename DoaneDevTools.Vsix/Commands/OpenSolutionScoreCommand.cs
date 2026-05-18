using System;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.SolutionScore;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DoaneDevTools.Commands
{
    internal sealed class OpenSolutionScoreCommand
    {
        private readonly DoaneDevToolsPackage _package;
        public static OpenSolutionScoreCommand? Instance { get; private set; }

        private OpenSolutionScoreCommand(DoaneDevToolsPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            var cmdId = new CommandID(PackageGuids.DoaneDevToolsCommandSetGuid, PackageIds.OpenSolutionScoreCommandId);
            commandService.AddCommand(new OleMenuCommand(Execute, cmdId));
        }

        public static async Task InitializeAsync(DoaneDevToolsPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var svc = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                ?? throw new InvalidOperationException("IMenuCommandService unavailable.");
            Instance = new OpenSolutionScoreCommand(package, svc);
        }

        private void Execute(object sender, EventArgs e) => _ = ExecuteAsync();

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            var window = await _package.ShowToolWindowAsync(
                typeof(SolutionScoreWindow), 0, true, _package.DisposalToken);
            if (window?.Frame is IVsWindowFrame frame)
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }
    }
}
