using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using DoaneDevTools.ToolWindows.Infrastructure;
using DoaneDevTools.ToolWindows.TemplateBuilder.Models;
using DoaneDevTools.ToolWindows.TemplateBuilder.Services;

namespace DoaneDevTools.ToolWindows.TemplateBuilder
{
    /// <summary>
    /// ViewModel for the Template Builder tool window.
    /// </summary>
    public class TemplateBuilderViewModel : ViewModelBase
    {
        // -----------------------------------------------------------------------
        // Services
        // -----------------------------------------------------------------------
        private readonly TemplateService _templateService = new TemplateService();

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------
        private ObservableCollection<TemplateCategoryNode> _templateCategories =
            new ObservableCollection<TemplateCategoryNode>();
        private TemplateManifest? _selectedTemplate;
        private ObservableCollection<TemplateVariableInputItem> _templateVariables =
            new ObservableCollection<TemplateVariableInputItem>();
        private string _outputPath = string.Empty;
        private string _statusMessage = "Select a template category on the left.";

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public ObservableCollection<TemplateCategoryNode> TemplateCategories
        {
            get => _templateCategories;
            private set => SetProperty(ref _templateCategories, value);
        }

        public TemplateManifest? SelectedTemplate
        {
            get => _selectedTemplate;
            private set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    RefreshTemplateVariables();
                    GenerateCommand.RaiseCanExecuteChanged();
                    PreviewCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<TemplateVariableInputItem> TemplateVariables
        {
            get => _templateVariables;
            private set => SetProperty(ref _templateVariables, value);
        }

        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------
        public RelayCommand GenerateCommand          { get; }
        public RelayCommand PreviewCommand           { get; }
        public RelayCommand BrowseOutputPathCommand  { get; }

        // -----------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------
        public TemplateBuilderViewModel()
        {
            GenerateCommand         = new RelayCommand(ExecuteGenerate,  CanGenerate);
            PreviewCommand          = new RelayCommand(ExecutePreview,   CanGenerate);
            BrowseOutputPathCommand = new RelayCommand(ExecuteBrowse);

            LoadTemplates();
        }

        // -----------------------------------------------------------------------
        // Tree-node selection (called from code-behind)
        // -----------------------------------------------------------------------
        public void SelectedTreeNode_Set(TemplateTreeNode node)
        {
            // Only leaf nodes (actual templates) are actionable.
            if (node is TemplateLeafNode leaf)
            {
                SelectedTemplate = leaf.Manifest;
                StatusMessage = $"Template selected: {leaf.Manifest.Name}";
            }
        }

        /// <summary>
        /// Property set from the XAML code-behind when the TreeView selection changes.
        /// </summary>
        public TemplateTreeNode? SelectedTreeNode
        {
            set
            {
                if (value != null) SelectedTreeNode_Set(value);
            }
        }

        // -----------------------------------------------------------------------
        // Template loading
        // -----------------------------------------------------------------------

        private void LoadTemplates()
        {
            try
            {
                var allTemplates = _templateService.GetTemplates();
                var grouped = allTemplates
                    .GroupBy(t => t.Category)
                    .OrderBy(g => g.Key);

                var categories = new ObservableCollection<TemplateCategoryNode>();
                foreach (var group in grouped)
                {
                    var cat = new TemplateCategoryNode { Name = group.Key };
                    foreach (var manifest in group)
                    {
                        cat.Templates.Add(new TemplateLeafNode
                        {
                            Name     = manifest.Name,
                            Manifest = manifest
                        });
                    }
                    categories.Add(cat);
                }

                TemplateCategories = categories;
                StatusMessage = $"Loaded {allTemplates.Count} template(s) across {categories.Count} categories.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading templates: {ex.Message}";
            }
        }

        // -----------------------------------------------------------------------
        // Variable refresh
        // -----------------------------------------------------------------------

        private void RefreshTemplateVariables()
        {
            if (SelectedTemplate == null)
            {
                TemplateVariables = new ObservableCollection<TemplateVariableInputItem>();
                return;
            }

            var items = SelectedTemplate.Variables
                .Select(v => new TemplateVariableInputItem(v))
                .ToList();

            TemplateVariables = new ObservableCollection<TemplateVariableInputItem>(items);
        }

        // -----------------------------------------------------------------------
        // Command implementations
        // -----------------------------------------------------------------------

        private bool CanGenerate(object? _) =>
            SelectedTemplate != null && !string.IsNullOrWhiteSpace(OutputPath);

        private void ExecuteGenerate(object? _)
        {
            if (SelectedTemplate == null || string.IsNullOrWhiteSpace(OutputPath)) return;

            var variables = BuildVariableDictionary();
            try
            {
                _templateService.GenerateTemplate(SelectedTemplate, variables, OutputPath);
                var files = string.Join(", ", SelectedTemplate.OutputFiles.Take(5));
                StatusMessage = $"Generated: {files}" +
                    (SelectedTemplate.OutputFiles.Count > 5 ? " ..." : string.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Generation failed: {ex.Message}";
                MessageBox.Show($"Template generation failed:\n{ex.Message}",
                    "Template Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecutePreview(object? _)
        {
            if (SelectedTemplate == null) return;

            var variables = BuildVariableDictionary();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Preview: {SelectedTemplate.Name} ===");
            sb.AppendLine($"Output path: {(string.IsNullOrWhiteSpace(OutputPath) ? "(not set)" : OutputPath)}");
            sb.AppendLine();
            sb.AppendLine("Files that will be generated:");
            foreach (var file in SelectedTemplate.OutputFiles)
                sb.AppendLine($"  • {file}");

            sb.AppendLine();
            sb.AppendLine("Variables:");
            foreach (var kv in variables)
                sb.AppendLine($"  {kv.Key} = {kv.Value}");

            // Show sample content of the first output file if available.
            try
            {
                var firstFile = SelectedTemplate.OutputFiles.FirstOrDefault();
                if (firstFile != null)
                {
                    var templatePath = Path.Combine(SelectedTemplate.TemplateDirectory, firstFile);
                    if (File.Exists(templatePath))
                    {
                        sb.AppendLine();
                        sb.AppendLine($"=== Preview of {firstFile} ===");
                        var content = _templateService.RenderTemplate(templatePath, variables);
                        // Show first 40 lines.
                        var lines = content.Split('\n').Take(40);
                        sb.AppendLine(string.Join("\n", lines));
                        if (content.Split('\n').Length > 40)
                            sb.AppendLine("... (truncated)");
                    }
                }
            }
            catch { /* Preview is best-effort */ }

            MessageBox.Show(sb.ToString(), "Template Preview",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteBrowse(object? _)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Select output folder for generated files",
                ShowNewFolderButton = true,
                SelectedPath        = string.IsNullOrWhiteSpace(OutputPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : OutputPath
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputPath = dlg.SelectedPath;
                GenerateCommand.RaiseCanExecuteChanged();
                PreviewCommand.RaiseCanExecuteChanged();
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private System.Collections.Generic.Dictionary<string, string> BuildVariableDictionary()
        {
            return TemplateVariables.ToDictionary(
                item => item.Name,
                item => item.ResolvedValue);
        }
    }
}
