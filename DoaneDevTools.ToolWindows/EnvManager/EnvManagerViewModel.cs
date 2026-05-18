using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using DoaneDevTools.ToolWindows.EnvManager.Models;
using DoaneDevTools.ToolWindows.EnvManager.Services;
using DoaneDevTools.ToolWindows.Infrastructure;

namespace DoaneDevTools.ToolWindows.EnvManager
{
    /// <summary>
    /// ViewModel for the Environment Manager tool window.
    /// Orchestrates profile management, file discovery, apply/validate operations,
    /// and binds directly to <see cref="EnvManagerWindowControl"/>.
    /// </summary>
    public class EnvManagerViewModel : ViewModelBase
    {
        // -----------------------------------------------------------------------
        // Services
        // -----------------------------------------------------------------------

        private readonly EnvFileService _fileService = new EnvFileService();
        private readonly ProfileStorageService _storageService = new ProfileStorageService();

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------

        private ObservableCollection<EnvProfile> _profiles = new ObservableCollection<EnvProfile>();
        private EnvProfile? _activeProfile;
        private ObservableCollection<ProjectFileNode> _projectFiles = new ObservableCollection<ProjectFileNode>();
        private ObservableCollection<EnvVariable> _activeProfileVariables = new ObservableCollection<EnvVariable>();
        private string _statusMessage = "Ready";
        private string? _solutionPath;

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        /// <summary>All named profiles available in this solution.</summary>
        public ObservableCollection<EnvProfile> Profiles
        {
            get => _profiles;
            private set => SetProperty(ref _profiles, value);
        }

        /// <summary>The currently selected profile whose variables are shown in the grid.</summary>
        public EnvProfile? ActiveProfile
        {
            get => _activeProfile;
            set
            {
                if (SetProperty(ref _activeProfile, value))
                    RefreshActiveProfileVariables();
            }
        }

        /// <summary>Project/file nodes displayed in the left-panel TreeView.</summary>
        public ObservableCollection<ProjectFileNode> ProjectFiles
        {
            get => _projectFiles;
            private set => SetProperty(ref _projectFiles, value);
        }

        /// <summary>Variables of the active profile, shown in the DataGrid.</summary>
        public ObservableCollection<EnvVariable> ActiveProfileVariables
        {
            get => _activeProfileVariables;
            private set => SetProperty(ref _activeProfileVariables, value);
        }

        /// <summary>Message shown in the status bar at the bottom of the window.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------

        public RelayCommand NewProfileCommand       { get; }
        public RelayCommand DuplicateProfileCommand { get; }
        public RelayCommand DeleteProfileCommand    { get; }
        public RelayCommand ApplyToAllCommand       { get; }
        public RelayCommand ValidateCommand         { get; }
        public RelayCommand ExportSchemaCommand     { get; }
        public RelayCommand AddVariableCommand      { get; }
        public RelayCommand ImportEnvCommand        { get; }

        // -----------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------

        public EnvManagerViewModel()
        {
            NewProfileCommand       = new RelayCommand(ExecuteNewProfile);
            DuplicateProfileCommand = new RelayCommand(ExecuteDuplicateProfile, CanOperateOnProfile);
            DeleteProfileCommand    = new RelayCommand(ExecuteDeleteProfile,    CanOperateOnProfile);
            ApplyToAllCommand       = new RelayCommand(ExecuteApplyToAll,       CanOperateOnProfile);
            ValidateCommand         = new RelayCommand(ExecuteValidate,         CanOperateOnProfile);
            ExportSchemaCommand     = new RelayCommand(ExecuteExportSchema);
            AddVariableCommand      = new RelayCommand(ExecuteAddVariable,      CanOperateOnProfile);
            ImportEnvCommand        = new RelayCommand(ExecuteImportEnv,        CanOperateOnProfile);

            // Seed with default profiles so the window is usable out of the box.
            SeedDefaultProfiles();
        }

