using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using DoaneDevTools.ToolWindows.AssemblyInspector.Models;

namespace DoaneDevTools.ToolWindows.AssemblyInspector
{
    /// <summary>
    /// Code-behind for <see cref="AssemblyInspectorWindowControl"/>.
    /// Handles:
    ///   • TreeView selection → ViewModel commands
    ///   • DecompiledSource changes → RichTextBox syntax highlighting
    ///   • "Open in new tab" button click
    /// </summary>
    public partial class AssemblyInspectorWindowControl : UserControl
    {
        // -----------------------------------------------------------------------
        // Shared value converters (referenced from XAML via x:Static)
        // -----------------------------------------------------------------------

        public static readonly IValueConverter InverseBoolConverter = new InverseBooleanConverter();
        public static readonly IValueConverter BoolToVisConverter   = new BoolToVisibilityConverter();

        // C# keyword set for basic syntax colouring
        private static readonly string[] CSharpKeywords =
        {
            "abstract", "as", "async", "await", "base", "bool", "break", "byte",
            "case", "catch", "char", "checked", "class", "const", "continue",
            "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float",
            "for", "foreach", "get", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "set", "short", "sizeof", "stackalloc", "static", "string", "struct",
            "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "value", "var", "virtual",
            "void", "volatile", "where", "while", "yield"
        };

        private static readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        private static readonly SolidColorBrush StringBrush  = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
        private static readonly SolidColorBrush CommentBrush = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
        private static readonly SolidColorBrush TypeBrush    = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
        private static readonly SolidColorBrush DefaultBrush = SystemColors.WindowTextBrush;

        private AssemblyInspectorViewModel? _vm;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public AssemblyInspectorWindowControl()
        {
            InitializeComponent();
            _vm = new AssemblyInspectorViewModel();
            DataContext = _vm;

            // Re-render source whenever the ViewModel's DecompiledSource changes.
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AssemblyInspectorViewModel.DecompiledSource))
                    ApplySyntaxHighlighting(_vm.DecompiledSource, _vm.ShowIL);
            };
        }

        // -----------------------------------------------------------------------
        // TreeView selection
        // -----------------------------------------------------------------------

        private void MembersTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_vm == null) return;

            switch (e.NewValue)
            {
                case MethodNode method:
                    _vm.SelectMemberCommand.Execute(method);
                    break;
                case TypeNode type:
                    _vm.SelectTypeCommand.Execute(type);
                    break;
            }
        }

        // -----------------------------------------------------------------------
        // Open in Tab
        // -----------------------------------------------------------------------

        private void OpenInTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null || string.IsNullOrEmpty(_vm.DecompiledSource))
                return;

            try
            {
                // Write to a temp file and open it via the VS shell.
                var tempPath = Path.Combine(Path.GetTempPath(),
                    $"AssemblyInspector_{Guid.NewGuid():N}.cs");
                File.WriteAllText(tempPath, _vm.DecompiledSource);

                // Open via the operating-system default handler for .cs files.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "Assembly Inspector",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // -----------------------------------------------------------------------
        // Syntax highlighting
        // -----------------------------------------------------------------------

        /// <summary>
        /// Populates the <see cref="SourceBox"/> RichTextBox with syntax-coloured
        /// runs. Supports C# keyword/string/comment highlighting and a plain-text
        /// IL mode.
        /// </summary>
        private void ApplySyntaxHighlighting(string source, bool isIL)
        {
            var doc = new FlowDocument();
            var paragraph = new Paragraph { FontFamily = new FontFamily("Consolas"), FontSize = 12 };

            if (string.IsNullOrEmpty(source))
            {
                SourceBox.Document = doc;
                return;
            }

            if (isIL)
            {
                // IL: colour IL opcodes (lowercase words at start of token) in a
                // distinctive colour; leave everything else as default.
                foreach (var line in source.Split('\n'))
                {
                    AppendILLine(paragraph, line.TrimEnd('\r'));
                    paragraph.Inlines.Add(new LineBreak());
                }
            }
            else
            {
                // C#: tokenise line-by-line.
                foreach (var line in source.Split('\n'))
                {
                    AppendCSharpLine(paragraph, line.TrimEnd('\r'));
                    paragraph.Inlines.Add(new LineBreak());
                }
            }

            doc.Blocks.Add(paragraph);
            SourceBox.Document = doc;
        }

        private static void AppendCSharpLine(Paragraph paragraph, string line)
        {
            // Handle single-line // comments.
            var commentIdx = FindCommentStart(line);
            string codePart   = commentIdx >= 0 ? line[..commentIdx] : line;
            string commentPart = commentIdx >= 0 ? line[commentIdx..] : string.Empty;

            // Tokenise the code part.
            var tokens = Regex.Split(codePart, @"(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)'|\b\w+\b|[^\w\s])");
            foreach (var token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                Brush brush = DefaultBrush;

                if ((token.StartsWith("\"") && token.EndsWith("\"")) ||
                    (token.StartsWith("'") && token.EndsWith("'") && token.Length <= 4))
                {
                    brush = StringBrush;
                }
                else if (IsKeyword(token))
                {
                    brush = KeywordBrush;
                }
                else if (token.Length > 0 && char.IsUpper(token[0]) && Regex.IsMatch(token, @"^\w+$"))
                {
                    brush = TypeBrush;
                }

                paragraph.Inlines.Add(new Run(token) { Foreground = brush });
            }

            if (!string.IsNullOrEmpty(commentPart))
                paragraph.Inlines.Add(new Run(commentPart) { Foreground = CommentBrush });
        }

        private static void AppendILLine(Paragraph paragraph, string line)
        {
            // IL lines: instructions are indented and start with lowercase opcode.
            var trimmed = line.TrimStart();
            var indent  = line[..(line.Length - trimmed.Length)];

            if (!string.IsNullOrEmpty(indent))
                paragraph.Inlines.Add(new Run(indent));

            // Opcode is first token.
            var spaceIdx = trimmed.IndexOf(' ');
            var opcode   = spaceIdx > 0 ? trimmed[..spaceIdx] : trimmed;
            var rest     = spaceIdx > 0 ? trimmed[spaceIdx..] : string.Empty;

            var opBrush = Regex.IsMatch(opcode, @"^[a-z]") ? KeywordBrush : DefaultBrush;
            paragraph.Inlines.Add(new Run(opcode) { Foreground = opBrush });

            if (!string.IsNullOrEmpty(rest))
                paragraph.Inlines.Add(new Run(rest) { Foreground = DefaultBrush });
        }

        private static int FindCommentStart(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length - 1; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    inString = !inString;
                if (!inString && line[i] == '/' && line[i + 1] == '/')
                    return i;
            }
            return -1;
        }

        private static bool IsKeyword(string token) =>
            Array.IndexOf(CSharpKeywords, token) >= 0;
    }

    // -----------------------------------------------------------------------
    // Value converters
    // -----------------------------------------------------------------------

    internal sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && !b;
    }

    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }
}
