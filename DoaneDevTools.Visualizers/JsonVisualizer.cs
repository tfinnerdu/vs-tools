using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DoaneDevTools.Visualizers
{
    /// <summary>
    /// Object source — runs in the debuggee process.
    /// Serialises the JObject / JArray / JToken to a formatted JSON string
    /// and streams it to the visualizer host.
    /// </summary>
    public class JsonVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            string json;

            try
            {
                if (target is JToken jToken)
                    json = jToken.ToString(Formatting.Indented);
                else
                    json = JsonConvert.SerializeObject(target, Formatting.Indented);
            }
            catch (Exception ex)
            {
                json = $"{{ \"error\": \"{ex.Message}\" }}";
            }

            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }
    }

    /// <summary>
    /// Visualizer window — runs in the VS debugger process.
    /// Renders the JSON as a syntax-highlighted, expandable WPF TreeView.
    /// </summary>
    public class JsonVisualizer : DialogDebuggerVisualizer
    {
        // Brand colours (Doane palette).
        private static readonly SolidColorBrush KeyBrush      = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        private static readonly SolidColorBrush StringBrush   = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
        private static readonly SolidColorBrush NumberBrush   = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));
        private static readonly SolidColorBrush BoolNullBrush = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        private static readonly SolidColorBrush NodeBg        = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        private static readonly SolidColorBrush TextDefault   = new SolidColorBrush(Color.FromRgb(212, 212, 212));

        protected override void Show(
            IDialogVisualizerService windowService,
            IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var rawJson = reader.ReadToEnd();

            var window = new Window
            {
                Title = "Doane JSON Inspector",
                Width  = 640,
                Height = 560,
                Background = NodeBg,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new TextBlock
            {
                Text = "JSON",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x79, 0x00)),
                Margin = new Thickness(12, 10, 12, 6)
            };
            Grid.SetRow(header, 0);

            // TreeView
            var treeView = new TreeView
            {
                Background = NodeBg,
                Foreground = TextDefault,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(12, 0, 12, 4)
            };
            Grid.SetRow(treeView, 1);

            try
            {
                var token = JToken.Parse(rawJson);
                var treeItem = BuildTreeItem(null, token);
                treeView.Items.Add(treeItem);
            }
            catch (JsonException ex)
            {
                treeView.Items.Add(new TreeViewItem
                {
                    Header = $"Parse error: {ex.Message}",
                    Foreground = Brushes.Red
                });
            }

            // Copy button
            var copyBtn = new Button
            {
                Content = "Copy JSON",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(12, 4, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x38, 0x64)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyBtn.Click += (s, e) => Clipboard.SetText(rawJson);
            Grid.SetRow(copyBtn, 2);

            root.Children.Add(header);
            root.Children.Add(treeView);
            root.Children.Add(copyBtn);
            window.Content = root;
            window.ShowDialog();
        }

        // -----------------------------------------------------------------------
        // Tree building
        // -----------------------------------------------------------------------

        private static TreeViewItem BuildTreeItem(string? key, JToken token)
        {
            var item = new TreeViewItem
            {
                IsExpanded = true,
                Background = Brushes.Transparent,
                Foreground = TextDefault
            };

            switch (token.Type)
            {
                case JTokenType.Object:
                    item.Header = MakeHeader(key, "{  }", "object", KeyBrush);
                    foreach (var prop in ((JObject)token).Properties())
                        item.Items.Add(BuildTreeItem(prop.Name, prop.Value));
                    break;

                case JTokenType.Array:
                    var arr = (JArray)token;
                    item.Header = MakeHeader(key, $"[  ] ({arr.Count} items)", "array", KeyBrush);
                    for (int i = 0; i < arr.Count; i++)
                        item.Items.Add(BuildTreeItem($"[{i}]", arr[i]));
                    break;

                case JTokenType.String:
                    item.Header = MakeValueHeader(key, $"\"{token.Value<string>()}\"", StringBrush);
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                    item.Header = MakeValueHeader(key, token.ToString(), NumberBrush);
                    break;

                case JTokenType.Boolean:
                    item.Header = MakeValueHeader(key, token.ToString().ToLower(), BoolNullBrush);
                    break;

                case JTokenType.Null:
                    item.Header = MakeValueHeader(key, "null", BoolNullBrush);
                    break;

                default:
                    item.Header = MakeValueHeader(key, token.ToString(), TextDefault);
                    break;
            }

            return item;
        }

        private static StackPanel MakeHeader(string? key, string typeSuffix, string typeLabel, Brush typeBrush)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            if (!string.IsNullOrEmpty(key))
            {
                sp.Children.Add(new TextBlock
                {
                    Text       = $"\"{key}\"",
                    Foreground = KeyBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 12
                });
                sp.Children.Add(new TextBlock
                {
                    Text       = ": ",
                    Foreground = TextDefault,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 12
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text       = typeSuffix,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 12
            });

            sp.Children.Add(new TextBlock
            {
                Text       = $" // {typeLabel}",
                Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55)),
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 11,
                Margin     = new Thickness(6, 0, 0, 0)
            });

            return sp;
        }

        private static StackPanel MakeValueHeader(string? key, string value, Brush valueBrush)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            if (!string.IsNullOrEmpty(key))
            {
                sp.Children.Add(new TextBlock
                {
                    Text       = $"\"{key}\"",
                    Foreground = KeyBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 12
                });
                sp.Children.Add(new TextBlock
                {
                    Text       = ": ",
                    Foreground = TextDefault,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 12
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text       = value,
                Foreground = valueBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 12
            });

            return sp;
        }
    }
}
