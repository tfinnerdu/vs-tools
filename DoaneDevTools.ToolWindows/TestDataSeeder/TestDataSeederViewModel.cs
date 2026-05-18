using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DoaneDevTools.ToolWindows.Shared;
using DoaneDevTools.ToolWindows.TestDataSeeder.Models;
using DoaneDevTools.ToolWindows.TestDataSeeder.Services;

namespace DoaneDevTools.ToolWindows.TestDataSeeder
{
    public class TestDataSeederViewModel : INotifyPropertyChanged
    {
        private string _selectedConnection = string.Empty;
        private string _selectedDatabase = string.Empty;
        private int _defaultSeedCount = 100;
        private string _statusMessage = "Ready";
        private string _logOutput = string.Empty;
        private bool _isSeeding;
        private double _seedProgress;
        private CancellationTokenSource? _cts;

        public ObservableCollection<string> SavedConnections { get; } = new();
        public ObservableCollection<string> Databases { get; } = new();
        public ObservableCollection<TableSeedConfig> Tables { get; } = new();
        public ObservableCollection<string> CircularDependencyWarnings { get; } = new();

        public Array AvailableStrategies { get; } = Enum.GetValues(typeof(SeedStrategy));

        public string SelectedConnection
        {
            get => _selectedConnection;
            set { _selectedConnection = value; OnPropertyChanged(nameof(SelectedConnection)); }
        }

        public string SelectedDatabase
        {
            get => _selectedDatabase;
            set { _selectedDatabase = value; OnPropertyChanged(nameof(SelectedDatabase)); }
        }

        public int DefaultSeedCount
        {
            get => _defaultSeedCount;
            set { _defaultSeedCount = value; OnPropertyChanged(nameof(DefaultSeedCount)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public string LogOutput
        {
            get => _logOutput;
            set { _logOutput = value; OnPropertyChanged(nameof(LogOutput)); }
        }

        public bool IsSeeding
        {
            get => _isSeeding;
            set { _isSeeding = value; OnPropertyChanged(nameof(IsSeeding)); }
        }

        public double SeedProgress
        {
            get => _seedProgress;
            set { _seedProgress = value; OnPropertyChanged(nameof(SeedProgress)); }
        }

        public bool HasCircularWarnings => CircularDependencyWarnings.Count > 0;

        public ICommand LoadSchemaCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand SeedCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand ExportScriptCommand { get; }

        public TestDataSeederViewModel()
        {
            LoadSchemaCommand = new RelayCommand(async _ => await LoadSchemaAsync());
            PreviewCommand = new RelayCommand(async _ => await RunPreviewAsync());
            SeedCommand = new RelayCommand(async _ => await RunSeedAsync(), _ => !IsSeeding);
            ClearAllCommand = new RelayCommand(_ => ClearAll());
            ExportScriptCommand = new RelayCommand(async _ => await ExportScriptAsync());

            // Populate with any connections saved in VS settings (placeholder)
            SavedConnections.Add("(local)");
            SavedConnections.Add("Server=dev-sql;Database=master");
        }

        private async Task LoadSchemaAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedConnection)) return;

            StatusMessage = "Loading schema...";
            try
            {
                var connStr = BuildConnectionString();
                var inspector = new SchemaInspectorService(connStr);
                var tables = await inspector.GetTablesAsync();

                Tables.Clear();
                CircularDependencyWarnings.Clear();

                foreach (var t in tables)
                {
                    t.SeedCount = DefaultSeedCount;
                    Tables.Add(t);
                }

                var (_, cycles) = TopologicalSortService.Sort(Tables);
                foreach (var c in cycles)
                    CircularDependencyWarnings.Add(c);

                OnPropertyChanged(nameof(HasCircularWarnings));
                StatusMessage = $"Loaded {Tables.Count} tables.";
                Log($"[INFO] Schema loaded: {Tables.Count} tables found.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Schema load failed.";
                Log($"[ERROR] {ex.Message}");
            }
        }

        private async Task RunPreviewAsync()
        {
            LogOutput = string.Empty;
            var connStr = BuildConnectionString();
            var seeder = new SqlSeedService(connStr);
            seeder.LogMessage += Log;
            await seeder.SeedAsync(Tables.ToList(), previewOnly: true);
        }

        private async Task RunSeedAsync()
        {
            IsSeeding = true;
            SeedProgress = 0;
            _cts = new CancellationTokenSource();
            LogOutput = string.Empty;

            var connStr = BuildConnectionString();
            var seeder = new SqlSeedService(connStr);
            seeder.LogMessage += msg =>
            {
                Log(msg);
                SeedProgress = Math.Min(SeedProgress + (100.0 / Math.Max(Tables.Count, 1)), 100);
            };

            try
            {
                await seeder.SeedAsync(Tables.ToList(), previewOnly: false, _cts.Token);
                StatusMessage = "Seeding complete.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Seeding cancelled.";
                Log("[INFO] Seeding cancelled by user.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Seeding failed.";
                Log($"[ERROR] {ex.Message}");
            }
            finally
            {
                IsSeeding = false;
                SeedProgress = 100;
            }
        }

        private void ClearAll()
        {
            foreach (var t in Tables)
                t.IsEnabled = false;
        }

        private async Task ExportScriptAsync()
        {
            var connStr = BuildConnectionString();
            var seeder = new SqlSeedService(connStr);
            var script = await seeder.ExportSqlScriptAsync(Tables.ToList());

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "SQL files|*.sql|All files|*.*",
                FileName = $"seed_{DateTime.Now:yyyyMMdd_HHmmss}.sql"
            };

            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, script, Encoding.UTF8);
                StatusMessage = $"Script exported to {Path.GetFileName(dlg.FileName)}";
            }
        }

        private string BuildConnectionString()
        {
            if (SelectedConnection.Contains("="))
                return string.IsNullOrEmpty(SelectedDatabase)
                    ? SelectedConnection
                    : $"{SelectedConnection};Initial Catalog={SelectedDatabase}";

            return $"Server={SelectedConnection};Database={SelectedDatabase};Integrated Security=True;";
        }

        private void Log(string message)
        {
            LogOutput += $"{DateTime.Now:HH:mm:ss} {message}\n";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
