using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DoaneDevTools.ToolWindows.AssemblyInspector.Models;
using DoaneDevTools.ToolWindows.AssemblyInspector.Services;
using DoaneDevTools.ToolWindows.Infrastructure;

namespace DoaneDevTools.ToolWindows.AssemblyInspector
{
    /// <summary>
    /// ViewModel for the Assembly Inspector tool window.
    /// Drives assembly loading, type/member navigation, decompilation, and search.
    /// </summary>
    public sealed class AssemblyInspectorViewModel : ViewModelBase
    {
        private readonly DecompilerService _decompilerService = new DecompilerService();

        // -----------------------------------------------------------------------
        // Backing fields
        // -----------------------------------------------------------------------

        private ObservableCollection<AssemblyNode> _assemblies = new ObservableCollection<AssemblyNode>();
        private AssemblyNode? _selectedAssembly;
        private string _searchText = string.Empty;
        private ObservableCollection<object> _members = new ObservableCollection<object>();
        private string _decompiledSource = string.Empty;
        private bool _showIL;
        private bool _isBusy;
        private string _statusText = string.Empty;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public AssemblyInspectorViewModel()
        {
            LoadAssemblyCommand = new RelayCommand(
                param => _ = LoadAssemblyAsync(param as string ?? string.Empty),
                param => !IsBusy);

            SelectMemberCommand = new RelayCommand(
                param => _ = SelectMemberAsync(param as MethodNode),
                param => param is MethodNode && !IsBusy);

            SelectTypeCommand = new RelayCommand(
                param => _ = SelectTypeAsync(param as TypeNode),
                param => param is TypeNode && !IsBusy);

            CopySourceCommand = new RelayCommand(
                _ => CopySource(),
                _ => !string.IsNullOrEmpty(DecompiledSource));

            ToggleILCommand = new RelayCommand(
                _ => _ = ToggleViewAsync(),
                _ => SelectedAssembly != null && !string.IsNullOrEmpty(DecompiledSource));
        }

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

        /// <summary>All assemblies available for inspection (from the solution).</summary>
        public ObservableCollection<AssemblyNode> Assemblies
        {
            get => _assemblies;
            set => SetProperty(ref _assemblies, value);
        }

        /// <summary>The currently selected assembly in the ComboBox.</summary>
        public AssemblyNode? SelectedAssembly
        {
            get => _selectedAssembly;
            set
            {
                if (SetProperty(ref _selectedAssembly, value) && value != null)
                    _ = LoadAssemblyAsync(value.Path);
            }
        }

