using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Web.WebView2.Core;

namespace DoaneDevTools.ToolWindows.CssInspector
{
    /// <summary>
    /// Code-behind for <see cref="CssInspectorWindowControl"/>.
    ///
    /// Responsibilities:
    ///   1. Initialises WebView2 and navigates to the selected HTML file.
    ///   2. After navigation, injects a JavaScript bridge that posts
    ///      element-click events back to the host via window.chrome.webview.postMessage.
    ///   3. Receives bridge messages and routes them to the ViewModel.
    ///   4. "Apply to preview" — executes JS to mutate the selected element's style.
    ///   5. "Write back to file" — performs regex-based CSS text replacement.
    ///   6. "Copy as CSS" — puts generated CSS on the clipboard.
    /// </summary>
    public partial class CssInspectorWindowControl : UserControl
    {
        // -----------------------------------------------------------------------
        // Shared value converters (referenced from XAML via x:Static)
        // -----------------------------------------------------------------------

        public static readonly IValueConverter InverseBoolConverter = new CssInverseBoolConverter();
        public static readonly IValueConverter BoolToVisConverter   = new CssBoolToVisConverter();

        // -----------------------------------------------------------------------
        // JavaScript bridge injected after each navigation
        // -----------------------------------------------------------------------

        /// <summary>
        /// Injected into every page after load.  Adds a click listener that
        /// serialises the clicked element's tag, id, class, selector, computed
        /// dimensions, and computed styles, then posts the JSON blob to the
        /// WebView2 host via <c>window.chrome.webview.postMessage</c>.
        /// </summary>
        private const string BridgeScript = @"
(function() {
    // Remove any previous listener to avoid duplicate registrations on reload.
    if (window.__doaneBridgeInstalled) return;
    window.__doaneBridgeInstalled = true;

    // Highlight overlay
    var overlay = document.createElement('div');
    overlay.style.cssText = 'position:fixed;pointer-events:none;z-index:999999;' +
        'outline:2px solid #FF7900;background:rgba(255,121,0,0.08);';
    document.body.appendChild(overlay);

    document.addEventListener('click', function(e) {
        var el = e.target;
        e.preventDefault();
        e.stopPropagation();

        // Build a simple CSS selector.
        var selector = el.tagName.toLowerCase();
        if (el.id)        selector += '#' + el.id;
        if (el.className) selector += '.' + el.className.trim().replace(/\s+/g, '.');

        // Collect computed style for the most-common layout properties.
        var cs = window.getComputedStyle(el);
        var props = ['padding','margin','color','font-size','background-color',
                     'display','width','height','border'];
        var styleMap = {};
        props.forEach(function(p) { styleMap[p] = cs.getPropertyValue(p); });

        // Collect matched CSS rules.
        var rules = [];
        for (var si = 0; si < document.styleSheets.length; si++) {
            try {
                var sheet = document.styleSheets[si];
                for (var ri = 0; ri < sheet.cssRules.length; ri++) {
                    var rule = sheet.cssRules[ri];
                    if (rule.selectorText && el.matches(rule.selectorText)) {
                        rules.push({ selector: rule.selectorText, declaration: rule.style.cssText });
                    }
                }
            } catch(ex) {}
        }

        // Move highlight overlay.
        var rect = el.getBoundingClientRect();
        overlay.style.left   = rect.left   + 'px';
        overlay.style.top    = rect.top    + 'px';
        overlay.style.width  = rect.width  + 'px';
        overlay.style.height = rect.height + 'px';

        var payload = {
            type:      'elementSelected',
            tagName:   el.tagName,
            id:        el.id,
            className: el.className,
            selector:  selector,
            width:     Math.round(rect.width),
            height:    Math.round(rect.height),
            styles:    styleMap,
            rules:     rules
        };

        window.chrome.webview.postMessage(JSON.stringify(payload));
    }, true);
})();
";

