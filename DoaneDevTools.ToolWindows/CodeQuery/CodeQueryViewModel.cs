using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DoaneDevTools.ToolWindows.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DoaneDevTools.ToolWindows.CodeQuery
{
    public class CodeQueryGlobals
    {
        public Compilation? Compilation { get; set; }
        public IEnumerable<IMethodSymbol> Methods { get; set; } = Enumerable.Empty<IMethodSymbol>();
        public IEnumerable<INamedTypeSymbol> Types { get; set; } = Enumerable.Empty<INamedTypeSymbol>();
        public Dictionary<string, int> Metrics { get; set; } = new();
    }

    public class CodeQueryViewModel : INotifyPropertyChanged
    {
        private string _queryText = string.Empty;
        private string _statusText = "Ready";
        private string _errorText = string.Empty;
        private string _selectedExample = string.Empty;
        private Compilation? _currentCompilation;

        private static readonly Dictionary<string, string> Examples = new()
        {
            ["Methods with > 5 parameters"] =
                "from m in Methods\nwhere m.Parameters.Length > 5 && !m.IsConstructor\nselect new { Type = m.ContainingType?.Name, Method = m.Name, ParamCount = m.Parameters.Length }",

            ["Classes with > 10 public methods"] =
                "from t in Types\nlet pubMethods = t.GetMembers().OfType<IMethodSymbol>().Count(m => m.DeclaredAccessibility == Accessibility.Public)\nwhere pubMethods > 10\nselect new { Type = t.Name, Namespace = t.ContainingNamespace?.ToString(), PublicMethods = pubMethods }",

            ["Async void methods (not event handlers)"] =
                "from m in Methods\nwhere m.IsAsync && m.ReturnsVoid\n  && !m.Parameters.Any(p => p.Type.Name.Contains(\"EventArgs\"))\nselect new { Type = m.ContainingType?.Name, Method = m.Name }",

            ["Interfaces never implemented"] =
                "from t in Types\nwhere t.TypeKind == TypeKind.Interface\nlet impls = Types.Where(c => c.AllInterfaces.Contains(t, SymbolEqualityComparer.Default)).Count()\nwhere impls == 0\nselect new { Interface = t.Name, Namespace = t.ContainingNamespace?.ToString() }",

            ["Methods returning Task not in async context"] =
                "from m in Methods\nwhere m.ReturnType.Name.StartsWith(\"Task\") && !m.IsAsync\n  && !m.Name.EndsWith(\"Async\")\nselect new { Type = m.ContainingType?.Name, Method = m.Name, ReturnType = m.ReturnType.Name }",

            ["Types with no public constructor"] =
                "from t in Types\nwhere t.TypeKind == TypeKind.Class && !t.IsStatic && !t.IsAbstract\n  && !t.Constructors.Any(c => c.DeclaredAccessibility == Accessibility.Public)\nselect new { Type = t.Name, Namespace = t.ContainingNamespace?.ToString() }",

            ["Most coupled types (efferent)"] =
                "from t in Types\nlet ce = Metrics.GetValueOrDefault(t.ToDisplayString() + \".ce\")\norderby ce descending\nselect new { Type = t.Name, Namespace = t.ContainingNamespace?.ToString(), EfferentCoupling = ce }"
        };

        public string QueryText
        {
            get => _queryText;
            set
            {
                _queryText = value;
                OnPropertyChanged(nameof(QueryText));
                OnPropertyChanged(nameof(IsQueryEmpty));
            }
        }

        public bool IsQueryEmpty => string.IsNullOrWhiteSpace(_queryText);

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public string ErrorText
        {
            get => _errorText;
            set { _errorText = value; OnPropertyChanged(nameof(ErrorText)); }
        }

        public string SelectedExample
        {
            get => _selectedExample;
            set { _selectedExample = value; OnPropertyChanged(nameof(SelectedExample)); }
        }

        public List<string> ExampleQueries { get; } = Examples.Keys.ToList();

        public ObservableCollection<object> Results { get; } = new();

        public ICommand RunQueryCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand LoadExampleCommand { get; }

        public CodeQueryViewModel()
        {
            RunQueryCommand = new RelayCommand(async _ => await RunQueryAsync());
            ClearCommand = new RelayCommand(_ => { QueryText = string.Empty; Results.Clear(); ErrorText = string.Empty; });
            LoadExampleCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(SelectedExample) && Examples.TryGetValue(SelectedExample, out var q))
                    QueryText = q;
            });
        }

        /// <summary>Called by the package when the VS workspace changes.</summary>
        public void SetCompilation(Compilation compilation) => _currentCompilation = compilation;

        private async Task RunQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(QueryText)) return;

            Results.Clear();
            ErrorText = string.Empty;
            StatusText = "Running...";

            try
            {
                var globals = BuildGlobals();
                var options = ScriptOptions.Default
                    .WithReferences(
                        typeof(IMethodSymbol).Assembly,
                        typeof(Enumerable).Assembly)
                    .WithImports(
                        "System",
                        "System.Linq",
                        "System.Collections.Generic",
                        "Microsoft.CodeAnalysis",
                        "Microsoft.CodeAnalysis.CSharp");

                var script = CSharpScript.Create<IEnumerable<object>>(
                    QueryText, options, globalsType: typeof(CodeQueryGlobals));

                var result = await script.RunAsync(globals);
                var items = result.ReturnValue?.ToList() ?? new List<object>();

                foreach (var item in items)
                    Results.Add(item);

                StatusText = $"Done — {items.Count} result(s)";
            }
            catch (CompilationErrorException ex)
            {
                ErrorText = string.Join("\n", ex.Diagnostics.Select(d => d.ToString()));
                StatusText = "Compilation error";
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
                StatusText = "Runtime error";
            }
        }

        private CodeQueryGlobals BuildGlobals()
        {
            var globals = new CodeQueryGlobals();

            if (_currentCompilation != null)
            {
                globals.Compilation = _currentCompilation;
                globals.Methods = _currentCompilation.SyntaxTrees
                    .SelectMany(t =>
                    {
                        var model = _currentCompilation.GetSemanticModel(t);
                        return t.GetRoot().DescendantNodes()
                            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
                            .Select(m => model.GetDeclaredSymbol(m) as IMethodSymbol)
                            .Where(m => m != null)!;
                    })
                    .ToList();

                globals.Types = _currentCompilation.SyntaxTrees
                    .SelectMany(t =>
                    {
                        var model = _currentCompilation.GetSemanticModel(t);
                        return t.GetRoot().DescendantNodes()
                            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
                            .Select(td => model.GetDeclaredSymbol(td) as INamedTypeSymbol)
                            .Where(ts => ts != null)!;
                    })
                    .ToList();
            }

            return globals;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