        // -----------------------------------------------------------------------
        // Public entry point
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the VS package knows the solution path. Discovers all
        /// relevant config files and loads stored profiles from disk.
        /// </summary>
        public void LoadFromSolution(string solutionPath)
        {
            _solutionPath = solutionPath;

            try
            {
                var loaded = _storageService.LoadProfiles(solutionPath);
                if (loaded.Any())
                {
                    Profiles = new ObservableCollection<EnvProfile>(loaded);
                    ActiveProfile = Profiles.First();
                }

                DiscoverProjectFiles(Path.GetDirectoryName(solutionPath)!);
                StatusMessage = $"Loaded {Profiles.Count} profile(s) from solution.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading profiles: {ex.Message}";
            }
        }

        // -----------------------------------------------------------------------
        // Profile file discovery
        // -----------------------------------------------------------------------

        private void DiscoverProjectFiles(string solutionDir)
        {
            var nodes = new ObservableCollection<ProjectFileNode>();

            // Walk first-level subdirectories as project folders.
            foreach (var dir in Directory.EnumerateDirectories(solutionDir)
                                         .Where(d => !IsHiddenOrSystem(d))
                                         .OrderBy(d => d))
            {
                var folderNode = new ProjectFileNode
                {
                    Name     = Path.GetFileName(dir),
                    FullPath = dir,
                    FileType = ConfigFileType.Folder
                };

                // Search for known config file patterns in this project directory.
                FindConfigFiles(dir, folderNode);

                if (folderNode.Children.Any())
                    nodes.Add(folderNode);
            }

            ProjectFiles = nodes;
        }

        private static void FindConfigFiles(string directory, ProjectFileNode parent)
        {
            // .env files
            foreach (var f in SafeGetFiles(directory, ".env"))
                parent.Children.Add(MakeFileNode(f, ConfigFileType.DotEnv));

            // appsettings*.json
            foreach (var f in SafeGetFiles(directory, "appsettings*.json"))
                parent.Children.Add(MakeFileNode(f, ConfigFileType.AppSettings));

            // start-local.ps1
            foreach (var f in SafeGetFiles(directory, "start-local.ps1"))
                parent.Children.Add(MakeFileNode(f, ConfigFileType.StartLocalPs1));

            // K8s secret YAML — files named *secret*.yaml / *secret*.yml
            foreach (var f in SafeGetFiles(directory, "*secret*.yaml")
                               .Concat(SafeGetFiles(directory, "*secret*.yml")))
                parent.Children.Add(MakeFileNode(f, ConfigFileType.K8sSecret));

            // docker-compose*.yml
            foreach (var f in SafeGetFiles(directory, "docker-compose*.yml")
                               .Concat(SafeGetFiles(directory, "docker-compose*.yaml")))
                parent.Children.Add(MakeFileNode(f, ConfigFileType.DockerCompose));

            // Recurse one level deeper for services in subdirectories.
            foreach (var sub in Directory.EnumerateDirectories(directory)
                                         .Where(d => !IsHiddenOrSystem(d)))
            {
                var subNode = new ProjectFileNode
                {
                    Name     = Path.GetFileName(sub),
                    FullPath = sub,
                    FileType = ConfigFileType.Folder
                };
                FindConfigFiles(sub, subNode);
                if (subNode.Children.Any())
                    parent.Children.Add(subNode);
            }
        }

        private static ProjectFileNode MakeFileNode(string path, ConfigFileType type) =>
            new ProjectFileNode
            {
                Name     = Path.GetFileName(path),
                FullPath = path,
                FileType = type,
                IsLinked = true   // linked by default when discovered
            };

        private static System.Collections.Generic.IEnumerable<string> SafeGetFiles(
            string dir, string pattern)
        {
            try { return Directory.GetFiles(dir, pattern); }
            catch { return System.Array.Empty<string>(); }
        }

