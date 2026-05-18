using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace DoaneDevTools.Visualizers
{
    public class ListVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var items = ((IEnumerable)target).Cast<object>().Take(500).ToList();
            var json = JsonSerializer.Serialize(new
            {
                Count = items.Count,
                Items = items.Select(SerializeItem).ToList()
            }, new JsonSerializerOptions { WriteIndented = false });

            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }

        private static object SerializeItem(object? item)
        {
            if (item == null) return "(null)";
            try { return JsonSerializer.Serialize(item); }
            catch { return item.ToString() ?? string.Empty; }
        }
    }

    public class ListVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            var count = doc.RootElement.GetProperty("Count").GetInt32();
            var items = doc.RootElement.GetProperty("Items").EnumerateArray()
                .Select(e => (object)e.GetString()!)
                .ToList();

            var window = new Window
            {
                Title = $"Doane List Inspector ({count} items)",
                Width = 700,
                Height = 500,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new TextBlock
            {
                Text = $"List<T>  —  {count} items",
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 56, 100)),
                Margin = new Thickness(12, 10, 12, 6)
            };
            Grid.SetRow(header, 0);

            // Filter box
            var filterBox = new TextBox
            {
                Margin = new Thickness(12, 0, 12, 6),
                Padding = new Thickness(4),
                Text = string.Empty
            };
            Grid.SetRow(filterBox, 0);

            // DataGrid
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Margin = new Thickness(12, 0, 12, 0),
                CanUserSortColumns = true
            };
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "#",
                Binding = new Binding("Index"),
                Width = 50
            });
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Value",
                Binding = new Binding("Value"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            var rows = items.Select((v, i) => new { Index = i, Value = v }).ToList();
            dataGrid.ItemsSource = rows;
            Grid.SetRow(dataGrid, 1);

            // Copy as JSON button
            var copyBtn = new Button
            {
                Content = "Copy as JSON",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(12, 6, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(31, 56, 100)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0)
            };
            copyBtn.Click += (s, e) =>
                Clipboard.SetText(JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            Grid.SetRow(copyBtn, 2);

            grid.Children.Add(header);
            grid.Children.Add(dataGrid);
            grid.Children.Add(copyBtn);
            window.Content = grid;
            window.ShowDialog();
        }
    }

    /// <summary>Object source for Dictionary visualizer — serializes key/value pairs to JSON.</summary>
    public class DictionaryVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var dict = (System.Collections.IDictionary)target;
            var pairs = new System.Collections.Generic.List<object>();
            foreach (System.Collections.DictionaryEntry entry in dict)
                pairs.Add(new { Key = entry.Key?.ToString() ?? "(null)", Value = entry.Value?.ToString() ?? "(null)" });

            var json = System.Text.Json.JsonSerializer.Serialize(pairs);
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }
    }

    public class DictionaryVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();

            var window = new Window
            {
                Title = "Doane Dictionary Inspector",
                Width = 700,
                Height = 480,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = "Dictionary<K, V>",
                FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 56, 100)),
                Margin = new Thickness(12, 10, 12, 6)
            };
            Grid.SetRow(header, 0);

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Margin = new Thickness(12, 0, 12, 12),
                CanUserSortColumns = true
            };
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Key",
                Binding = new Binding("Key"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Value",
                Binding = new Binding("Value"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            // Parse the JSON as Dictionary
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    dataGrid.ItemsSource = doc.RootElement.EnumerateObject()
                        .Select(p => new { Key = p.Name, Value = p.Value.ToString() })
                        .ToList();
                }
            }
            catch { /* fallback — show raw */ }

            Grid.SetRow(dataGrid, 1);
            grid.Children.Add(header);
            grid.Children.Add(dataGrid);
            window.Content = grid;
            window.ShowDialog();
        }
    }

    public class HttpResponseVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            var window = new Window
            {
                Title = "Doane HTTP Response Inspector",
                Width = 700,
                Height = 500,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            var statusCode = doc.RootElement.TryGetProperty("StatusCode", out var sc) ? sc.GetInt32() : 0;
            var isSuccess = statusCode >= 200 && statusCode < 300;

            stack.Children.Add(new TextBlock
            {
                Text = $"HTTP {statusCode} {(doc.RootElement.TryGetProperty("ReasonPhrase", out var rp) ? rp.GetString() : "")}",
                FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isSuccess ? Color.FromRgb(46, 125, 50) : Color.FromRgb(198, 40, 40)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            if (doc.RootElement.TryGetProperty("RequestUri", out var uri))
                stack.Children.Add(new TextBlock
                {
                    Text = $"URI: {uri.GetString()}",
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    FontFamily = new FontFamily("Consolas"), FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 8)
                });

            if (doc.RootElement.TryGetProperty("Body", out var body))
                stack.Children.Add(new TextBox
                {
                    Text = body.GetString(),
                    IsReadOnly = true, Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                    FontFamily = new FontFamily("Consolas"), FontSize = 12,
                    TextWrapping = TextWrapping.Wrap, MaxHeight = 300,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0),
                    Padding = new Thickness(8)
                });

            var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            window.Content = scroll;
            window.ShowDialog();
        }
    }
}