        /// <summary>Live search text — filters the Members tree.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplySearchFilter();
            }
        }

        /// <summary>
        /// The filtered member tree items shown in the TreeView.
        /// Contains <see cref="NamespaceNode"/>, <see cref="TypeNode"/>,
        /// and <see cref="MethodNode"/> instances.
        /// </summary>
        public ObservableCollection<object> Members
        {
            get => _members;
            set => SetProperty(ref _members, value);
        }

        /// <summary>C# or IL source displayed in the lower panel.</summary>
        public string DecompiledSource
        {
            get => _decompiledSource;
            set => SetProperty(ref _decompiledSource, value);
        }

        /// <summary>When true, the lower panel shows IL rather than C#.</summary>
        public bool ShowIL
        {
            get => _showIL;
            set => SetProperty(ref _showIL, value);
        }

        /// <summary>True while a background operation is running.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (LoadAssemblyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SelectMemberCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SelectTypeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Status bar message at the bottom of the tool window.</summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        // -----------------------------------------------------------------------
        // Commands
        // -----------------------------------------------------------------------

        public RelayCommand LoadAssemblyCommand { get; }
        public RelayCommand SelectMemberCommand { get; }
        public RelayCommand SelectTypeCommand { get; }
        public RelayCommand CopySourceCommand { get; }
        public RelayCommand ToggleILCommand { get; }

        // -----------------------------------------------------------------------
        // All-assemblies list (called from code-behind when the window initialises)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Loads a list of assemblies from the paths provided (e.g. all DLLs in
        /// the current solution's output directories). Clears and rebuilds
        /// <see cref="Assemblies"/>.
        /// </summary>
        public void SetAssemblyPaths(IEnumerable<string> paths)
        {
            Assemblies.Clear();
            foreach (var path in paths)
            {
                Assemblies.Add(new AssemblyNode
                {
                    Name = System.IO.Path.GetFileName(path),
                    Path = path
                });
            }
            StatusText = $"{Assemblies.Count} assemblies found.";
        }

        // -----------------------------------------------------------------------
        // Private methods
        // -----------------------------------------------------------------------

        private async Task LoadAssemblyAsync(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                return;

            IsBusy = true;
            StatusText = $"Loading {System.IO.Path.GetFileName(assemblyPath)}…";
            Members.Clear();
            DecompiledSource = string.Empty;

            try
            {
                var types = await Task.Run(() => _decompilerService.GetTypes(assemblyPath).ToList());

                // Group types by namespace.
                var grouped = types
                    .GroupBy(t => string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace)
                    .OrderBy(g => g.Key);

                // Update the AssemblyNode in the list.
                var node = Assemblies.FirstOrDefault(a => a.Path == assemblyPath);
                if (node != null)
                {
                    node.Children.Clear();
                    foreach (var group in grouped)
                    {
                        var nsNode = new NamespaceNode { Namespace = group.Key };
                        foreach (var typeInfo in group.OrderBy(t => t.ShortName))
                        {
                            var typeNode = new TypeNode
                            {
                                FullName = typeInfo.FullName,
                                ShortName = typeInfo.ShortName,
                                AssemblyPath = assemblyPath
                            };
                            foreach (var sig in typeInfo.MethodSignatures)
                            {
                                typeNode.Methods.Add(new MethodNode
                                {
                                    Signature = sig,
                                    MethodName = sig.Contains("(") ? sig[..sig.IndexOf('(')] : sig,
                                    AssemblyPath = assemblyPath,
                                    DeclaringTypeFullName = typeInfo.FullName
                                });
                            }
                            nsNode.Types.Add(typeNode);
                        }
                        node.Children.Add(nsNode);
                    }
                }

                RebuildMembersTree(node);
                StatusText = $"Loaded {types.Count} types from {System.IO.Path.GetFileName(assemblyPath)}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SelectMemberAsync(MethodNode? node)
        {
            if (node == null) return;

            IsBusy = true;
            StatusText = $"Decompiling {node.Signature}…";

            try
            {
                string source;
                if (ShowIL)
                {
                    source = await Task.Run(() =>
                        _decompilerService.GetIL(node.AssemblyPath, node.DeclaringTypeFullName));
                }
                else
                {
                    source = await Task.Run(() =>
                        _decompilerService.DecompileMethod(
                            node.AssemblyPath, node.DeclaringTypeFullName, node.MethodName));
                }
                DecompiledSource = source;
            }
            catch (Exception ex)
            {
                DecompiledSource = $"// Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                StatusText = string.Empty;
            }
        }

        private async Task SelectTypeAsync(TypeNode? node)
        {
            if (node == null) return;

            IsBusy = true;
            StatusText = $"Decompiling {node.FullName}…";

            try
            {
                string source;
                if (ShowIL)
                {
                    source = await Task.Run(() =>
                        _decompilerService.GetIL(node.AssemblyPath, node.FullName));
                }
                else
                {
                    source = await Task.Run(() =>
                        _decompilerService.DecompileType(node.AssemblyPath, node.FullName));
                }
                DecompiledSource = source;
            }
            catch (Exception ex)
            {
                DecompiledSource = $"// Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                StatusText = string.Empty;
            }
        }

        private async Task ToggleViewAsync()
        {
            ShowIL = !ShowIL;

            // Re-decompile the currently displayed content in the new mode.
            // Walk the members tree to find a selected type if possible.
            var firstType = FindFirstTypeInMembers();
            if (firstType != null)
                await SelectTypeAsync(firstType);
        }

        private TypeNode? FindFirstTypeInMembers()
        {
            foreach (var item in Members)
            {
                if (item is NamespaceNode ns && ns.Types.Any())
                    return ns.Types[0];
            }
            return null;
        }

        private void CopySource()
        {
            if (!string.IsNullOrEmpty(DecompiledSource))
                Clipboard.SetText(DecompiledSource);
        }

        private void ApplySearchFilter()
        {
            var node = _selectedAssembly
                       ?? (Assemblies.Count > 0 ? Assemblies[0] : null);
            RebuildMembersTree(node);
        }

        private void RebuildMembersTree(AssemblyNode? assemblyNode)
        {
            Members.Clear();
            if (assemblyNode == null) return;

            var filter = _searchText?.Trim() ?? string.Empty;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var nsNode in assemblyNode.Children)
            {
                var filteredTypes = nsNode.Types
                    .Where(t => !hasFilter ||
                        t.ShortName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        t.Methods.Any(m => m.Signature.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!filteredTypes.Any()) continue;

                var filteredNs = new NamespaceNode { Namespace = nsNode.Namespace };
                foreach (var typeNode in filteredTypes)
                {
                    var filteredType = new TypeNode
                    {
                        FullName = typeNode.FullName,
                        ShortName = typeNode.ShortName,
                        AssemblyPath = typeNode.AssemblyPath
                    };

                    var filteredMethods = hasFilter
                        ? typeNode.Methods.Where(m =>
                            m.Signature.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList()
                        : typeNode.Methods;

                    filteredType.Methods.AddRange(filteredMethods);
                    filteredNs.Types.Add(filteredType);
                }

                Members.Add(filteredNs);
            }
        }
    }
}
