using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;

namespace DoaneDevTools.ToolWindows.DocGenerator
{
    /// <summary>
    /// Code-behind for <see cref="DocGeneratorWindowControl"/>.
    /// Routes user interactions (browse, project selection, generate) to the ViewModel.
    /// Auto-scrolls the log output as new lines are appended.
    /// </summary>
    public partial class DocGeneratorWindowControl : System.Windows.Controls.UserControl
    {
        // -----------------------------------------------------------------------
        // Shared value converters (referenced from XAML via x:Static)
        // -----------------------------------------------------------------------

        public static readonly IValueConverter InverseBoolConverter = new DocInverseBoolConverter();

        private DocGeneratorViewModel? _vm;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public DocGeneratorWindowControl()
        {
            InitializeComponent();

            _vm = new DocGeneratorViewModel();
            DataContext = _vm;

            // Auto-scroll log as it grows.
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DocGeneratorViewModel.LogOutput))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LogScroll.ScrollToBottom();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            };
        }

        // -----------------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------------

        private void RefreshProjectsButton_Click(object sender, RoutedEventArgs e)
        {
            _vm?.RefreshProjects();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ProjectList.SelectAll();
        }

        private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SelectedProjects.Clear();
            foreach (var item in ProjectList.SelectedItems)
            {
                if (item is string path)
                    _vm.SelectedProjects.Add(path);
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description  = "Select output folder for the HTML documentation site",
                SelectedPath = _vm?.OutputPath ?? string.Empty,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == DialogResult.OK && _vm != null)
                _vm.OutputPath = dialog.SelectedPath;
        }
    }

    // -----------------------------------------------------------------------
    // Value converter
    // -----------------------------------------------------------------------

    internal sealed class DocInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
    }
}
