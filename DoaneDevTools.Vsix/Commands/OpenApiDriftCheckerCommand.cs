using System;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.ApiDriftChecker;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DoaneDevTools.Commands
{
    internal sealed class OpenApiDriftCheckerCommand
    {
        private readonly DoaneDevToolsPackage _package;
        public static OpenApiDriftCheckerCommand? Instance { get; private set; }

        private OpenApiDriftCheckerCommand(DoaneDevToolsPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            var cmdId = new CommandID(PackageGuids.DoaneDevToolsCommandSetGuid, PackageIds.OpenApiDriftCheckerCommandId);
            commandService.AddCommand(new OleMenuCommand(Execute, cmdId));
        }

        public static async Task InitializeAsync(DoaneDevToolsPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var svc = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService
                ?? throw new InvalidOperationException("IMenuCommandService unavailable.");
            Instance = new OpenApiDriftCheckerCommand(package, svc);
        }

        private void Execute(object sender, EventArgs e) => _ = ExecuteAsync();

        private async Task ExecuteAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            var window = await _package.ShowToolWindowAsync(
                typeof(ApiDriftCheckerWindow), 0, true, _package.DisposalToken);
            if (window?.Frame is IVsWindowFrame frame)
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
        }
    }
}
