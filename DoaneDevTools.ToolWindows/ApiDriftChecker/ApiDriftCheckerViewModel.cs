using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DoaneDevTools.ToolWindows.ApiDriftChecker.Models;
using DoaneDevTools.ToolWindows.ApiDriftChecker.Services;
using DoaneDevTools.ToolWindows.Shared;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker
{
    public class ApiDriftCheckerViewModel : INotifyPropertyChanged
    {
        private string _selectedController = string.Empty;
        private string _selectedSpec = string.Empty;
        private string _statusMessage = "Select a controller and spec file, then click Analyze.";
        private bool _hasReport;
        private int _inSyncCount;
        private int _driftCount;

        public ObservableCollection<string> ControllerFiles { get; } = new();
        public ObservableCollection<string> SpecFiles { get; } = new();
        public ObservableCollection<DriftItem> DriftItems { get; } = new();

        public string SelectedController
        {
            get => _selectedController;
            set { _selectedController = value; OnPropertyChanged(nameof(SelectedController)); }
        }

        public string SelectedSpec
        {
            get => _selectedSpec;
            set { _selectedSpec = value; OnPropertyChanged(nameof(SelectedSpec)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public bool HasReport
        {
            get => _hasReport;
            set { _hasReport = value; OnPropertyChanged(nameof(HasReport)); }
        }

        public int InSyncCount
        {
            get => _inSyncCount;
            set { _inSyncCount = value; OnPropertyChanged(nameof(InSyncCount)); }
        }

        public int DriftCount
        {
            get => _driftCount;
            set { _driftCount = value; OnPropertyChanged(nameof(DriftCount)); }
        }

        public string ReportSummary =>
            $"API Drift Report — {System.IO.Path.GetFileName(SelectedController)} vs {System.IO.Path.GetFileName(SelectedSpec)}";

        public ICommand AnalyzeCommand { get; }
        public ICommand SyncSpecCommand { get; }

        public ApiDriftCheckerViewModel()
        {
            AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync());
            SyncSpecCommand = new RelayCommand(async _ => await SyncSpecAsync(),
                _ => HasReport && DriftCount > 0);
        }

        public void LoadFromSolution(string solutionPath, string[] controllerFiles, string[] specFiles)
        {
            ControllerFiles.Clear();
            SpecFiles.Clear();

            foreach (var f in controllerFiles) ControllerFiles.Add(f);
            foreach (var f in specFiles) SpecFiles.Add(f);
        }

        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrEmpty(SelectedController) || string.IsNullOrEmpty(SelectedSpec))
            {
                StatusMessage = "Select both a controller file and a spec file first.";
                return;
            }

            StatusMessage = "Analyzing...";
            DriftItems.Clear();
            HasReport = false;

            try
            {
                var controllerAnalyzer = new ControllerAnalyzerService();
                var specParser = new OpenApiParserService();
                var driftAnalyzer = new DriftAnalyzerService();

                // Note: in practice this would use the live Roslyn workspace from VS;
                // here we use MSBuildWorkspace which requires a project/solution path
                var codeEndpoints = await controllerAnalyzer.GetEndpointsAsync(SelectedController);
                var specEndpoints = specParser.ParseSpec(SelectedSpec);
                var report = driftAnalyzer.Analyze(codeEndpoints, specEndpoints, SelectedController, SelectedSpec);

                foreach (var item in report.Items)
                    DriftItems.Add(item);

                InSyncCount = report.InSyncCount;
                DriftCount = report.DriftCount;
                HasReport = true;
                StatusMessage = $"Analysis complete: {InSyncCount} in sync, {DriftCount} drifted.";
                OnPropertyChanged(nameof(ReportSummary));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Analysis failed: {ex.Message}";
            }
        }

        private async Task SyncSpecAsync()
        {
            StatusMessage = "Syncing spec from code — not yet implemented in this preview.";
            await Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