        private CssInspectorViewModel? _vm;
        private bool _webViewReady;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public CssInspectorWindowControl()
        {
            InitializeComponent();

            _vm = new CssInspectorViewModel();
            DataContext = _vm;

            // Initialise WebView2 asynchronously.
            _ = InitialiseWebViewAsync();

            // Navigate when the selected file changes.
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CssInspectorViewModel.SelectedFile) &&
                    _webViewReady && !string.IsNullOrEmpty(_vm.SelectedFile))
                {
                    NavigateToFile(_vm.SelectedFile);
                }
            };
        }

        // -----------------------------------------------------------------------
        // WebView2 initialisation
        // -----------------------------------------------------------------------

        private async Task InitialiseWebViewAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                await PreviewWebView.EnsureCoreWebView2Async(env);

                // Subscribe to messages from the JavaScript bridge.
                PreviewWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewReady = true;

                if (_vm != null && !string.IsNullOrEmpty(_vm.SelectedFile))
                    NavigateToFile(_vm.SelectedFile);
            }
            catch (Exception ex)
            {
                if (_vm != null)
                    _vm.StatusText = $"WebView2 initialisation failed: {ex.Message}";
            }
        }

        private void NavigateToFile(string path)
        {
            if (!File.Exists(path)) return;
            PreviewWebView.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
        }

        // -----------------------------------------------------------------------
        // Navigation completed → inject bridge script
        // -----------------------------------------------------------------------

        private async void PreviewWebView_NavigationCompleted(object sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            try
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync(BridgeScript);
                if (_vm != null) _vm.StatusText = "Ready. Click an element in the preview.";
            }
            catch (Exception ex)
            {
                if (_vm != null) _vm.StatusText = $"Script injection failed: {ex.Message}";
            }
        }

        // -----------------------------------------------------------------------
        // WebView2 → host message handler
        // -----------------------------------------------------------------------

        private void OnWebMessageReceived(object? sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (_vm == null) return;

            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Minimal manual JSON parse to avoid a full JSON library dependency.
                    _vm.SelectedElement = ParseElementInfo(json);
                    _vm.AppliedRules.Clear();

                    foreach (var rule in ParseRules(json))
                        _vm.AppliedRules.Add(rule);

                    // Pre-fill the live-edit fields from computed styles.
                    _vm.LiveEditProperties.Padding    = ExtractStyle(json, "padding");
                    _vm.LiveEditProperties.Margin     = ExtractStyle(json, "margin");
                    _vm.LiveEditProperties.Color      = ExtractStyle(json, "color");
                    _vm.LiveEditProperties.FontSize   = ExtractStyle(json, "font-size");
                    _vm.LiveEditProperties.Background = ExtractStyle(json, "background-color");
                }
                catch (Exception ex)
                {
                    _vm.StatusText = $"Message parse error: {ex.Message}";
                }
            });
        }

        // -----------------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------------

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webViewReady && _vm != null && !string.IsNullOrEmpty(_vm.SelectedFile))
                NavigateToFile(_vm.SelectedFile);
        }

        private void ViewportCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_webViewReady || ViewportCombo.SelectedItem is not ComboBoxItem item) return;

            var tag = item.Tag?.ToString();
            if (string.IsNullOrEmpty(tag) || tag == "custom") return;

            var parts = tag.Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int w) &&
                int.TryParse(parts[1], out int h))
            {
                _ = PreviewWebView.CoreWebView2.ExecuteScriptAsync(
                    $"document.body.style.width='{w}px';document.body.style.height='{h}px';");
            }
        }

        private async void ApplyToPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _vm.SelectedElement == null || !_webViewReady) return;

            var selector = _vm.SelectedElement.Selector ?? "*";
            var props    = _vm.LiveEditProperties;

            // Build a JS snippet that mutates the element's inline style.
            var script = $@"
