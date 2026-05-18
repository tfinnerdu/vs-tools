using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DoaneDevTools.ToolWindows.DependencyMap.Models;

namespace DoaneDevTools.ToolWindows.DependencyMap
{
    /// <summary>
    /// Code-behind for <see cref="DependencyMapWindowControl"/>.
    ///
    /// Responsibilities:
    ///   • Wires ViewModel to the view and hooks collection-changed events to
    ///     redraw edges on the Canvas when Nodes/Edges update.
    ///   • Draws edge lines on the EdgeLayer canvas.
    ///   • Handles mouse drag for node panning and Ctrl+Wheel for zoom.
    ///   • Routes scope combobox changes to the ViewModel.
    /// </summary>
    public partial class DependencyMapWindowControl : UserControl
    {
        // -----------------------------------------------------------------------
        // Shared value converters (referenced from XAML via x:Static)
        // -----------------------------------------------------------------------

        public static readonly IValueConverter InverseBoolConverter = new DmInverseBooleanConverter();
        public static readonly IValueConverter BoolToVisConverter   = new DmBoolToVisibilityConverter();

        // -----------------------------------------------------------------------
        // Drag / pan / zoom state
        // -----------------------------------------------------------------------

        private bool   _isDragging;
        private Point  _dragStart;
        private GraphNode? _dragNode;

        private double _zoomScale = 1.0;
        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1, 1);
        private readonly TranslateTransform _panTransform = new TranslateTransform(0, 0);

        private DependencyMapViewModel? _vm;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public DependencyMapWindowControl()
        {
            InitializeComponent();

            _vm = new DependencyMapViewModel();
            DataContext = _vm;

            // Apply zoom/pan transform to the canvas.
            var group = new TransformGroup();
            group.Children.Add(_scaleTransform);
            group.Children.Add(_panTransform);
            GraphCanvas.RenderTransform = group;

            // Redraw edges whenever the Edges collection changes.
            _vm.Edges.CollectionChanged += (s, e) => RedrawEdges();
            _vm.Nodes.CollectionChanged += (s, e) =>
            {
                // Let the layout settle, then draw edges once nodes are positioned.
                Dispatcher.BeginInvoke(new Action(RedrawEdges),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            };

            // Update canvas size in the ViewModel so layout uses correct dimensions.
            SizeChanged += (s, e) =>
            {
                _vm.SetCanvasSize(GraphCanvas.ActualWidth, GraphCanvas.ActualHeight);
            };
        }

        // -----------------------------------------------------------------------
        // Edge rendering
        // -----------------------------------------------------------------------

        private void RedrawEdges()
        {
            if (_vm == null) return;
            EdgeLayer.Children.Clear();

            foreach (var edge in _vm.Edges)
            {
                var srcNode = FindNode(edge.SourceId);
                var tgtNode = FindNode(edge.TargetId);

                if (srcNode == null || tgtNode == null) continue;

                // Centre-to-centre line.
                double x1 = srcNode.X + srcNode.Width  / 2;
                double y1 = srcNode.Y + srcNode.Height / 2;
                double x2 = tgtNode.X + tgtNode.Width  / 2;
                double y2 = tgtNode.Y + tgtNode.Height / 2;

                var line = new Line
                {
                    X1 = x1, Y1 = y1,
                    X2 = x2, Y2 = y2,
                    StrokeThickness = 1.0,
                    Stroke = PickEdgeBrush(edge.Type),
                    Opacity = 0.6
                };

                // Arrow head at target end using a small Polyline.
                var arrowHead = MakeArrowHead(x1, y1, x2, y2);

                EdgeLayer.Children.Add(line);
                EdgeLayer.Children.Add(arrowHead);
            }
        }

        private static Polyline MakeArrowHead(double x1, double y1, double x2, double y2)
        {
            const double arrowLen   = 10;
            const double arrowAngle = Math.PI / 7;

            double angle = Math.Atan2(y2 - y1, x2 - x1);

            var p1 = new Point(
                x2 - arrowLen * Math.Cos(angle - arrowAngle),
                y2 - arrowLen * Math.Sin(angle - arrowAngle));
            var p2 = new Point(
                x2 - arrowLen * Math.Cos(angle + arrowAngle),
                y2 - arrowLen * Math.Sin(angle + arrowAngle));

            return new Polyline
            {
                Points = new PointCollection { p1, new Point(x2, y2), p2 },
                Stroke = Brushes.Gray,
                StrokeThickness = 1.0,
                Opacity = 0.7
            };
        }

        private static Brush PickEdgeBrush(EdgeType type) => type switch
        {
            EdgeType.Inheritance    => new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),
            EdgeType.Implementation => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
            _                       => Brushes.Gray
        };

        private GraphNode? FindNode(string id)
        {
            if (_vm == null) return null;
            foreach (var node in _vm.Nodes)
                if (node.Id == id) return node;
            return null;
        }

        // -----------------------------------------------------------------------
        // Mouse interactions: drag nodes, pan canvas, zoom
        // -----------------------------------------------------------------------

        private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _dragStart = e.GetPosition(GraphCanvas);
            _isDragging = true;
            GraphCanvas.CaptureMouse();

            // Check if a node was hit.
            _dragNode = HitTestNode(_dragStart);
            if (_dragNode != null && _vm != null)
            {
                _vm.SelectNodeCommand.Execute(_dragNode);
                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var pos   = e.GetPosition(GraphCanvas);
            double dx = pos.X - _dragStart.X;
            double dy = pos.Y - _dragStart.Y;

            if (_dragNode != null)
            {
                // Move the individual node.
                _dragNode.X += dx;
                _dragNode.Y += dy;

                // Force ItemsControl to reposition (properties changed externally).
                // Since GraphNode does not implement INPC, we refresh the canvas.
                NodesItemsControl.Items.Refresh();
                RedrawEdges();
            }
            else
            {
                // Pan the entire canvas.
                _panTransform.X += dx;
                _panTransform.Y += dy;
            }

            _dragStart = pos;
        }

        private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _dragNode   = null;
            GraphCanvas.ReleaseMouseCapture();
        }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            double factor = e.Delta > 0 ? 1.1 : (1.0 / 1.1);
            _zoomScale = Math.Clamp(_zoomScale * factor, 0.1, 5.0);

            var pos = e.GetPosition(GraphCanvas);
            _scaleTransform.CenterX = pos.X;
            _scaleTransform.CenterY = pos.Y;
            _scaleTransform.ScaleX  = _zoomScale;
            _scaleTransform.ScaleY  = _zoomScale;

            e.Handled = true;
        }

        // -----------------------------------------------------------------------
        // Scope combobox
        // -----------------------------------------------------------------------

        private void ScopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || ScopeCombo.SelectedIndex < 0) return;
            _vm.SelectedScope = (NodeType)ScopeCombo.SelectedIndex;
        }

        // -----------------------------------------------------------------------
        // Hit testing
        // -----------------------------------------------------------------------

        private GraphNode? HitTestNode(Point canvasPoint)
        {
            if (_vm == null) return null;

            foreach (var node in _vm.Nodes)
            {
                var rect = new Rect(node.X, node.Y, node.Width, node.Height);
                if (rect.Contains(canvasPoint))
                    return node;
            }
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Value converters (DM-prefixed to avoid collision with the Assembly Inspector's)
    // -----------------------------------------------------------------------

    internal sealed class DmInverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
    }

    internal sealed class DmBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }
}