        private static bool IsHiddenOrSystem(string path)
        {
            var name = Path.GetFileName(path);
            return name.StartsWith(".", StringComparison.Ordinal)
                || name.Equals(".vs",    StringComparison.OrdinalIgnoreCase)
                || name.Equals(".git",   StringComparison.OrdinalIgnoreCase)
                || name.Equals("bin",    StringComparison.OrdinalIgnoreCase)
                || name.Equals("obj",    StringComparison.OrdinalIgnoreCase)
                || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".venv",  StringComparison.OrdinalIgnoreCase)
                || name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------------
        // Apply profile to all linked files
        // -----------------------------------------------------------------------

        /// <summary>
        /// Writes the active profile's variables to every linked file in the tree.
        /// </summary>
        public void ApplyProfileToAllFiles()
        {
            if (ActiveProfile == null) return;

            var values = ActiveProfile.ToDictionary();
            int applied = 0;
            int errors  = 0;

            foreach (var fileNode in GetAllFileNodes())
            {
                if (!fileNode.IsLinked || string.IsNullOrEmpty(fileNode.FullPath)) continue;

                try
                {
                    switch (fileNode.FileType)
                    {
                        case ConfigFileType.DotEnv:
                            _fileService.WriteDotEnv(fileNode.FullPath, values);
                            break;
                        case ConfigFileType.AppSettings:
                            _fileService.WriteAppSettings(fileNode.FullPath, values);
                            break;
                        case ConfigFileType.StartLocalPs1:
                            _fileService.WriteStartLocalPs1(fileNode.FullPath, values);
                            break;
                        case ConfigFileType.K8sSecret:
                            _fileService.WriteK8sSecret(fileNode.FullPath, values);
                            break;
                        // DockerCompose handled via .env convention
                    }
                    applied++;
                }
                catch (Exception ex)
                {
                    errors++;
                    // Log but continue with remaining files.
                    System.Diagnostics.Debug.WriteLine(
                        $"[EnvManager] Error writing {fileNode.FullPath}: {ex.Message}");
                }
            }

            StatusMessage = errors == 0
                ? $"Applied \"{ActiveProfile.Name}\" to {applied} file(s)."
                : $"Applied to {applied} file(s) with {errors} error(s). Check Output window.";

            SaveProfiles();
        }

        // -----------------------------------------------------------------------
        // Validate
        // -----------------------------------------------------------------------