(function() {{
    var el = document.querySelector({EscapeJsString(selector)});
    if (!el) return;
    if ({JsStr(props.Padding)})    el.style.padding    = {JsStr(props.Padding)};
    if ({JsStr(props.Margin)})     el.style.margin     = {JsStr(props.Margin)};
    if ({JsStr(props.Color)})      el.style.color      = {JsStr(props.Color)};
    if ({JsStr(props.FontSize)})   el.style.fontSize   = {JsStr(props.FontSize)};
    if ({JsStr(props.Background)}) el.style.background = {JsStr(props.Background)};
}})();";

            try
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync(script);
                _vm.StatusText = "Styles applied to preview.";
            }
            catch (Exception ex)
            {
                _vm.StatusText = $"Apply failed: {ex.Message}";
            }
        }

        private void WriteBackToFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || string.IsNullOrEmpty(_vm.SelectedFile)) return;

            var cssPath = FindLinkedCssFile(_vm.SelectedFile);
            if (cssPath == null || !File.Exists(cssPath))
            {
                _vm.StatusText = "No linked CSS file found. Inline styles only.";
                return;
            }

            var selector  = _vm.SelectedElement?.Selector;
            if (string.IsNullOrEmpty(selector)) return;

            var props = _vm.LiveEditProperties;
            var newBlock = BuildCssBlock(selector, props);

            try
            {
                var existing = File.ReadAllText(cssPath);

                // If the selector already exists, replace its block.
                var escaped   = Regex.Escape(selector);
                var blockPattern = $@"{escaped}\s*\{{[^}}]*\}}";

                string updated;
                if (Regex.IsMatch(existing, blockPattern))
                {
                    updated = Regex.Replace(existing, blockPattern, newBlock);
                }
                else
                {
                    updated = existing + Environment.NewLine + newBlock;
                }

                File.WriteAllText(cssPath, updated);
                _vm.StatusText = $"Written to {Path.GetFileName(cssPath)}.";
            }
            catch (Exception ex)
            {
                _vm.StatusText = $"Write failed: {ex.Message}";
            }
        }

        private void CopyAsCssButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var selector = _vm.SelectedElement?.Selector ?? "element";
            var css = BuildCssBlock(selector, _vm.LiveEditProperties);
            Clipboard.SetText(css);
            _vm.StatusText = "CSS copied to clipboard.";
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static string BuildCssBlock(string selector, LiveCssProperties props)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{selector} {{");
            if (!string.IsNullOrWhiteSpace(props.Padding))    sb.AppendLine($"    padding: {props.Padding};");
            if (!string.IsNullOrWhiteSpace(props.Margin))     sb.AppendLine($"    margin: {props.Margin};");
            if (!string.IsNullOrWhiteSpace(props.Color))      sb.AppendLine($"    color: {props.Color};");
            if (!string.IsNullOrWhiteSpace(props.FontSize))   sb.AppendLine($"    font-size: {props.FontSize};");
            if (!string.IsNullOrWhiteSpace(props.Background)) sb.AppendLine($"    background: {props.Background};");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Finds the first linked .css href relative to the HTML file.</summary>
        private static string? FindLinkedCssFile(string htmlPath)
        {
            try
            {
                var html  = File.ReadAllText(htmlPath);
                var match = Regex.Match(html, @"<link[^>]+href\s*=\s*[""']([^""']+\.css)[""']",
                    RegexOptions.IgnoreCase);
                if (!match.Success) return null;

                var href = match.Groups[1].Value;
                return Path.Combine(Path.GetDirectoryName(htmlPath)!, href);
            }
            catch
            {
                return null;
            }
        }

        // ── Minimal JSON extraction helpers ──────────────────────────────────

        private static SelectedElementInfo ParseElementInfo(string json)
        {
            return new SelectedElementInfo
            {
                TagName   = JsonString(json, "tagName"),
                Id        = JsonString(json, "id"),
                ClassName = JsonString(json, "className"),
                Selector  = JsonString(json, "selector"),
                Width     = JsonInt(json, "width"),
                Height    = JsonInt(json, "height")
            };
        }

        private static System.Collections.Generic.List<CssRule> ParseRules(string json)
        {
            var rules = new System.Collections.Generic.List<CssRule>();
            // Extract the rules array.
            var match = Regex.Match(json, @"""rules""\s*:\s*\[([^\]]*)\]", RegexOptions.Singleline);
            if (!match.Success) return rules;

            var array = match.Groups[1].Value;
            foreach (Match ruleMatch in Regex.Matches(array, @"\{[^\}]+\}", RegexOptions.Singleline))
            {
                rules.Add(new CssRule
                {
                    Selector    = JsonString(ruleMatch.Value, "selector"),
                    Declaration = JsonString(ruleMatch.Value, "declaration")
                });
            }
            return rules;
        }

        private static string ExtractStyle(string json, string propName)
        {
            // Look inside the styles object for the property.
            var match = Regex.Match(json,
                $@"""{Regex.Escape(propName)}""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string JsonString(string json, string key)
        {
            var match = Regex.Match(json, $@"""{Regex.Escape(key)}""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int JsonInt(string json, string key)
        {
            var match = Regex.Match(json, $@"""{Regex.Escape(key)}""\s*:\s*(\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out int v) ? v : 0;
        }

        private static string EscapeJsString(string s) => $"'{s.Replace("'", "\\'")}'";
        private static string JsStr(string? s) => string.IsNullOrEmpty(s) ? "''" : $"'{s}'";
    }

    // -----------------------------------------------------------------------
    // Value converters
    // -----------------------------------------------------------------------

    internal sealed class CssInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
    }

    internal sealed class CssBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }
}
