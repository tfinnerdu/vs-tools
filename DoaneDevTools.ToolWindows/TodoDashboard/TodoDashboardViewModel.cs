using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.Infrastructure;
using DoaneDevTools.ToolWindows.TodoDashboard.Models;
using DoaneDevTools.ToolWindows.TodoDashboard.Services;

namespace DoaneDevTools.ToolWindows.TodoDashboard
{
    /// <summary>
    /// ViewModel for the TODO Dashboard tool window.
    /// </summary>
    public class TodoDashboardViewModel : ViewModelBase
    {
        // -----------------------------------------------------------------------
        // Services
        // -----------------------------------------------------------------------
        private readonly TodoScannerService _scanner = new TodoScannerService();
        private readonly TodoExportService  _exporter = new TodoExportService();

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------
        private ObservableCollection<TodoItem> _todoItems = new ObservableCollection<TodoItem>();
        private string  _filterType  = "All";
        private string  _groupBy     = "File";
        private bool    _isScanning  = false;
        private string  _statusMessage = "Click \"Scan Solution\" to begin.";
        private string? _solutionDir;

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public ObservableCollection<TodoItem> TodoItems
        {
            get => _todoItems;
            private set
            {
                if (SetProperty(ref _todoItems, value))
                {
                    OnPropertyChanged(nameof(FilteredItems));
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        /// <summary>Items shown in the DataGrid after applying the current filter.</summary>
        public IEnumerable<TodoItem> FilteredItems
        {
            get
            {
                var items = (IEnumerable<TodoItem>)_todoItems;

                if (_filterType != "All")
                    items = items.Where(i => i.Type == _filterType);

                items = _groupBy == "File"
                    ? items.OrderBy(i => i.FilePath).ThenBy(i => i.LineNumber)
                    : items.OrderBy(i => i.Type).ThenBy(i => i.FilePath).ThenBy(i => i.LineNumber);

                return items;
            }
        }

        public string FilterType
        {
            get => _filterType;
            set
            {
                if (SetProperty(ref _filterType, value))
                {
                    OnPropertyChanged(nameof(FilteredItems));
                    OnPropertyChanged(nameof(SummaryText));
                }
            }
        }

        public string GroupBy
        {
            get => _groupBy;
            set
            {
                if (SetProperty(ref _groupBy, value))
                    OnPropertyChanged(nameof(FilteredItems));
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                SetProperty(ref _isScanning, value);
                ScanCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string SummaryText
        {
            get
            {
                var all   = _todoItems;
                var shown = FilteredItems.ToList();
                var todo  = all.Count(i => i.Type == "TODO");
                var fixme = all.Count(i => i.Type == "FIXME");
                var hack  = all.Count(i => i.Type == "HACK");
                var note  = all.Count(i => i.Type == "NOTE");

                return $"{shown.Count} item(s) shown  |  " +
                       $"{all.Count} total: {todo} TODO, {fixme} FIXME, {hack} HACK, {note} NOTE";
            }
        }

        public static List<string> FilterTypes  => new List<string> { "All", "TODO", "FIXME", "HACK", "NOTE" };
        public static List<string> GroupByOptions => new List<string> { "File", "Type" };

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------
        public RelayCommand ScanCommand           { get; }
        public RelayCommand ExportCsvCommand      { get; }
        public RelayCommand NavigateToItemCommand  { get; }

        // -----------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------
        public TodoDashboardViewModel()
        {
            ScanCommand          = new RelayCommand(ExecuteScan,     _ => !IsScanning);
            ExportCsvCommand     = new RelayCommand(ExecuteExportCsv, _ => _todoItems.Any());
            NavigateToItemCommand = new RelayCommand(ExecuteNavigate);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>Sets the solution directory; called by the VS package after load.</summary>
        public void SetSolutionDirectory(string solutionDir) =>
            _solutionDir = solutionDir;

        // -----------------------------------------------------------------------
        // Command implementations
        // -----------------------------------------------------------------------

        private async void ExecuteScan(object? _)
        {
            IsScanning    = true;
            StatusMessage = "Scanning…";

            try
            {
                var dir = _solutionDir
                    ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

                var items = await Task.Run(() => _scanner.ScanDirectory(dir));
                TodoItems = new ObservableCollection<TodoItem>(items);

                StatusMessage   = $"Scan complete — {items.Count} item(s) found in {dir}";
                ExportCsvCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ExecuteExportCsv(object? _)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export TODO Items",
                Filter     = "CSV files|*.csv|All files|*.*",
                FileName   = $"todos_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                DefaultExt = ".csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                _exporter.ExportToCsv(FilteredItems, dlg.FileName);
                StatusMessage = $"Exported {FilteredItems.Count()} item(s) to {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        private void ExecuteNavigate(object? parameter)
        {
            if (parameter is not TodoItem item || string.IsNullOrEmpty(item.FilePath)) return;

            // Open the file at the specified line using the system default editor
            // (or VS editor when running inside the extension).
            try
            {
                // VS-hosted path: use DTE if accessible.
                // Fallback: launch the file in the OS default handler.
                if (!File.Exists(item.FilePath))
                {
                    StatusMessage = $"File not found: {item.FilePath}";
                    return;
                }

                // Try to navigate via VS automation (will succeed inside VS process).
                TryNavigateInVisualStudio(item);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Navigation failed: {ex.Message}";
            }
        }

        private void TryNavigateInVisualStudio(TodoItem item)
        {
            // When running inside VS, the DTE is available via ServiceProvider.
            // We use a late-bound reflection approach to avoid a hard compile-time
            // dependency on EnvDTE from this library project.
            try
            {
                var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(
                    typeof(Microsoft.VisualStudio.Shell.Interop.SDTE));

                if (dte == null)
                {
                    // Fallback: open with default OS application.
                    Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                    return;
                }

                dynamic dteObj = dte;
                dteObj.ItemOperations.OpenFile(item.FilePath);

                // Navigate to the specific line.
                dynamic selection = dteObj.ActiveDocument?.Selection;
                if (selection != null)
                {
                    selection.GotoLine(item.LineNumber, true);
                }

                StatusMessage = $"Navigated to {item.FileName}:{item.LineNumber}";
            }
            catch
            {
                // If VS navigation fails, at minimum open the file.
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
        }
    }
}
