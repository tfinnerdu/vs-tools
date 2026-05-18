using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.DocGenerator.Services;
using DoaneDevTools.ToolWindows.Infrastructure;

namespace DoaneDevTools.ToolWindows.DocGenerator
{
    /// <summary>
    /// ViewModel for the Documentation Generator tool window.
    /// Drives project selection, generation options, progress reporting,
    /// and cancellation.
    /// </summary>
    public sealed class DocGeneratorViewModel : ViewModelBase
    {
        private readonly XmlDocGeneratorService _xmlDocService  = new XmlDocGeneratorService();
        private readonly HtmlDocSiteGenerator   _htmlSiteGen    = new HtmlDocSiteGenerator();

        private CancellationTokenSource? _cts;

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------

        private ObservableCollection<string> _projects        = new ObservableCollection<string>();
        private List<string>                 _selectedProjects = new List<string>();
        private string                       _outputPath      = string.Empty;
        private bool                         _generateXmlDocs  = true;
        private bool                         _generateHtmlSite = true;
        private bool                         _openWhenComplete = true;
        private double                       _progress;
        private string                       _progressLabel   = string.Empty;
        private string                       _logOutput       = string.Empty;
        private bool                         _isBusy;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public DocGeneratorViewModel()
        {
            GenerateCommand = new RelayCommand(_ => _ = GenerateAsync(), _ => !IsBusy && SelectedProjects.Count > 0 && !string.IsNullOrWhiteSpace(OutputPath));
            CancelCommand   = new RelayCommand(_ => Cancel(), _ => IsBusy);
        }

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public ObservableCollection<string> Projects
        {
            get => _projects;
            set => SetProperty(ref _projects, value);
        }

        public List<string> SelectedProjects
        {
            get => _selectedProjects;
            set
            {
                _selectedProjects = value;
                (GenerateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                SetProperty(ref _outputPath, value);
                (GenerateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool GenerateXmlDocs
        {
            get => _generateXmlDocs;
            set => SetProperty(ref _generateXmlDocs, value);
        }

        public bool GenerateHtmlSite
        {
            get => _generateHtmlSite;
            set => SetProperty(ref _generateHtmlSite, value);
        }

        public bool OpenWhenComplete
        {
            get => _openWhenComplete;
            set => SetProperty(ref _openWhenComplete, value);
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string ProgressLabel
        {
            get => _progressLabel;
            set => SetProperty(ref _progressLabel, value);
        }

        public string LogOutput
        {
            get => _logOutput;
            set => SetProperty(ref _logOutput, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (GenerateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelCommand   as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------

        public RelayCommand GenerateCommand { get; }
        public RelayCommand CancelCommand   { get; }

        // -----------------------------------------------------------------------
        // Public API (called from code-behind)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Scans the solution for .csproj files and populates <see cref="Projects"/>.
        /// In a real VS integration this would query the IVsSolution service.
        /// </summary>
        public void RefreshProjects()
        {
            Projects.Clear();

            // Heuristic: look for .csproj files starting from the working directory.
            var searchRoot = Directory.GetCurrentDirectory();
            try
            {
                foreach (var proj in Directory.EnumerateFiles(searchRoot, "*.csproj",
                    SearchOption.AllDirectories))
                {
                    Projects.Add(proj);
                }
            }
            catch (UnauthorizedAccessException) { /* skip inaccessible dirs */ }

            AppendLog($"Found {Projects.Count} project(s).");
        }

        /// <summary>
        /// Called by the VS host to set the known project paths from the
        /// IVsSolution enumeration.
        /// </summary>
        public void SetProjectPaths(IEnumerable<string> projectPaths)
        {
            Projects.Clear();
            foreach (var p in projectPaths)
                Projects.Add(p);
        }

        // -----------------------------------------------------------------------
        // Private command implementations
        // -----------------------------------------------------------------------

        private async Task GenerateAsync()
        {
            IsBusy = true;
            Progress = 0;
            LogOutput = string.Empty;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                int total = SelectedProjects.Count;
                int done  = 0;

                foreach (var projectPath in SelectedProjects)
                {
                    token.ThrowIfCancellationRequested();

                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    var projectDir  = Path.GetDirectoryName(projectPath) ?? string.Empty;

                    AppendLog($"Processing: {projectName}");
                    ProgressLabel = $"Processing {projectName}…";

                    // ── Step 1: Generate XML doc comments if requested ────────
                    if (GenerateXmlDocs)
                    {
                        AppendLog("  Generating XML doc comments…");
                        try
                        {
                            int added = await Task.Run(() =>
                                _xmlDocService.GenerateForProjectAsync(projectPath, token).GetAwaiter().GetResult(),
                                token);
                            AppendLog($"  Added {added} XML doc comment(s).");
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            AppendLog($"  [WARN] XML doc generation failed: {ex.Message}");
                        }
                    }

                    // ── Step 2: Locate the XML output file from a prior build ─
                    string? xmlFile = null;
                    if (GenerateHtmlSite)
                    {
                        var candidates = Directory.GetFiles(projectDir, "*.xml",
                            SearchOption.AllDirectories);
                        // The XML doc file typically has the same name as the DLL.
                        xmlFile = Array.Find(candidates,
                            f => Path.GetFileNameWithoutExtension(f)
                                     .Equals(projectName, StringComparison.OrdinalIgnoreCase));
                    }

                    // ── Step 3: Generate HTML site ────────────────────────────
                    if (GenerateHtmlSite)
                    {
                        var outDir = Path.Combine(OutputPath, projectName);
                        AppendLog($"  Generating HTML site → {outDir}");
                        try
                        {
                            await Task.Run(() =>
                                _htmlSiteGen.Generate(xmlFile, outDir, projectName), token);
                            AppendLog($"  HTML site generated successfully.");
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            AppendLog($"  [WARN] HTML site generation failed: {ex.Message}");
                        }
                    }

                    done++;
                    Progress      = (double)done / total * 100;
                    ProgressLabel = $"{done} / {total} project(s) completed.";
                }

                AppendLog("Done.");
                ProgressLabel = "Completed.";

                if (OpenWhenComplete && Directory.Exists(OutputPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = OutputPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("Cancelled.");
                ProgressLabel = "Cancelled.";
            }
            catch (Exception ex)
            {
                AppendLog($"Error: {ex.Message}");
                ProgressLabel = "Failed.";
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void Cancel()
        {
            _cts?.Cancel();
        }

        private void AppendLog(string message)
        {
            LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        }
    }
}
