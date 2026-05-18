using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DoaneDevTools.ToolWindows.Infrastructure;

namespace DoaneDevTools.ToolWindows.CssInspector
{
    // -----------------------------------------------------------------------
    // Supporting models
    // -----------------------------------------------------------------------

    /// <summary>Information about the HTML element currently selected in the preview.</summary>
    public class SelectedElementInfo
    {
        public string TagName   { get; set; } = string.Empty;
        public string Id        { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Selector  { get; set; } = string.Empty;
        public int    Width     { get; set; }
        public int    Height    { get; set; }
    }

    /// <summary>A single CSS rule that matches the selected element.</summary>
    public class CssRule
    {
        public string Selector    { get; set; } = string.Empty;
        public string Declaration { get; set; } = string.Empty;
    }

    /// <summary>Editable CSS property values in the Live Edit panel.</summary>
    public class LiveCssProperties : ViewModelBase
    {
        private string _padding    = string.Empty;
        private string _margin     = string.Empty;
        private string _color      = string.Empty;
        private string _fontSize   = string.Empty;
        private string _background = string.Empty;

        public string Padding    { get => _padding;    set => SetProperty(ref _padding,    value); }
        public string Margin     { get => _margin;     set => SetProperty(ref _margin,     value); }
        public string Color      { get => _color;      set => SetProperty(ref _color,      value); }
        public string FontSize   { get => _fontSize;   set => SetProperty(ref _fontSize,   value); }
        public string Background { get => _background; set => SetProperty(ref _background, value); }
    }

    // -----------------------------------------------------------------------
    // ViewModel
    // -----------------------------------------------------------------------

    /// <summary>
    /// ViewModel for the CSS Layout Inspector tool window.
    /// Holds the list of HTML files from the solution, the currently selected file,
    /// and inspector state (selected element, applied rules, live-edit fields).
    /// </summary>
    public sealed class CssInspectorViewModel : ViewModelBase
    {
        private ObservableCollection<string> _htmlFiles = new ObservableCollection<string>();
        private string?                     _selectedFile;
        private SelectedElementInfo?        _selectedElement;
        private ObservableCollection<CssRule> _appliedRules = new ObservableCollection<CssRule>();
        private LiveCssProperties           _liveEditProperties = new LiveCssProperties();
        private string                      _statusText = "Ready.";
        private bool                        _isBusy;

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        /// <summary>All HTML files found in the current solution.</summary>
        public ObservableCollection<string> HtmlFiles
        {
            get => _htmlFiles;
            set => SetProperty(ref _htmlFiles, value);
        }

        /// <summary>The HTML file currently loaded in the WebView2 preview.</summary>
        public string? SelectedFile
        {
            get => _selectedFile;
            set => SetProperty(ref _selectedFile, value);
        }

        /// <summary>The element most recently clicked in the preview.</summary>
        public SelectedElementInfo? SelectedElement
        {
            get => _selectedElement;
            set => SetProperty(ref _selectedElement, value);
        }

        /// <summary>CSS rules that match the selected element.</summary>
        public ObservableCollection<CssRule> AppliedRules
        {
            get => _appliedRules;
            set => SetProperty(ref _appliedRules, value);
        }

        /// <summary>Live-editable CSS property values bound to the edit fields.</summary>
        public LiveCssProperties LiveEditProperties
        {
            get => _liveEditProperties;
            set => SetProperty(ref _liveEditProperties, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // -----------------------------------------------------------------------
        // Host-facing API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Populates <see cref="HtmlFiles"/> from the provided root directories
        /// (e.g. solution project directories). Selects the first file found.
        /// </summary>
        public void SetSolutionPaths(IEnumerable<string> projectDirectories)
        {
            HtmlFiles.Clear();

            foreach (var dir in projectDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*.html",
                    SearchOption.AllDirectories).Take(100))
                {
                    HtmlFiles.Add(file);
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*.htm",
                    SearchOption.AllDirectories).Take(50))
                {
                    HtmlFiles.Add(file);
                }
            }

            if (HtmlFiles.Count > 0)
                SelectedFile = HtmlFiles[0];

            StatusText = $"{HtmlFiles.Count} HTML file(s) found.";
        }
    }
}
