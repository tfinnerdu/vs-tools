using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace DoaneDevTools.Visualizers
{
    public class HttpClientVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var client = (HttpClient)target;
            var headers = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var h in client.DefaultRequestHeaders)
                headers[h.Key] = string.Join(", ", h.Value);

            var info = new
            {
                BaseAddress = client.BaseAddress?.ToString() ?? "(none)",
                Timeout = client.Timeout.TotalSeconds + "s",
                MaxResponseContentBufferSize = client.MaxResponseContentBufferSize,
                DefaultHeaders = headers
            };

            var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }
    }

    public class HttpClientVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            var window = new Window
            {
                Title = "Doane HTTP Client Inspector",
                Width = 500,
                Height = 380,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = "HttpClient",
                FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 56, 100)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            void AddRow(string label, string value)
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                row.Children.Add(new TextBlock
                {
                    Text = label + ":",
                    FontWeight = FontWeights.SemiBold,
                    Width = 160,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
                });
                row.Children.Add(new TextBlock
                {
                    Text = value,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    TextWrapping = TextWrapping.Wrap
                });
                stack.Children.Add(row);
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "DefaultHeaders") continue;
                AddRow(prop.Name, prop.Value.ToString());
            }

            if (doc.RootElement.TryGetProperty("DefaultHeaders", out var hdrs) &&
                hdrs.ValueKind == JsonValueKind.Object)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Default Headers:",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 4)
                });

                foreach (var h in hdrs.EnumerateObject())
                    AddRow("  " + h.Name, h.Value.GetString() ?? "");
            }

            window.Content = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            window.ShowDialog();
        }
    }

    public class HttpResponseVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var response = (HttpResponseMessage)target;
            var body = "(unable to read body — already consumed or stream not seekable)";
            try
            {
                // ReadAsStringAsync is async but we're on a sync path in the debuggee
                body = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "(no content)";
                if (body.Length > 4096) body = body[..4096] + "\n[truncated]";
            }
            catch { }

            var headers = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var h in response.Headers)
                headers[h.Key] = string.Join(", ", h.Value);

            var info = new
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                RequestUri = response.RequestMessage?.RequestUri?.ToString() ?? "(unknown)",
                Headers = headers,
                Body = body
            };

            var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
        }
    }
}
