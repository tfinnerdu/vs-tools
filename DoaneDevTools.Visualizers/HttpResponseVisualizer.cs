using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace DoaneDevTools.Visualizers
{
    /// <summary>
    /// Object source for HttpResponseMessage — runs in the debuggee process.
    /// Reads the response body synchronously (safe for inspection; the message
    /// is already received) and serialises status, headers, and body to JSON.
    /// </summary>
    public class HttpResponseVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var response = (HttpResponseMessage)target;

            string body;
            try
            {
                // ReadAsStringAsync is safe here because the response has already been
                // awaited by the time the debugger pauses execution.
                body = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
                       ?? string.Empty;
            }
            catch (Exception ex)
            {
                body = $"[Could not read body: {ex.Message}]";
            }

            var headerPairs = response.Headers
                .Concat(response.Content?.Headers ?? Enumerable.Empty<System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

            var info = new
            {
                StatusCode   = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                RequestUri   = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty,
                Headers      = headerPairs,
                Body         = TruncateBody(body, maxChars: 8192)
            };

            var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }

        private static string TruncateBody(string body, int maxChars)
        {
            if (body.Length <= maxChars) return body;
            return body[..maxChars] + $"\n\n[Truncated — {body.Length:N0} total characters]";
        }
    }

    /// <summary>
    /// Visualizer window for <see cref="HttpResponseMessage"/>.
    /// Shows: status code (green/red), reason phrase, request URI,
    /// headers as a key-value list, and the decoded response body.
    /// </summary>
    public class HttpResponseVisualizer : DialogDebuggerVisualizer
    {
        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        private static readonly SolidColorBrush ErrorBrush   = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        private static readonly SolidColorBrush WarnBrush    = new SolidColorBrush(Color.FromRgb(245, 127, 23));
        private static readonly SolidColorBrush TextLight    = new SolidColorBrush(Color.FromRgb(212, 212, 212));
        private static readonly SolidColorBrush TextMuted    = new SolidColorBrush(Color.FromRgb(150, 150, 150));
        private static readonly SolidColorBrush PanelBg      = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        private static readonly SolidColorBrush SecondaryBg  = new SolidColorBrush(Color.FromRgb(45, 45, 48));

        protected override void Show(
            IDialogVisualizerService windowService,
            IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var statusCode = root.TryGetProperty("StatusCode", out var sc) ? sc.GetInt32() : 0;
            var reason     = root.TryGetProperty("ReasonPhrase", out var rp) ? rp.GetString() ?? "" : "";
            var uri        = root.TryGetProperty("RequestUri",   out var ru) ? ru.GetString() ?? "" : "";
            var body       = root.TryGetProperty("Body",         out var bd) ? bd.GetString() ?? "" : "";

            var statusBrush = statusCode >= 200 && statusCode < 300 ? SuccessBrush
                            : statusCode >= 400 && statusCode < 500 ? WarnBrush
                            : ErrorBrush;

            var window = new Window
            {
                Title = $"HTTP Response — {statusCode} {reason}",
                Width  = 760,
                Height = 580,
                Background = PanelBg,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var outerGrid = new Grid();
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // status
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // uri
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) }); // headers
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // body
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // copy button

            // ── Status line ────────────────────────────────────────────────
            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 14, 16, 6),
                Background = Brushes.Transparent
            };
            statusPanel.Children.Add(new Border
            {
                Background    = statusBrush,
                CornerRadius  = new CornerRadius(4),
                Padding       = new Thickness(10, 4, 10, 4),
                Margin        = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text       = statusCode.ToString(),
                    FontSize   = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            });
            statusPanel.Children.Add(new TextBlock
            {
                Text       = reason,
                FontSize   = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = statusBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(statusPanel, 0);

            // ── Request URI ────────────────────────────────────────────────
            var uriPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 8) };
            uriPanel.Children.Add(new TextBlock { Text = "URI: ", Foreground = TextMuted, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
            uriPanel.Children.Add(new TextBlock { Text = uri, Foreground = TextLight, FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetRow(uriPanel, 1);

            // ── Headers grid ───────────────────────────────────────────────
            var headersLabel = new TextBlock { Text = "Response Headers", FontWeight = FontWeights.SemiBold, Foreground = TextMuted, FontSize = 11, Margin = new Thickness(16, 0, 16, 2) };

            var headersGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly          = true,
                Background          = SecondaryBg,
                Foreground          = TextLight,
                RowBackground       = SecondaryBg,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(38, 38, 42)),
                BorderThickness     = new Thickness(0),
                Margin              = new Thickness(16, 0, 16, 8),
                FontFamily          = new FontFamily("Consolas"),
                FontSize            = 11,
                HeadersVisibility   = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            headersGrid.Columns.Add(new DataGridTextColumn { Header = "Header",   Binding = new System.Windows.Data.Binding("Key"),   Width = new DataGridLength(200) });
            headersGrid.Columns.Add(new DataGridTextColumn { Header = "Value",    Binding = new System.Windows.Data.Binding("Value"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            if (root.TryGetProperty("Headers", out var hdrs) && hdrs.ValueKind == JsonValueKind.Object)
            {
                headersGrid.ItemsSource = hdrs.EnumerateObject()
                    .Select(p => new { Key = p.Name, Value = p.Value.GetString() })
                    .ToList();
            }

            var headersContainer = new StackPanel();
            headersContainer.Children.Add(headersLabel);
            headersContainer.Children.Add(headersGrid);
            Grid.SetRow(headersContainer, 2);

            // ── Response body ──────────────────────────────────────────────
            var bodyLabel = new TextBlock { Text = "Response Body", FontWeight = FontWeights.SemiBold, Foreground = TextMuted, FontSize = 11, Margin = new Thickness(16, 4, 16, 2) };

            var bodyBox = new TextBox
            {
                Text = body,
                IsReadOnly = true,
                Background = SecondaryBg,
                Foreground = TextLight,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(16, 0, 16, 4),
                Padding = new Thickness(8)
            };

            var bodyContainer = new DockPanel { Margin = new Thickness(0) };
            DockPanel.SetDock(bodyLabel, Dock.Top);
            bodyContainer.Children.Add(bodyLabel);
            bodyContainer.Children.Add(bodyBox);
            Grid.SetRow(bodyContainer, 3);

            // ── Copy button ────────────────────────────────────────────────
            var copyBtn = new Button
            {
                Content  = "Copy body to clipboard",
                Padding  = new Thickness(12, 6, 12, 6),
                Margin   = new Thickness(16, 4, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background   = new SolidColorBrush(Color.FromRgb(0x1F, 0x38, 0x64)),
                Foreground   = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyBtn.Click += (s, e) => Clipboard.SetText(body);
            Grid.SetRow(copyBtn, 4);

            outerGrid.Children.Add(statusPanel);
            outerGrid.Children.Add(uriPanel);
            outerGrid.Children.Add(headersContainer);
            outerGrid.Children.Add(bodyContainer);
            outerGrid.Children.Add(copyBtn);

            window.Content = outerGrid;
            window.ShowDialog();
        }
    }
}