        /// <summary>
        /// Cross-checks that every key in the active profile exists in every linked file,
        /// and flags any linked files that define keys not in the profile.
        /// </summary>
        public void ValidateProfile()
        {
            if (ActiveProfile == null) return;

            var profileKeys = ActiveProfile.Variables
                .Select(v => v.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing  = new System.Collections.Generic.List<string>();
            var orphaned = new System.Collections.Generic.List<string>();

            foreach (var fileNode in GetAllFileNodes().Where(n => n.IsLinked && !string.IsNullOrEmpty(n.FullPath)))
            {
                try
                {
                    System.Collections.Generic.Dictionary<string, string> fileVars;
                    switch (fileNode.FileType)
                    {
                        case ConfigFileType.DotEnv:
                            fileVars = _fileService.ReadDotEnv(fileNode.FullPath!);
                            break;
                        case ConfigFileType.AppSettings:
                            fileVars = _fileService.ReadAppSettings(fileNode.FullPath!);
                            break;
                        case ConfigFileType.StartLocalPs1:
                            fileVars = _fileService.ReadStartLocalPs1(fileNode.FullPath!);
                            break;
                        case ConfigFileType.K8sSecret:
                            fileVars = _fileService.ReadK8sSecret(fileNode.FullPath!);
                            break;
                        default:
                            continue;
                    }

                    foreach (var key in profileKeys.Except(fileVars.Keys, StringComparer.OrdinalIgnoreCase))
                        missing.Add($"{Path.GetFileName(fileNode.FullPath)}: missing [{key}]");

                    foreach (var key in fileVars.Keys.Except(profileKeys, StringComparer.OrdinalIgnoreCase))
                        orphaned.Add($"{Path.GetFileName(fileNode.FullPath)}: extra [{key}]");
                }
                catch (Exception ex)
                {
                    missing.Add($"{Path.GetFileName(fileNode.FullPath)}: read error — {ex.Message}");
                }
            }

            if (!missing.Any() && !orphaned.Any())
            {
                StatusMessage = $"Validation passed — profile \"{ActiveProfile.Name}\" is consistent.";
                return;
            }

            var sb = new System.Text.StringBuilder();
            if (missing.Any())
            {
                sb.Append($"{missing.Count} missing key(s): ");
                sb.Append(string.Join("; ", missing.Take(3)));
                if (missing.Count > 3) sb.Append($" (+{missing.Count - 3} more)");
            }
            if (orphaned.Any())
            {
                if (sb.Length > 0) sb.Append("  |  ");
                sb.Append($"{orphaned.Count} extra key(s) in files");
            }

            StatusMessage = sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Command implementations
        // -----------------------------------------------------------------------

        private void ExecuteNewProfile(object? _)
        {
            var name = PromptForName("New Profile", "Enter profile name:", "NEW");
            if (string.IsNullOrWhiteSpace(name)) return;

            var profile = new EnvProfile { Name = name };
            Profiles.Add(profile);
            ActiveProfile = profile;
            SaveProfiles();
            StatusMessage = $"Created profile \"{name}\".";
        }

        private void ExecuteDuplicateProfile(object? _)
        {
            if (ActiveProfile == null) return;

            var name = PromptForName("Duplicate Profile",
                "Enter name for the duplicate:", $"{ActiveProfile.Name}_COPY");
            if (string.IsNullOrWhiteSpace(name)) return;

            var copy = ActiveProfile.Duplicate(name);
            Profiles.Add(copy);
            ActiveProfile = copy;
            SaveProfiles();
            StatusMessage = $"Duplicated \"{ActiveProfile.Name}\" → \"{name}\".";
        }

        private void ExecuteDeleteProfile(object? _)
        {
            if (ActiveProfile == null) return;

            var result = MessageBox.Show(
                $"Delete profile \"{ActiveProfile.Name}\"? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var removed = ActiveProfile;
            Profiles.Remove(removed);
            ActiveProfile = Profiles.FirstOrDefault();
            SaveProfiles();
            StatusMessage = $"Deleted profile \"{removed.Name}\".";
        }

        private void ExecuteApplyToAll(object? _)  => ApplyProfileToAllFiles();
        private void ExecuteValidate(object? _)    => ValidateProfile();

        private void ExecuteExportSchema(object? _)
        {
            if (_solutionPath == null) return;
            try
            {
                _storageService.ExportSchema(_solutionPath, Profiles);
                StatusMessage = "Exported devtools.profiles.schema.json.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        private void ExecuteAddVariable(object? _)
        {
            if (ActiveProfile == null) return;

            var variable = new EnvVariable { Key = "NEW_KEY", Value = string.Empty };
            ActiveProfile.Variables.Add(variable);
            ActiveProfileVariables.Add(variable);
            StatusMessage = "Added new variable. Edit the key and value in the grid.";
        }

        private void ExecuteImportEnv(object? _)
        {
            if (ActiveProfile == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Import .env File",
                Filter = ".env files|.env;*.env|All files|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var imported = _fileService.ReadDotEnv(dlg.FileName);
                int added = 0, updated = 0;

                foreach (var kv in imported)
                {
                    var existing = ActiveProfile.GetVariable(kv.Key);
                    if (existing != null)
                    {
                        existing.Value = kv.Value;
                        updated++;
                    }
                    else
                    {
                        var newVar = new EnvVariable { Key = kv.Key, Value = kv.Value };
                        ActiveProfile.Variables.Add(newVar);
                        ActiveProfileVariables.Add(newVar);
                        added++;
                    }
                }

                SaveProfiles();
                StatusMessage = $"Imported {added} new + {updated} updated variable(s) from {Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
        }

        private bool CanOperateOnProfile(object? _) => ActiveProfile != null;

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void RefreshActiveProfileVariables()
        {
            ActiveProfileVariables = ActiveProfile != null
                ? new ObservableCollection<EnvVariable>(ActiveProfile.Variables)
                : new ObservableCollection<EnvVariable>();

            RaiseCommandCanExecuteChanged();
        }

        private void RaiseCommandCanExecuteChanged()
        {
            DuplicateProfileCommand.RaiseCanExecuteChanged();
            DeleteProfileCommand.RaiseCanExecuteChanged();
            ApplyToAllCommand.RaiseCanExecuteChanged();
            ValidateCommand.RaiseCanExecuteChanged();
            AddVariableCommand.RaiseCanExecuteChanged();
            ImportEnvCommand.RaiseCanExecuteChanged();
        }

        private System.Collections.Generic.IEnumerable<ProjectFileNode> GetAllFileNodes()
        {
            foreach (var root in ProjectFiles)
            {
                foreach (var node in FlattenNodes(root))
                    yield return node;
            }
        }

        private static System.Collections.Generic.IEnumerable<ProjectFileNode> FlattenNodes(
            ProjectFileNode node)
        {
            if (!node.IsFolder)
                yield return node;

            foreach (var child in node.Children)
            {
                foreach (var n in FlattenNodes(child))
                    yield return n;
            }
        }

        private void SaveProfiles()
        {
            if (_solutionPath == null) return;
            try { _storageService.SaveProfiles(_solutionPath, Profiles); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnvManager] Save failed: {ex.Message}");
            }
        }

        private void SeedDefaultProfiles()
        {
            var dev = new EnvProfile
            {
                Name = "DEV",
                Variables = new System.Collections.Generic.List<EnvVariable>
                {
                    new EnvVariable { Key = "FLASK_ENV",          Value = "development" },
                    new EnvVariable { Key = "ASPNETCORE_ENVIRONMENT", Value = "Development" },
                    new EnvVariable { Key = "CONNECTION_STRING",  Value = "Server=localhost;Database=dev_db;Trusted_Connection=True;" },
                    new EnvVariable { Key = "SECRET_KEY",         Value = "dev-only-secret" },
                }
            };

            var qa = new EnvProfile
            {
                Name = "QA",
                Variables = new System.Collections.Generic.List<EnvVariable>
                {
                    new EnvVariable { Key = "FLASK_ENV",          Value = "testing" },
                    new EnvVariable { Key = "ASPNETCORE_ENVIRONMENT", Value = "Staging" },
                    new EnvVariable { Key = "CONNECTION_STRING",  Value = "Server=qa-sql;Database=qa_db;Trusted_Connection=True;" },
                    new EnvVariable { Key = "SECRET_KEY",         Value = "qa-secret-change-me" },
                }
            };

            Profiles.Add(dev);
            Profiles.Add(qa);
            ActiveProfile = dev;
        }

        /// <summary>
        /// Simple modal prompt for naming a profile.
        /// Uses a lightweight WPF window rather than a VS dialog to avoid
        /// a hard dependency on the VS shell in design-time.
        /// </summary>
        private static string? PromptForName(string title, string label, string defaultValue)
        {
            var win = new Window
            {
                Title                 = title,
                Width                 = 360,
                Height                = 140,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode            = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });

            var tb = new TextBox { Text = defaultValue };
            tb.SelectAll();
            stack.Children.Add(tb);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            bool ok = false;
            var okBtn = new Button
            {
                Content    = "OK",
                IsDefault  = true,
                Width      = 70,
                Margin     = new Thickness(0, 0, 6, 0)
            };
            okBtn.Click += (_, __) => { ok = true; win.Close(); };

            var cancelBtn = new Button { Content = "Cancel", IsCancel = true, Width = 70 };
            cancelBtn.Click += (_, __) => win.Close();

            btnRow.Children.Add(okBtn);
            btnRow.Children.Add(cancelBtn);
            stack.Children.Add(btnRow);

            win.Content = stack;
            win.ShowDialog();

            return ok ? tb.Text.Trim() : null;
        }
    }
}
