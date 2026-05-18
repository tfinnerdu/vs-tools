using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.DependencyMap.Models;
using DoaneDevTools.ToolWindows.DependencyMap.Services;
using DoaneDevTools.ToolWindows.Infrastructure;
using Microsoft.CodeAnalysis;

namespace DoaneDevTools.ToolWindows.DependencyMap
{
    /// <summary>
    /// ViewModel for the Dependency Map tool window.
    /// Drives graph construction via Roslyn, force-directed layout,
    /// cycle detection, and interactive impact analysis.
    /// </summary>
    public sealed class DependencyMapViewModel : ViewModelBase
    {
        private readonly RoslynGraphBuilder _graphBuilder = new RoslynGraphBuilder();
        private readonly ForceLayoutService _layoutService = new ForceLayoutService();

        // Roslyn graph (built on demand) — held so impact queries can reuse it.
        private Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>? _typeDeps;
        private Dictionary<ITypeSymbol, string>?                _symbolToId;

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------

        private ObservableCollection<GraphNode> _nodes   = new ObservableCollection<GraphNode>();
        private ObservableCollection<GraphEdge> _edges   = new ObservableCollection<GraphEdge>();
        private ObservableCollection<GraphNode> _impactedNodes = new ObservableCollection<GraphNode>();
        private ObservableCollection<string>    _circularDependencies = new ObservableCollection<string>();

        private NodeType   _selectedScope = NodeType.Class;
        private int        _depth         = 2;
        private string     _metricsOverlay = "None";
        private GraphNode? _selectedNode;
        private string     _statusText    = "Ready. Click 'Build Graph' to analyse the solution.";
        private bool       _isBusy;

        private Compilation? _compilation;   // set via SetCompilation() from the VS host

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public DependencyMapViewModel()
        {
            BuildGraphCommand  = new RelayCommand(_ => _ = BuildGraphAsync(), _ => !IsBusy && _compilation != null);
            ShowImpactCommand  = new RelayCommand(param => _ = ShowImpactAsync(param as GraphNode), _ => !IsBusy);
            SelectNodeCommand  = new RelayCommand(param => SelectedNode = param as GraphNode);
            ClearImpactCommand = new RelayCommand(_ => ClearImpact(), _ => ImpactedNodes.Count > 0);
        }

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        public ObservableCollection<GraphNode> Nodes
        {
            get => _nodes;
            private set => SetProperty(ref _nodes, value);
        }

        public ObservableCollection<GraphEdge> Edges
        {
            get => _edges;
            private set => SetProperty(ref _edges, value);
        }

        /// <summary>Nodes highlighted as "will break" by the impact query.</summary>
        public ObservableCollection<GraphNode> ImpactedNodes
        {
            get => _impactedNodes;
            private set => SetProperty(ref _impactedNodes, value);
        }

        /// <summary>Descriptions of detected circular dependency cycles.</summary>
        public ObservableCollection<string> CircularDependencies
        {
            get => _circularDependencies;
            private set => SetProperty(ref _circularDependencies, value);
        }

        public NodeType SelectedScope
        {
            get => _selectedScope;
            set => SetProperty(ref _selectedScope, value);
        }

        /// <summary>Maximum dependency depth to traverse when building the graph (1–4).</summary>
        public int Depth
        {
            get => _depth;
            set => SetProperty(ref _depth, Math.Clamp(value, 1, 4));
        }

        /// <summary>Which metric to colour nodes by: None / LOC / Complexity / Coupling.</summary>
        public string MetricsOverlay
        {
            get => _metricsOverlay;
            set => SetProperty(ref _metricsOverlay, value);
        }

        public GraphNode? SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (BuildGraphCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ShowImpactCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ClearImpactCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // -----------------------------------------------------------------------
        // Computed status summary
        // -----------------------------------------------------------------------

        public string GraphSummary =>
            $"{Nodes.Count} types, {Edges.Count} edges, " +
            $"{CircularDependencies.Count} circular dependencies detected";

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------

        public RelayCommand BuildGraphCommand  { get; }
        public RelayCommand ShowImpactCommand  { get; }
        public RelayCommand SelectNodeCommand  { get; }
        public RelayCommand ClearImpactCommand { get; }

        // -----------------------------------------------------------------------
        // Public host-facing API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called by the code-behind once a Roslyn <see cref="Compilation"/> is
        /// available (e.g. obtained from the VS workspace).
        /// </summary>
        public void SetCompilation(Compilation compilation)
        {
            _compilation = compilation;
            (BuildGraphCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void SetCanvasSize(double width, double height)
        {
            _layoutService.CanvasWidth  = Math.Max(width,  400);
            _layoutService.CanvasHeight = Math.Max(height, 300);
        }

        // -----------------------------------------------------------------------
        // Private command implementations
        // -----------------------------------------------------------------------

        private async Task BuildGraphAsync()
        {
            if (_compilation == null) return;

            IsBusy = true;
            StatusText = "Analysing solution…";
            Nodes.Clear();
            Edges.Clear();
            CircularDependencies.Clear();
            ImpactedNodes.Clear();

            try
            {
                var (nodes, edges) = await _graphBuilder.BuildAsync(_compilation, SelectedScope);

                // Run force-directed layout on a background thread.
                await Task.Run(() => _layoutService.Run(nodes, edges));

                foreach (var node in nodes) Nodes.Add(node);
                foreach (var edge in edges) Edges.Add(edge);

                // Collect cycle descriptions.
                var cycleNodes = nodes.Where(n => n.IsInCycle).ToList();
                foreach (var cn in cycleNodes)
                    CircularDependencies.Add(cn.FullName);

                OnPropertyChanged(nameof(GraphSummary));
                StatusText = GraphSummary;
            }
            catch (Exception ex)
            {
                StatusText = $"Error building graph: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShowImpactAsync(GraphNode? node)
        {
            if (node == null || _typeDeps == null || _symbolToId == null) return;

            IsBusy = true;
            StatusText = $"Computing impact of '{node.Name}'…";

            try
            {
                // Find the matching ITypeSymbol from the cached graph.
                var symbol = _symbolToId
                    .FirstOrDefault(kv => kv.Value == node.Id).Key;

                if (symbol == null)
                {
                    StatusText = $"Symbol for '{node.Name}' not found in graph.";
                    return;
                }

                var impactedIds = await Task.Run(() =>
                    _graphBuilder.GetImpact(symbol, _typeDeps, _symbolToId));

                ClearImpact();

                foreach (var graphNode in Nodes)
                {
                    if (impactedIds.Contains(graphNode.Id))
                    {
                        graphNode.IsImpacted = true;
                        ImpactedNodes.Add(graphNode);
                    }
                }

                (ClearImpactCommand as RelayCommand)?.RaiseCanExecuteChanged();
                StatusText = $"{ImpactedNodes.Count} types impacted by changes to '{node.Name}'.";
            }
            catch (Exception ex)
            {
                StatusText = $"Impact analysis failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearImpact()
        {
            foreach (var node in Nodes)
                node.IsImpacted = false;
            ImpactedNodes.Clear();
            (ClearImpactCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
