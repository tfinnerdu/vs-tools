using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace DoaneDevTools.Visualizers
{
    public class ExceptionVisualizer : DialogDebuggerVisualizer
    {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider)
        {
            using var reader = new StreamReader(objectProvider.GetData());
            var json = reader.ReadToEnd();
            using var doc = JsonDocument.Parse(json);

            var window = new Window
            {
                Title = "Doane Exception Inspector",
                Width = 700,
                Height = 500,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(16) };

            BuildExceptionView(stack, doc.RootElement, 0);

            scroll.Content = stack;
            window.Content = scroll;
            window.ShowDialog();
        }

        private static void BuildExceptionView(StackPanel parent, JsonElement element, int depth)
        {
            var indent = new Thickness(depth * 16, 0, 0, 0);

            if (element.TryGetProperty("ExceptionType", out var exType))
                parent.Children.Add(new TextBlock
                {
                    Text = exType.GetString(),
                    FontSize = 14, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(244, 135, 113)),
                    Margin = new Thickness(indent.Left, 8, 0, 4)
                });

            if (element.TryGetProperty("Message", out var msg))
                parent.Children.Add(new TextBlock
                {
                    Text = msg.GetString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    FontSize = 13, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(indent.Left, 0, 0, 8)
                });

            if (element.TryGetProperty("StackTrace", out var st) && st.GetString() is string stackTrace)
            {
                var stBlock = new TextBlock
                {
                    Text = stackTrace,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(indent.Left, 0, 0, 12)
                };
                parent.Children.Add(stBlock);
            }

            if (element.TryGetProperty("InnerException", out var inner) &&
                inner.ValueKind == JsonValueKind.Object)
            {
                parent.Children.Add(new TextBlock
                {
                    Text = "↳ Inner Exception:",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 121, 0)),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(indent.Left + 8, 0, 0, 4)
                });
                BuildExceptionView(parent, inner, depth + 1);
            }
        }
    }

    public class ExceptionVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            var ex = (Exception)target;
            var info = Serialize(ex);
            using var writer = new StreamWriter(outgoingData, leaveOpen: true);
            writer.Write(JsonSerializer.Serialize(info));
            writer.Flush();
        }

        private static object Serialize(Exception? ex)
        {
            if (ex == null) return new { };
            return new
            {
                ExceptionType = ex.GetType().FullName,
                ex.Message,
                StackTrace = ex.StackTrace ?? string.Empty,
                InnerException = ex.InnerException != null ? Serialize(ex.InnerException) : null
            };
        }
    }
}
