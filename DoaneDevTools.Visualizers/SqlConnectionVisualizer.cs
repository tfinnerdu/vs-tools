using System;
using System.Data.SqlClient;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace DoaneDevTools.Visualizers
{
    /// <summary>Object source — runs in the debuggee process, serializes connection state.</summary>
    public class SqlConnectionVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var conn = (SqlConnection)target;
            var info = new
            {
                Server = conn.DataSource,
                Database = conn.Database,
                State = conn.State.ToString(),
                ConnectionTimeout = conn.ConnectionTimeout,
                ConnectionString = SanitizeConnectionString(conn.ConnectionString)
            };
            var json = JsonSerializer.Serialize(info);
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }

        private static string SanitizeConnectionString(string cs)
        {
            if (string.IsNullOrEmpty(cs)) return cs;
            return Regex.Replace(cs, @"(?i)(password|pwd)\s*=\s*[^;]*", "password=***",
                RegexOptions.IgnoreCase);
        }
    }

    /// <summary>Visualizer window — runs in the VS process, shows connection info.</summary>
    public class SqlConnectionVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            var window = new Window
            {
                Title = "Doane SQL Connection Inspector",
                Width = 480,
                Height = 300,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = "SQL Connection",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 56, 100)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var props = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetRow(props, 1);

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                row.Children.Add(new TextBlock
                {
                    Text = prop.Name + ":",
                    FontWeight = FontWeights.SemiBold,
                    Width = 140,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
                });

                var stateColor = prop.Name == "State" && prop.Value.GetString() == "Open"
                    ? Color.FromRgb(46, 125, 50)
                    : prop.Name == "State"
                    ? Color.FromRgb(198, 40, 40)
                    : Color.FromRgb(30, 30, 30);

                row.Children.Add(new TextBlock
                {
                    Text = prop.Value.ToString(),
                    Foreground = new SolidColorBrush(stateColor),
                    TextWrapping = TextWrapping.Wrap
                });
                props.Children.Add(row);
            }

            grid.Children.Add(props);
            window.Content = grid;
            window.ShowDialog();
        }
    }
}
